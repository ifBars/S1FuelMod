using UnityEngine;
using ScheduleOne.Interaction;
using ScheduleOne.Vehicles;
using ScheduleOne.Money;
using S1FuelMod.Utils;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// FuelStation component that handles vehicle refueling interactions
    /// Attaches to gameobjects named "Bowser (EMC Merge)" to make them functional fuel stations
    /// </summary>
    public class FuelStation : InteractableObject
    {
        [Header("Fuel Station Settings")]
        [SerializeField] private float refuelRate = 10f; // liters per second
        [SerializeField] private float pricePerLiter = 1.5f;
        [SerializeField] private float maxInteractionDistance = 4f;
        [SerializeField] private float vehicleDetectionRadius = 6f;
        [SerializeField] private LayerMask vehicleLayerMask = ~0; // All layers by default

        [Header("Audio")]
        [SerializeField] private AudioSource refuelAudioSource;
        [SerializeField] private AudioClip refuelStartSound;
        [SerializeField] private AudioClip refuelLoopSound;
        [SerializeField] private AudioClip refuelEndSound;

        // State tracking
        private bool _isRefueling = false;
        private LandVehicle _targetVehicle;
        private VehicleFuelSystem _targetFuelSystem;
        private float _refuelStartTime;
        private float _totalFuelAdded;
        private float _totalCost;

        // Components
        private MoneyManager _moneyManager;
        private FuelSystemManager _fuelSystemManager;

        private void Start()
        {
            try
            {
                // Initialize fuel station
                InitializeFuelStation();

                ModLogger.Info($"FuelStation initialized at {transform.position}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error initializing FuelStation", ex);
            }
        }

        /// <summary>
        /// Initialize the fuel station component
        /// </summary>
        private void InitializeFuelStation()
        {
            // Set up as interactable
            SetMessage("Refuel Vehicle - Hold to refuel");
            SetInteractionType(EInteractionType.Key_Press);
            MaxInteractionRange = maxInteractionDistance;
            RequiresUniqueClick = false; // Allow holding to refuel

            // Get required managers
            if (MoneyManager.InstanceExists)
            {
                _moneyManager = MoneyManager.Instance;
            }

            if (Core.Instance?.GetFuelSystemManager() != null)
            {
                _fuelSystemManager = Core.Instance.GetFuelSystemManager();
            }

            // Set up audio if not assigned
            if (refuelAudioSource == null)
            {
                refuelAudioSource = GetComponent<AudioSource>();
                if (refuelAudioSource == null)
                {
                    refuelAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Configure audio source
            if (refuelAudioSource != null)
            {
                refuelAudioSource.playOnAwake = false;
                refuelAudioSource.loop = false;
                refuelAudioSource.volume = 0.7f;
                refuelAudioSource.spatialBlend = 1f; // 3D sound
                refuelAudioSource.maxDistance = 20f;
            }

            // Set interaction values from constants
            refuelRate = Constants.Fuel.REFUEL_RATE;
            pricePerLiter = Constants.Fuel.FUEL_PRICE_PER_LITER;
        }

        public override void Hovered()
        {
            // Check for nearby owned vehicles before showing interaction
            var nearbyVehicle = GetNearestOwnedVehicle();

            if (nearbyVehicle != null)
            {
                var fuelSystem = _fuelSystemManager?.GetFuelSystem(nearbyVehicle);
                if (fuelSystem != null)
                {
                    SetFuelPrice();
                    float fuelNeeded = fuelSystem.MaxFuelCapacity - fuelSystem.CurrentFuelLevel;
                    float estimatedCost = fuelNeeded * pricePerLiter;

                    if (fuelNeeded > 0.1f) // Only show if vehicle needs fuel
                    {
                        SetMessage($"Refuel {nearbyVehicle.VehicleName} - {MoneyManager.FormatAmount(estimatedCost)}");
                        SetInteractableState(EInteractableState.Default);
                    }
                    else
                    {
                        SetMessage($"{nearbyVehicle.VehicleName} - Tank Full");
                        SetInteractableState(EInteractableState.Invalid);
                    }
                }
                else
                {
                    SetMessage("Vehicle has no fuel system");
                    SetInteractableState(EInteractableState.Invalid);
                }
            }
            else
            {
                SetMessage("No owned vehicle nearby");
                SetInteractableState(EInteractableState.Invalid);
            }

            base.Hovered();
        }

        public override void StartInteract()
        {
            if (_isRefueling) return;

            try
            {
                // Show FuelGaugeUI when hovering
                Core.Instance?.GetFuelUIManager().ShowFuelGaugeForVehicle(GetNearestOwnedVehicle());
                // Find target vehicle
                _targetVehicle = GetNearestOwnedVehicle();
                if (_targetVehicle == null)
                {
                    ShowMessage("No owned vehicle nearby!", MessageType.Error);
                    return;
                }

                // Get fuel system
                _targetFuelSystem = _fuelSystemManager?.GetFuelSystem(_targetVehicle);
                if (_targetFuelSystem == null)
                {
                    ShowMessage("Vehicle has no fuel system!", MessageType.Error);
                    return;
                }

                // Check if vehicle needs fuel
                float fuelNeeded = _targetFuelSystem.MaxFuelCapacity - _targetFuelSystem.CurrentFuelLevel;
                if (fuelNeeded <= 0.1f)
                {
                    ShowMessage("Vehicle tank is already full!", MessageType.Warning);
                    return;
                }

                // Check if player has enough money for at least 1 liter
                if (_moneyManager != null && _moneyManager.onlineBalance < pricePerLiter)
                {
                    ShowMessage($"Insufficient funds! Need {MoneyManager.FormatAmount(pricePerLiter)} minimum", MessageType.Error);
                    return;
                }

                // Start refueling
                StartRefueling();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error starting fuel station interaction", ex);
                ShowMessage("Error starting refuel process", MessageType.Error);
            }

            base.StartInteract();
        }

        public override void EndInteract()
        {
            Core.Instance?.GetFuelUIManager()?.HideFuelGaugeForVehicle(_targetVehicle.GUID.ToString());
            if (_isRefueling)
            {
                StopRefueling();
            }

            base.EndInteract();
        }

        /// <summary>
        /// Start the refueling process
        /// </summary>
        private void StartRefueling()
        {
            _isRefueling = true;
            _refuelStartTime = Time.time;
            _totalFuelAdded = 0f;
            _totalCost = 0f;

            // Play start sound
            PlayRefuelSound(refuelStartSound);

            ModLogger.Info($"Started refueling {_targetVehicle.VehicleName} at fuel station");
            ShowMessage($"Refueling {_targetVehicle.VehicleName}...", MessageType.Info);
        }

        /// <summary>
        /// Stop the refueling process and process payment
        /// </summary>
        private void StopRefueling()
        {
            if (!_isRefueling) return;

            _isRefueling = false;

            // Play end sound
            PlayRefuelSound(refuelEndSound);

            // Process payment if any fuel was added
            if (_totalFuelAdded > 0.01f)
            {
                ProcessPayment();
                ShowMessage($"Refueled {_totalFuelAdded:F1}L for {MoneyManager.FormatAmount(_totalCost)}", MessageType.Success);
                ModLogger.Info($"Completed refueling: {_totalFuelAdded:F1}L for {MoneyManager.FormatAmount(_totalCost)}");
            }
            else
            {
                ShowMessage("No fuel added", MessageType.Warning);
            }

            // Reset state
            _targetVehicle = null;
            _targetFuelSystem = null;
            _totalFuelAdded = 0f;
            _totalCost = 0f;
        }

        /// <summary>
        /// Update refueling process
        /// </summary>
        private void Update()
        {
            if (_isRefueling && _targetFuelSystem != null)
            {
                UpdateRefueling();
            }
        }

        /// <summary>
        /// Update the refueling process
        /// </summary>
        private void UpdateRefueling()
        {
            float deltaTime = Time.deltaTime;
            float fuelToAdd = refuelRate * deltaTime;
            float costForThisFuel = fuelToAdd * pricePerLiter;

            // Check if player has enough money for this fuel amount
            if (_moneyManager != null && _moneyManager.onlineBalance < costForThisFuel)
            {
                // Stop refueling if no money left
                StopRefueling();
                ShowMessage("Insufficient funds to continue refueling!", MessageType.Error);
                return;
            }

            // Add fuel to vehicle
            float actualFuelAdded = _targetFuelSystem.AddFuel(fuelToAdd);
            if (actualFuelAdded > 0f)
            {
                _totalFuelAdded += actualFuelAdded;
                _totalCost += actualFuelAdded * pricePerLiter;

                // Play refuel loop sound occasionally
                if (refuelLoopSound != null && !refuelAudioSource.isPlaying)
                {
                    PlayRefuelSound(refuelLoopSound);
                }
            }
            else
            {
                // Tank is full, stop refueling
                StopRefueling();
                ShowMessage("Vehicle tank is now full!", MessageType.Success);
            }

            // Check if vehicle moved away
            if (Vector3.Distance(transform.position, _targetVehicle.transform.position) > vehicleDetectionRadius)
            {
                StopRefueling();
                ShowMessage("Vehicle moved too far away!", MessageType.Warning);
            }
        }

        /// <summary>
        /// Process payment for the fuel
        /// </summary>
        private void ProcessPayment()
        {
            if (_moneyManager != null && _totalCost > 0f)
            {
                // Create transaction for fuel purchase
                string transactionName = $"Fuel Station";
                string transactionNote = $"Refueled {_targetVehicle.VehicleName} with {_totalFuelAdded:F1}L";

                _moneyManager.CreateOnlineTransaction(transactionName, -_totalCost, 1f, transactionNote);

                ModLogger.Info($"Processed fuel payment: {MoneyManager.FormatAmount(_totalCost)} for {_totalFuelAdded:F1}L");
            }
        }

        /// <summary>
        /// Find the nearest owned vehicle within detection radius
        /// </summary>
        /// <returns>Nearest owned LandVehicle or null if none found</returns>
        private LandVehicle GetNearestOwnedVehicle()
        {
            try
            {
                // Find all vehicles within detection radius
                Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, vehicleDetectionRadius, vehicleLayerMask);

                List<LandVehicle> nearbyVehicles = new List<LandVehicle>();

                foreach (Collider col in nearbyColliders)
                {
                    LandVehicle vehicle = col.GetComponentInParent<LandVehicle>();
                    if (vehicle != null && vehicle.IsPlayerOwned && !nearbyVehicles.Contains(vehicle))
                    {
                        nearbyVehicles.Add(vehicle);
                    }
                }

                // Return the closest owned vehicle
                if (nearbyVehicles.Count > 0)
                {
                    return nearbyVehicles.OrderBy(v => Vector3.Distance(transform.position, v.transform.position)).First();
                }

                return null;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error finding nearest owned vehicle", ex);
                return null;
            }
        }

        /// <summary>
        /// Play a refuel sound effect
        /// </summary>
        /// <param name="clip">Audio clip to play</param>
        private void PlayRefuelSound(AudioClip clip)
        {
            if (refuelAudioSource != null && clip != null)
            {
                refuelAudioSource.clip = clip;
                refuelAudioSource.Play();
            }
        }

        /// <summary>
        /// Show a message to the player
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="type">Message type for color coding</param>
        private void ShowMessage(string message, MessageType type)
        {
            try
            {
                // You can extend this to show UI messages or use the game's notification system
                ModLogger.Info($"FuelStation: {message}");

                // For now, just log the message - you could integrate with the game's UI system here
                switch (type)
                {
                    case MessageType.Error:
                        ModLogger.Warning($"FuelStation Error: {message}");
                        break;
                    case MessageType.Warning:
                        ModLogger.Warning($"FuelStation Warning: {message}");
                        break;
                    case MessageType.Success:
                        ModLogger.Info($"FuelStation Success: {message}");
                        break;
                    case MessageType.Info:
                    default:
                        ModLogger.Info($"FuelStation: {message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing fuel station message", ex);
            }
        }

        /// <summary>
        /// Sets the price per liter for refueling 
        /// </summary>
        private void SetFuelPrice()
        {
            float basePrice = Constants.Fuel.FUEL_PRICE_PER_LITER;
            int hashCode = ("Petrol" + NetworkSingleton<TimeManager>.Instance.DayIndex.ToString()).GetHashCode();
            float time = Mathf.Lerp(0f, 0.2f, Mathf.InverseLerp(-2.1474836E+09f, 2.1474836E+09f, (float)hashCode));
            float finalPrice = basePrice + (basePrice * time);
            pricePerLiter = finalPrice;
            ModLogger.Debug($"Setting fuel price based on time: {basePrice} {time:F2} multiplier to {finalPrice:F2}");
        }

        /// <summary>
        /// Draw debug gizmos in the scene view
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // Draw vehicle detection radius
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, vehicleDetectionRadius);

            // Draw interaction radius
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, maxInteractionDistance);

            // Draw connection line if refueling
            if (_isRefueling && _targetVehicle != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _targetVehicle.transform.position);
            }
        }

        private void OnDestroy()
        {
            try
            {
                // Stop refueling if in progress
                if (_isRefueling)
                {
                    StopRefueling();
                }

                ModLogger.Debug("FuelStation destroyed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error destroying FuelStation", ex);
            }
        }

        /// <summary>
        /// Message types for user feedback
        /// </summary>
        private enum MessageType
        {
            Info,
            Warning,
            Error,
            Success
        }
    }
}