using System;
using System.Collections.Generic;
using System.Linq;
using S1FuelMod.Systems.FuelTypes;
using S1FuelMod.Utils;
#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Interaction;
using ScheduleOne.Levelling;
using ScheduleOne.Money;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
using ScheduleOne.Law;
using ScheduleOne.UI;
#else
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.UI;
using MelonLoader;
using Il2CppInterop.Runtime.Attributes;
#endif
using UnityEngine;
using UnityEngine.Events;


namespace S1FuelMod.Systems
{
    /// <summary>
    /// FuelStation component that handles vehicle refueling interactions
    /// Attaches to gameobjects named "Bowser (EMC Merge)" to make them functional fuel stations
    /// 
    /// Supports both old (slider-based) and new (circular) fuel gauge UI systems based on
    /// the UseNewGaugeUI preference setting. The appropriate gauge type is automatically
    /// shown/hidden during refueling interactions based on the current preference.
    /// </summary>
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class FuelStation : InteractableObject
    {
        // Fuel Station Settings
        private float refuelRate = 40f; // liters per second
        private float pricePerLiter = 1f;
        private float maxInteractionDistance = 4f;
        private float vehicleDetectionRadius = 6f;

        private static readonly FuelTypeId[] EmptyFuelTypes = new FuelTypeId[0];
        private readonly Dictionary<FuelTypeId, float> _fuelTypePrices = new Dictionary<FuelTypeId, float>();
        private FuelTypeId _selectedFuelType = FuelTypeId.Regular;
        private FuelTypeId[] _compatibleFuelTypes = EmptyFuelTypes;

        // Audio
        private AudioSource refuelAudioSource;
        private AudioClip refuelStartSound;
        private AudioClip refuelLoopSound;
        private AudioClip refuelEndSound;

        // State tracking
        private bool _isRefueling = false;
        private LandVehicle _targetVehicle;
        private VehicleFuelSystem _targetFuelSystem;
        private float _refuelStartTime;
        private float _totalFuelAdded;
        private float _totalCost;

        // Caching for performance optimization
        private LandVehicle _cachedVehicle;
        private FuelTypeId[] _cachedCompatibleFuelTypes = EmptyFuelTypes;
        private FuelTypeId _cachedSelectedFuelType = FuelTypeId.Regular;
        private float _cachedPricePerLiter = 1f;
        private float _lastPriceUpdateTime = 0f;
        private const float PRICE_CACHE_DURATION = 1f; // Cache prices for 1 second

        // Components
        private MoneyManager _moneyManager;
        private FuelSystemManager _fuelSystemManager;

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public FuelStation(IntPtr ptr) : base(ptr) { }
#endif

        private void Start()
        {
            try
            {
                // Initialize fuel station
                InitializeFuelStation();

                ModLogger.Debug($"FuelStation initialized at {transform.position}");
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
            SetFuelPrice();
        }

        public override void Hovered()
        {
            try
            {
                // Check for nearby owned vehicles before showing interaction
                var nearbyVehicle = GetNearestOwnedVehicle();

                if (nearbyVehicle != null && nearbyVehicle.gameObject != null)
                {
                    try
                    {
                        var fuelSystem = _fuelSystemManager?.GetFuelSystem(nearbyVehicle.GUID.ToString());
                        if (fuelSystem != null)
                        {
                            // Use cached data if same vehicle, otherwise prepare new fuel selection
                            if (_cachedVehicle != nearbyVehicle)
                            {
                                PrepareFuelSelectionCached(fuelSystem, nearbyVehicle);
                            }

                            float fuelNeeded = fuelSystem.MaxFuelCapacity - fuelSystem.CurrentFuelLevel;
                            float selectedPrice = _cachedPricePerLiter;
                            float estimatedCost = fuelNeeded * selectedPrice;
                            string fuelTypeName = GetFuelTypeDisplayName(_cachedSelectedFuelType);

                            if (fuelNeeded > 0.1f) // Only show if vehicle needs fuel
                            {
                                string compatibilityTag = BuildFuelCompatibilityTag(fuelSystem, _cachedSelectedFuelType);
                                SetMessage($"Refuel {nearbyVehicle.VehicleName} [{fuelTypeName}{compatibilityTag}] - {MoneyManager.FormatAmount(estimatedCost)} | ${selectedPrice:F2}/L");
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
                    catch (Exception ex)
                    {
                        ModLogger.Debug($"Error accessing vehicle properties in Hovered: {ex.Message}");
                        SetMessage("Vehicle not accessible");
                        SetInteractableState(EInteractableState.Invalid);
                    }
                }
                else
                {
                    SetMessage("No owned vehicle nearby");
                    SetInteractableState(EInteractableState.Invalid);
                }

                // Manually invoke base functionality without calling base.Hovered() to avoid IL2CPP recursion
                // Invoke the onHovered event
                if (onHovered != null)
                {
                    onHovered.Invoke();
                }
                
                // Show the interaction message if not disabled
                if (interactionState != EInteractableState.Disabled)
                {
                    ShowMessage();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelStation.Hovered", ex);
                SetMessage("Error");
                SetInteractableState(EInteractableState.Invalid);
            }
        }

        public override void StartInteract()
        {
            if (_isRefueling) return;

            try
            {
                // Find target vehicle
                _targetVehicle = GetNearestOwnedVehicle();
                if (_targetVehicle == null || _targetVehicle.gameObject == null)
                {
                    ShowMessage("No owned vehicle nearby!", MessageType.Error);
                    return;
                }

                // Show appropriate fuel gauge when starting interaction based on preference
                // This ensures the correct gauge type (old slider vs new circular) is displayed
                try
                {
                    var uiManager = Core.Instance?.GetFuelUIManager();
                    if (uiManager != null)
                    {
                        bool useNewGauge = Core.Instance?.UseNewGaugeUI ?? false;
                        if (useNewGauge)
                        {
                            // Use the new circular gauge (FuelGauge class)
                            uiManager.ShowNewFuelGaugeForVehicle(_targetVehicle);
                        }
                        else
                        {
                            // Use the old slider-based gauge (FuelGaugeUI class)
                            uiManager.ShowFuelGaugeForVehicle(_targetVehicle);
                        }
                    }
                }
                catch (Exception uiEx)
                {
                    ModLogger.Debug($"Error showing fuel gauge UI: {uiEx.Message}");
                }

                // Get fuel system
                try
                {
                    _targetFuelSystem = _fuelSystemManager?.GetFuelSystem(_targetVehicle.GUID.ToString());
                }
                catch (Exception fuelSystemEx)
                {
                    ModLogger.Debug($"Error getting fuel system: {fuelSystemEx.Message}");
                    _targetFuelSystem = null;
                }

                if (_targetFuelSystem == null)
                {
                    ShowMessage("Vehicle has no fuel system!", MessageType.Error);
                    return;
                }

                // Use cached data if available, otherwise prepare new fuel selection
                if (_cachedVehicle != _targetVehicle)
                {
                    PrepareFuelSelectionCached(_targetFuelSystem, _targetVehicle);
                }
                
                // Update the non-cached variables for compatibility with existing code
                _selectedFuelType = _cachedSelectedFuelType;
                pricePerLiter = _cachedPricePerLiter;
                _compatibleFuelTypes = _cachedCompatibleFuelTypes;

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

                // Manually invoke base functionality without calling base.StartInteract() to avoid IL2CPP recursion
                if (interactionState != EInteractableState.Invalid)
                {
                    // Invoke the onInteractStart event
                    if (onInteractStart != null)
                    {
                        onInteractStart.Invoke();
                    }

                    // Apply the interaction display scale effect
#if MONO
                    Singleton<InteractionCanvas>.Instance.LerpDisplayScale(0.9f);
#else
                    // IL2CPP safe access
                    try
                    {
                        var interactionManager = Singleton<InteractionManager>.Instance;
                        if (interactionManager != null)
                        {
                            Singleton<InteractionCanvas>.Instance.LerpDisplayScale(0.9f);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Debug($"Error accessing InteractionManager: {ex.Message}");
                    }
#endif
                }

                // Start refueling
                StartRefueling();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error starting fuel station interaction", ex);
                ShowMessage("Error starting refuel process", MessageType.Error);
            }
        }

        public override void EndInteract()
        {
            try
            {
                // Hide appropriate fuel gauge when ending interaction based on preference
                // This ensures the correct gauge type is hidden based on current UI preference
                try
                {
                    var uiManager = Core.Instance?.GetFuelUIManager();
                    if (uiManager != null && _targetVehicle != null)
                    {
                        bool useNewGauge = Core.Instance?.UseNewGaugeUI ?? false;
                        if (useNewGauge)
                        {
                            // Hide the new circular gauge
                            uiManager.HideNewFuelGaugeForVehicle(_targetVehicle.GUID.ToString());
                        }
                        else
                        {
                            // Hide the old slider-based gauge
                            uiManager.HideFuelGaugeForVehicle(_targetVehicle.GUID.ToString());
                        }
                    }
                }
                catch (Exception uiEx)
                {
                    ModLogger.Debug($"Error hiding fuel gauge UI: {uiEx.Message}");
                }

                if (_isRefueling)
                {
                    StopRefueling();
                }

                // Manually invoke base functionality without calling base.EndInteract() to avoid IL2CPP recursion
                // Invoke the onInteractEnd event
                if (onInteractEnd != null)
                {
                    onInteractEnd.Invoke();
                }

                // Reset the interaction display scale effect
#if MONO
                Singleton<InteractionCanvas>.Instance.LerpDisplayScale(1f);
#else
                // IL2CPP safe access
                try
                {
                    var interactionCanvas = Singleton<InteractionCanvas>.Instance;
                    if (interactionCanvas != null)
                    {
                        Singleton<InteractionCanvas>.Instance.LerpDisplayScale(1f);
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Debug($"Error accessing InteractionCanvas in EndInteract: {ex.Message}");
                }
#endif
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelStation.EndInteract", ex);
            }
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

            string fuelTypeName = GetFuelTypeDisplayName(_selectedFuelType);
            string compatibilityTag = BuildFuelCompatibilityTag(_targetFuelSystem, _selectedFuelType);

            ModLogger.Debug($"Started refueling {_targetVehicle.VehicleName} with {fuelTypeName} at fuel station");
            ShowMessage($"Refueling {_targetVehicle.VehicleName} with {fuelTypeName}{compatibilityTag}...", MessageType.Info);
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
                string fuelTypeName = GetFuelTypeDisplayName(_selectedFuelType);
                ProcessPayment();
                ShowMessage($"Refueled {_totalFuelAdded:F1}L of {fuelTypeName} for {MoneyManager.FormatAmount(_totalCost)}", MessageType.Success);
                ModLogger.Debug($"Completed refueling: {_totalFuelAdded:F1}L of {fuelTypeName} for {MoneyManager.FormatAmount(_totalCost)}");
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
            float desiredFuel = refuelRate * deltaTime;
            if (_targetFuelSystem == null)
            {
                return;
            }

            float availableCapacity = Mathf.Max(0f, _targetFuelSystem.MaxFuelCapacity - _targetFuelSystem.CurrentFuelLevel);
            float fuelToAdd = Mathf.Min(desiredFuel, availableCapacity);
            float selectedPrice = GetSelectedFuelPrice();
            float costForThisFuel = fuelToAdd * selectedPrice;

            if (_targetVehicle == null || _targetFuelSystem == null)
            {
                return;
            }

            if (fuelToAdd <= 0.0001f)
            {
                StopRefueling();
                ShowMessage("Vehicle tank is now full!", MessageType.Success);
                return;
            }

            // Check if player has enough money for this fuel amount
            if (_moneyManager != null && _moneyManager.onlineBalance < costForThisFuel)
            {
                // Stop refueling if no money left
                StopRefueling();
                ShowMessage("Insufficient funds to continue refueling!", MessageType.Error);
                return;
            }

            if (!_targetFuelSystem.ChangeFuelType(_selectedFuelType, fuelToAdd))
            {
                StopRefueling();
                ShowMessage("Selected fuel type is incompatible with this vehicle!", MessageType.Error);
                return;
            }

            // Add fuel to vehicle
            float actualFuelAdded = _targetFuelSystem.AddFuel(fuelToAdd);
            if (actualFuelAdded > 0f)
            {
                _totalFuelAdded += actualFuelAdded;
                _totalCost += actualFuelAdded * selectedPrice;

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
            if (_targetVehicle == null || _targetVehicle.transform == null)
                return;
            // Check if vehicle moved away
            if (Vector3.Distance(transform.position, _targetVehicle.transform.position) > vehicleDetectionRadius)
            {
                StopRefueling();
                ShowMessage("Vehicle moved too far away!", MessageType.Warning);
            }
        }

        private void PrepareFuelSelection(VehicleFuelSystem fuelSystem)
        {
            if (fuelSystem == null)
            {
                _selectedFuelType = FuelTypeId.Regular;
                pricePerLiter = Constants.Fuel.FUEL_PRICE_PER_LITER;
                return;
            }

            SetFuelPrice();
            _compatibleFuelTypes = GetCompatibleFuelTypesForVehicle(fuelSystem);

            FuelTypeId preferred = fuelSystem.GetRecommendedFuelType();

            if (!IsFuelTypeCompatible(preferred))
            {
                preferred = GetFallbackFuelType();
            }

            if (!IsFuelTypeCompatible(_selectedFuelType))
            {
                _selectedFuelType = preferred;
            }

            if (!IsFuelTypeCompatible(_selectedFuelType))
            {
                _selectedFuelType = FuelTypeId.Regular;
            }

            pricePerLiter = GetSelectedFuelPrice();
        }

        /// <summary>
        /// Prepare fuel selection with caching for performance optimization
        /// Only recalculates when vehicle changes or prices need updating
        /// </summary>
        /// <param name="fuelSystem">The vehicle's fuel system</param>
        /// <param name="vehicle">The vehicle being refueled</param>
        private void PrepareFuelSelectionCached(VehicleFuelSystem fuelSystem, LandVehicle vehicle)
        {
            if (fuelSystem == null)
            {
                _cachedSelectedFuelType = FuelTypeId.Regular;
                _cachedPricePerLiter = Constants.Fuel.FUEL_PRICE_PER_LITER;
                _cachedVehicle = vehicle;
                return;
            }

            // Cache vehicle-specific data
            _cachedVehicle = vehicle;
            _cachedCompatibleFuelTypes = GetCompatibleFuelTypesForVehicle(fuelSystem);

            FuelTypeId preferred = fuelSystem.GetRecommendedFuelType();

            if (!IsFuelTypeCompatibleCached(preferred))
            {
                preferred = GetFallbackFuelTypeCached();
            }

            if (!IsFuelTypeCompatibleCached(_cachedSelectedFuelType))
            {
                _cachedSelectedFuelType = preferred;
            }

            if (!IsFuelTypeCompatibleCached(_cachedSelectedFuelType))
            {
                _cachedSelectedFuelType = FuelTypeId.Regular;
            }

            // Only update prices if cache is expired or this is a new vehicle
            if (Time.time - _lastPriceUpdateTime > PRICE_CACHE_DURATION)
            {
                UpdatePriceCache();
            }

            _cachedPricePerLiter = GetSelectedFuelPriceCached();
        }

        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private FuelTypeId[] GetCompatibleFuelTypesForVehicle(VehicleFuelSystem fuelSystem)
        {
            if (fuelSystem == null || FuelTypeManager.Instance == null)
            {
                return EmptyFuelTypes;
            }

#if MONO
            var compatList = FuelTypeManager.Instance.GetCompatibleFuelTypes(fuelSystem.VehicleType);
            if (compatList == null || compatList.Count == 0)
            {
                return EmptyFuelTypes;
            }
            return compatList.ToArray();
#else
            var compatArray = FuelTypeManager.Instance.GetCompatibleFuelTypesArray(fuelSystem.VehicleType);
            return compatArray ?? EmptyFuelTypes;
#endif
        }

        private bool IsFuelTypeCompatible(FuelTypeId fuelType)
        {
            if (_compatibleFuelTypes == null)
            {
                return false;
            }

            for (int i = 0; i < _compatibleFuelTypes.Length; i++)
            {
                if (_compatibleFuelTypes[i] == fuelType)
                {
                    return true;
                }
            }

            return false;
        }

        private FuelTypeId GetFallbackFuelType()
        {
            if (_compatibleFuelTypes != null && _compatibleFuelTypes.Length > 0)
            {
                return _compatibleFuelTypes[0];
            }

            return FuelTypeId.Regular;
        }

        /// <summary>
        /// Check if fuel type is compatible using cached compatible fuel types
        /// </summary>
        private bool IsFuelTypeCompatibleCached(FuelTypeId fuelType)
        {
            if (_cachedCompatibleFuelTypes == null)
            {
                return false;
            }

            for (int i = 0; i < _cachedCompatibleFuelTypes.Length; i++)
            {
                if (_cachedCompatibleFuelTypes[i] == fuelType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get fallback fuel type using cached compatible fuel types
        /// </summary>
        private FuelTypeId GetFallbackFuelTypeCached()
        {
            if (_cachedCompatibleFuelTypes != null && _cachedCompatibleFuelTypes.Length > 0)
            {
                return _cachedCompatibleFuelTypes[0];
            }

            return FuelTypeId.Regular;
        }

        /// <summary>
        /// Get selected fuel price using cached data
        /// </summary>
        private float GetSelectedFuelPriceCached()
        {
            return GetFuelPriceForType(_cachedSelectedFuelType);
        }

        /// <summary>
        /// Update price cache with current pricing calculations
        /// </summary>
        private void UpdatePriceCache()
        {
            SetFuelPrice();
            _lastPriceUpdateTime = Time.time;
        }

        /// <summary>
        /// Clear the fuel selection cache (useful when prices change significantly)
        /// </summary>
        private void ClearFuelSelectionCache()
        {
            _cachedVehicle = null;
            _cachedCompatibleFuelTypes = EmptyFuelTypes;
            _cachedSelectedFuelType = FuelTypeId.Regular;
            _cachedPricePerLiter = Constants.Fuel.FUEL_PRICE_PER_LITER;
            _lastPriceUpdateTime = 0f;
        }

        private float GetSelectedFuelPrice()
        {
            return GetFuelPriceForType(_selectedFuelType);
        }

        private float GetFuelPriceForType(FuelTypeId fuelTypeId)
        {
            if (_fuelTypePrices.TryGetValue(fuelTypeId, out float cachedPrice))
            {
                return cachedPrice;
            }

            float fallbackPrice = Core.Instance?.BaseFuelPricePerLiter ?? Constants.Fuel.FUEL_PRICE_PER_LITER;
            return fallbackPrice;
        }

        private string GetFuelTypeDisplayName(FuelTypeId fuelTypeId)
        {
            if (FuelTypeManager.Instance != null)
            {
                string displayName = FuelTypeManager.Instance.GetFuelDisplayName(fuelTypeId);
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }
            }

            return fuelTypeId.ToString();
        }

        private string BuildFuelCompatibilityTag(VehicleFuelSystem fuelSystem, FuelTypeId fuelType)
        {
            if (fuelSystem == null)
            {
                return string.Empty;
            }

            try
            {
                FuelTypeId recommended = fuelSystem.GetRecommendedFuelType();
                bool compatible = fuelSystem.IsFuelCompatible(fuelType);

                if (fuelType == recommended && compatible)
                {
                    return " (Recommended)";
                }

                if (compatible)
                {
                    return " (Compatible)";
                }

                return " (Penalty)";
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error building fuel compatibility tag: {ex.Message}");
                return string.Empty;
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

                ModLogger.Debug($"Processed fuel payment: {MoneyManager.FormatAmount(_totalCost)} for {_totalFuelAdded:F1}L");
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
                // Find all vehicles within detection radius - IL2CPP safe version without layer mask
                Collider[] nearbyColliders = null;
                
                try
                {
                    nearbyColliders = Physics.OverlapSphere(transform.position, vehicleDetectionRadius);
                }
                catch (Exception physicsEx)
                {
                    ModLogger.Error("Error calling Physics.OverlapSphere", physicsEx);
                    return null;
                }
                
                if (nearbyColliders == null)
                {
                    return null;
                }

                List<LandVehicle> nearbyVehicles = new List<LandVehicle>();

                foreach (Collider col in nearbyColliders)
                {
                    // IL2CPP safe null checks
                    if (col == null || col.gameObject == null) continue;
                    
                    LandVehicle vehicle = null;
                    try
                    {
                        vehicle = col.GetComponentInParent<LandVehicle>();
                    }
                    catch (Exception getCompEx)
                    {
                        ModLogger.Debug($"Error getting LandVehicle component: {getCompEx.Message}");
                        continue;
                    }
                    
                    // More defensive IL2CPP null checking and property access
                    if (vehicle != null && vehicle.gameObject != null)
                    {
                        try
                        {
                            // Check if this vehicle is player owned and not already in list
                            if (vehicle.IsPlayerOwned && !nearbyVehicles.Contains(vehicle))
                            {
                                nearbyVehicles.Add(vehicle);
                            }
                        }
                        catch (Exception propEx)
                        {
                            // Skip this vehicle if property access fails in IL2CPP
                            ModLogger.Debug($"Skipping vehicle due to property access error: {propEx.Message}");
                            continue;
                        }
                    }
                }

                // Return the closest owned vehicle
                if (nearbyVehicles.Count > 0)
                {
                    try
                    {
                        var closestVehicle = nearbyVehicles.OrderBy(v => Vector3.Distance(transform.position, v.transform.position)).First();
                        
                        if (Vector3.Distance(transform.position, closestVehicle.transform.position) <= maxInteractionDistance)
                        {
                            return closestVehicle;
                        }
                    }
                    catch (Exception linqEx)
                    {
                        ModLogger.Error("Error processing vehicle list", linqEx);
                    }
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
                ModLogger.Debug($"FuelStation: {message}");

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
            float basePrice = Core.Instance?.BaseFuelPricePerLiter ?? Constants.Fuel.FUEL_PRICE_PER_LITER;

            if (Core.Instance?.EnableDynamicPricing == true)
            {
                float timeModifier = 0f;

                try
                {
                    if (NetworkSingleton<TimeManager>.InstanceExists)
                    {
                        int dayIndex = NetworkSingleton<TimeManager>.Instance.DayIndex;
                        int hashCode = ("Petrol" + dayIndex.ToString()).GetHashCode();
                        timeModifier = Mathf.Lerp(0f, 0.2f, Mathf.InverseLerp(-2.1474836E+09f, 2.1474836E+09f, (float)hashCode));
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Debug($"Error calculating dynamic price modifier: {ex.Message}");
                }

                float tierMultiplier = GetTierMultiplier();

                float adjustedPrice = Core.Instance.EnablePricingOnTier
                    ? basePrice + (basePrice * timeModifier) * tierMultiplier
                    : basePrice + (basePrice * timeModifier);

                basePrice = ApplyCurfewTax(adjustedPrice);
            }

            UpdateFuelTypePriceCache(basePrice);
            pricePerLiter = GetSelectedFuelPrice();
            
            // Invalidate cache when prices change
            _lastPriceUpdateTime = 0f;
        }

        private float GetTierMultiplier()
        {
            try
            {
                if (!NetworkSingleton<LevelManager>.InstanceExists)
                {
                    return 1f;
                }

                switch (NetworkSingleton<LevelManager>.Instance.Rank)
                {
                    case ERank.Hoodlum:
                        return 1.05f;
                    case ERank.Peddler:
                        return 1.1f;
                    case ERank.Hustler:
                        return 1.15f;
                    case ERank.Bagman:
                        return 1.2f;
                    case ERank.Enforcer:
                        return 1.25f;
                    case ERank.Shot_Caller:
                        return 1.3f;
                    case ERank.Block_Boss:
                        return 1.4f;
                    case ERank.Underlord:
                        return 1.5f;
                    case ERank.Baron:
                        return 1.6f;
                    case ERank.Kingpin:
                        return 1.8f;
                    default:
                        return 1f;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error retrieving tier multiplier: {ex.Message}");
                return 1f;
            }
        }

        private float ApplyCurfewTax(float price)
        {
            if (Core.Instance?.EnableCurfewFuelTax != true)
            {
                return price;
            }

            try
            {
#if MONO
                bool curfewActive = NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance.IsCurrentTimeWithinRange(CurfewManager.CURFEW_START_TIME, CurfewManager.CURFEW_END_TIME);
#else
                bool curfewActive = NetworkSingleton<Il2CppScheduleOne.GameTime.TimeManager>.Instance.IsCurrentTimeWithinRange(CurfewManager.CURFEW_START_TIME, CurfewManager.CURFEW_END_TIME);
#endif
                if (curfewActive)
                {
                    return price * 2f;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error applying curfew tax: {ex.Message}");
            }

            return price;
        }

        private void UpdateFuelTypePriceCache(float basePrice)
        {
            _fuelTypePrices.Clear();

            if (FuelTypeManager.Instance == null)
            {
                _fuelTypePrices[FuelTypeId.Regular] = basePrice;
                _fuelTypePrices[FuelTypeId.Premium] = basePrice;
                _fuelTypePrices[FuelTypeId.Diesel] = basePrice;
                return;
            }

            Array fuelValues = Enum.GetValues(typeof(FuelTypeId));
            for (int i = 0; i < fuelValues.Length; i++)
            {
                FuelTypeId fuelTypeId = (FuelTypeId)fuelValues.GetValue(i);
                float price = basePrice;

                var fuelType = FuelTypeManager.Instance.GetFuelType(fuelTypeId);
                if (fuelType != null)
                {
                    price = basePrice * Mathf.Max(0.01f, fuelType.PriceMultiplier);
                }

                _fuelTypePrices[fuelTypeId] = price;
            }

            // Update fuel signs when prices change
            UpdateFuelSigns();
        }

        /// <summary>
        /// Update all fuel signs with current prices immediately
        /// </summary>
        private void UpdateFuelSigns()
        {
            try
            {
                if (FuelSignManager.Instance != null)
                {
                    // Use ForceUpdateAllSigns for immediate updates when prices change
                    FuelSignManager.Instance.ForceUpdateAllSigns();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error updating fuel signs: {ex.Message}");
            }
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
