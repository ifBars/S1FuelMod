using UnityEngine;
using UnityEngine.Events;
using MelonLoader;
#if MONO
using ScheduleOne.Vehicles;
using ScheduleOne.DevUtilities;
#else
using Il2CppScheduleOne.Vehicles;
using Il2CppInterop.Runtime.Attributes;
#endif
using S1FuelMod.Systems.FuelTypes;
using S1FuelMod.Utils;


namespace S1FuelMod.Systems
{
    /// <summary>
    /// Component that manages fuel for a single LandVehicle
    /// </summary>
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class VehicleFuelSystem : MonoBehaviour
    {
        // Fuel Settings
        private float currentFuelLevel = 50f;
        private float maxFuelCapacity = 50f;
        private float globalMaxFuelCapacity = 50f;
        private float baseFuelConsumptionRate = 6f; // liters per hour at full throttle
        private float idleConsumptionRate = 0.5f; // liters per hour when idling

        // Warning Settings
        private float lowFuelThreshold = 30f; // percentage
        private float criticalFuelThreshold = 5f; // percentage

        // Component references
        private LandVehicle? _landVehicle;
        private string _vehicleGUID = string.Empty;

        // State tracking
        private bool _isEngineRunning = false;
        private bool _isInVehicle = false; // Track if player is in the vehicle
        private bool _lowFuelWarningShown = false;
        private bool _criticalFuelWarningShown = false;
        private float _lastConsumptionTime = 0f;
        private VehicleType _vehicleType = VehicleType.Other;
        private FuelTypeId _currentFuelType = FuelTypeId.Regular;
        private float _fuelQuality = 1.0f;

        // Events for UI updates
        public UnityEvent<float> OnFuelLevelChanged = new UnityEvent<float>();
        public UnityEvent<float> OnFuelPercentageChanged = new UnityEvent<float>();
        public UnityEvent<bool> OnLowFuelWarning = new UnityEvent<bool>();
        public UnityEvent<bool> OnCriticalFuelWarning = new UnityEvent<bool>();
        public UnityEvent<bool> OnFuelEmpty = new UnityEvent<bool>();

        // Public properties
        public float CurrentFuelLevel => currentFuelLevel;
        public float MaxFuelCapacity => maxFuelCapacity;
        public float GlobalMaxFuelCapacity => globalMaxFuelCapacity;
        public float FuelPercentage => maxFuelCapacity > 0 ? (currentFuelLevel / maxFuelCapacity) * 100f : 0f;
        public bool IsOutOfFuel => currentFuelLevel <= Constants.Fuel.ENGINE_CUTOFF_FUEL_LEVEL;
        public bool IsLowFuel => FuelPercentage <= lowFuelThreshold;
        public bool IsCriticalFuel => FuelPercentage <= criticalFuelThreshold;
        public bool IsEngineRunning => _isEngineRunning;
        public bool IsInVehicle => _isInVehicle;
        public string VehicleGUID => _vehicleGUID;

        /// <summary>
        /// Get the network ID for this vehicle (consistent across all clients)
        /// </summary>
        public string NetworkID => _landVehicle?.NetworkObject?.ObjectId.ToString() ?? _vehicleGUID;

        public FuelTypeId CurrentFuelType => _currentFuelType;
        public float FuelQuality => _fuelQuality;
        public VehicleType VehicleType => _vehicleType;

        public FuelTypeId GetRecommendedFuelType()
        {
            return FuelTypeManager.Instance?.GetRecommendedFuelType(_vehicleType) ?? FuelTypeId.Regular;
        }

        public bool IsFuelCompatible(FuelTypeId fuelTypeId)
        {
            return FuelTypeManager.Instance?.IsFuelCompatible(fuelTypeId, _vehicleType) ?? true;
        }

        public string GetCurrentFuelDisplayName()
        {
            return FuelTypeManager.Instance?.GetFuelDisplayName(_currentFuelType) ?? "Regular";
        }

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public VehicleFuelSystem(IntPtr ptr) : base(ptr) { }
#endif

        private void Awake()
        {
            try
            {
                // Get LandVehicle component
                _landVehicle = GetComponent<LandVehicle>();
                if (_landVehicle == null)
                {
                    ModLogger.Error($"VehicleFuelSystem: LandVehicle component not found on {gameObject.name}");
                    enabled = false;
                    return;
                }

                // Get vehicle GUID
                _vehicleGUID = _landVehicle.GUID.ToString();

                // Initialize with mod preferences
                if (Core.Instance != null)
                {
                    SetVehicleType();
                    InitializeFuelTypeState();
                    baseFuelConsumptionRate = SetBaseFuelConsumption();
                    globalMaxFuelCapacity = Core.Instance.DefaultFuelCapacity;
                    maxFuelCapacity = SetMaxFuelCapacity();
                    currentFuelLevel = maxFuelCapacity; // Start with full tank for testing
                    idleConsumptionRate = Constants.Fuel.IDLE_CONSUMPTION_RATE * Core.Instance.FuelConsumptionMultiplier;
                }

                ModLogger.FuelDebug($"VehicleFuelSystem initialized for {_landVehicle.VehicleName} ({_vehicleGUID.Substring(0, 8)}...)");
                ModLogger.LogVehicleFuel(_landVehicle.VehicleCode, _vehicleGUID, currentFuelLevel, maxFuelCapacity);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleFuelSystem.Awake", ex);
            }
        }

        private void Start()
        {
            try
            {
                // Subscribe to vehicle events if available
                if (_landVehicle != null)
                {
#if !MONO
                    if (_landVehicle.onVehicleStart != null)
                        _landVehicle.onVehicleStart.AddListener(new Action(OnVehicleStarted));
                    if (_landVehicle.onVehicleStop != null)
                        _landVehicle.onVehicleStop.AddListener(new Action(OnVehicleStopped));
#else
                    if (_landVehicle.onVehicleStart != null)
                        _landVehicle.onVehicleStart.AddListener(OnVehicleStarted);
                    if (_landVehicle.onVehicleStop != null)
                        _landVehicle.onVehicleStop.AddListener(OnVehicleStopped);
#endif
                }

                // Initialize time tracking
                _lastConsumptionTime = Time.time;

                // Trigger initial UI update
                TriggerFuelLevelChanged();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleFuelSystem.Start", ex);
            }
        }

        private void Update()
        {
            try
            {
                if (!_landVehicle.localPlayerIsDriver)
                    return;

                // Update engine running state
                UpdateEngineState();

                // Calculate and apply fuel consumption
                if (_isEngineRunning)
                {
                    UpdateFuelConsumption();
                }

                // Check for warnings
                CheckFuelWarnings();

                // Handle out of fuel condition
                if (IsOutOfFuel && _isEngineRunning)
                {
                    HandleOutOfFuel();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleFuelSystem.Update", ex);
            }
        }

        /// <summary>
        /// Set maximum fuel capacity based on vehicle type
        /// </summary>
        public float SetMaxFuelCapacity()
        {
            //float newMaxCapacity = _landVehicle.vehicleName switch
            //{
            //    "Shitbox" => Core.Instance.ShitboxFuelCapacity,
            //    "Veeper" => Core.Instance.VeeperFuelCapacity,
            //    "Bruiser" => Core.Instance.BruiserFuelCapacity,
            //    "Dinkler" => Core.Instance.DinklerFuelCapacity,
            //    "Hounddog" => Core.Instance.HounddogFuelCapacity,
            //    "Cheetah" => Core.Instance.CheetahFuelCapacity,
            //    _ => 16f // Default for unknown vehicles
            //};
            float newMaxCapacity = _vehicleType switch
            {
                VehicleType.Shitbox => Core.Instance.ShitboxFuelCapacity,
                VehicleType.Veeper => Core.Instance.VeeperFuelCapacity,
                VehicleType.Bruiser => Core.Instance.BruiserFuelCapacity,
                VehicleType.Dinkler => Core.Instance.DinklerFuelCapacity,
                VehicleType.Hounddog => Core.Instance.HounddogFuelCapacity,
                VehicleType.Cheetah => Core.Instance.CheetahFuelCapacity,
                VehicleType.Hotbox => Core.Instance.HotboxFuelCapacity,
                VehicleType.BugattiTourbillon => Core.Instance.BugattiTourbillonFuelCapacity,
                VehicleType.CanOfSoupCar => Core.Instance.CanofsoupcarFuelCapacity,
                VehicleType.CyberTruck => Core.Instance.CyberTruckFuelCapacity,
                VehicleType.Demon => Core.Instance.DemonFuelCapacity,
                VehicleType.Driftcar => Core.Instance.DriftcarFuelCapacity,
                VehicleType.GTR_R34 => Core.Instance.GtrR34FuelCapacity,
                VehicleType.GTR_R35 => Core.Instance.GtrR35FuelCapacity,
                VehicleType.LamborghiniVeneno => Core.Instance.LamborghiniVenenoFuelCapacity,
                VehicleType.RollsRoyceGhost => Core.Instance.RollsRoyceGhostFuelCapacity,
                VehicleType.Supercar => Core.Instance.SupercarFuelCapacity,
                VehicleType.KoenigseggCC850 => Core.Instance.KoenigseggCc850FuelCapacity,
                VehicleType.Other => Core.Instance.DefaultFuelCapacity,
                _ => Core.Instance.DefaultFuelCapacity // Fallback for null or unknown types
            };
            maxFuelCapacity = newMaxCapacity;
            return newMaxCapacity;
        }

        /// <summary>
        /// Set base fuel consumption rate based on vehicle type
        /// </summary>
        private float SetBaseFuelConsumption()
        {
            float baseConsumption = _vehicleType switch
            {
                VehicleType.Shitbox => Constants.Fuel.BASE_CONSUMPTION_RATE * 0.8f, // Efficient small engine
                VehicleType.Veeper => Constants.Fuel.BASE_CONSUMPTION_RATE, // Standard consumption
                VehicleType.Bruiser => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.15f, // Heavy, thirsty vehicle
                VehicleType.Dinkler => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.2f, // Heavy truck, thirsty vehicle
                VehicleType.Hounddog => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.05f, // Performance vehicle, higher consumption
                VehicleType.Cheetah => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.05f, // High-performance sports car, high consumption
                VehicleType.Hotbox => Constants.Fuel.BASE_CONSUMPTION_RATE, // Hybrid SUV, moderate consumption
                VehicleType.Other => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.0f, // Default consumption
                _ => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.0f // Fallback for null or unknown types
            };
            
            // Apply the global fuel consumption multiplier
            return baseConsumption * Core.Instance.FuelConsumptionMultiplier;
        }

        /// <summary>
        /// Update the engine running state based on vehicle conditions
        /// </summary>
        private void UpdateEngineState()
        {
            if (_landVehicle == null) return;

            // Engine should be running if vehicle is occupied (player is in it)
            bool shouldBeRunning = _landVehicle.isOccupied;

            if (shouldBeRunning != _isEngineRunning)
            {
                _isEngineRunning = shouldBeRunning;
                
                // Reset consumption timer when engine starts to prevent parasitic fuel drain
                if (_isEngineRunning)
                {
                    _lastConsumptionTime = Time.time;
                    ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... engine started, consumption timer reset");
                }
                
                ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... engine state changed: {_isEngineRunning} " +
                                   $"(occupied: {_landVehicle.isOccupied}, throttle: {_landVehicle.currentThrottle:F2}, speed: {_landVehicle.speed_Kmh:F1})");
            }
        }

        /// <summary>
        /// Calculate and apply fuel consumption based on vehicle state
        /// </summary>
        private void UpdateFuelConsumption()
        {
            if (_landVehicle == null || currentFuelLevel <= 0f || !_isEngineRunning)
                return;

            float deltaTime = Time.deltaTime;
            float throttleInput = _landVehicle.currentThrottle;

            float baseConsumption = CalculateBaseConsumption(throttleInput);
            if (baseConsumption <= 0f)
            {
                _lastConsumptionTime = Time.time;
                return;
            }

            float speedMultiplier = CalculateAdvancedSpeedMultiplier(_landVehicle.speed_Kmh);
            float fuelTypeModifier = GetCurrentFuelEfficiencyModifier();

            float finalConsumption = baseConsumption * speedMultiplier * fuelTypeModifier;
            if (finalConsumption <= 0f)
            {
                _lastConsumptionTime = Time.time;
                return;
            }

            float fuelConsumed = (finalConsumption / 3600f) * deltaTime;
            if (fuelConsumed > 0.0005f && _landVehicle.isOccupied)
            {
                ConsumeFuel(fuelConsumed);
            }

            _lastConsumptionTime = Time.time;
        }

        private float CalculateBaseConsumption(float throttleInput)
        {
            float absoluteThrottle = Mathf.Clamp01(Mathf.Abs(throttleInput));

            if (absoluteThrottle > 0.01f)
            {
                return Mathf.Lerp(idleConsumptionRate, baseFuelConsumptionRate, absoluteThrottle);
            }

            if (_landVehicle != null && _landVehicle.isOccupied)
            {
                return idleConsumptionRate;
            }

            return 0f;
        }

        private float CalculateAdvancedSpeedMultiplier(float speedKmh)
        {
            if (speedKmh <= 20f)
                return 0.8f;

            if (speedKmh <= 40f)
                return 1.0f;

            if (speedKmh <= 60f)
                return 1.0f + ((speedKmh - 40f) * 0.025f);

            if (speedKmh <= 80f)
                return 1.5f + ((speedKmh - 60f) * 0.05f);

            float excessSpeed = speedKmh - 80f;
            return 2.5f + (excessSpeed * excessSpeed) * 0.002f;
        }

        private float GetCurrentFuelEfficiencyModifier()
        {
            if (_landVehicle == null)
            {
                return 1.0f;
            }

            float modifier = 1.0f;

            if (FuelTypeManager.Instance != null)
            {
                modifier = FuelTypeManager.Instance.GetFuelEfficiency(
                    _currentFuelType,
                    _vehicleType,
                    _landVehicle.speed_Kmh,
                    _landVehicle.currentThrottle);
            }

            float qualityPenalty = Mathf.Lerp(1.0f, 1.25f, 1.0f - Mathf.Clamp01(_fuelQuality));
            return modifier * qualityPenalty;
        }

        /// <summary>
        /// Check and trigger fuel warnings
        /// </summary>
        private void CheckFuelWarnings()
        {
            float fuelPercent = FuelPercentage;

            // Critical fuel warning
            if (fuelPercent <= criticalFuelThreshold && !_criticalFuelWarningShown)
            {
                _criticalFuelWarningShown = true;
                _lowFuelWarningShown = true; // Also set low fuel warning
                OnCriticalFuelWarning.Invoke(true);
                ModLogger.LogFuelWarning(_vehicleGUID, currentFuelLevel, "CRITICAL");
            }
            else if (fuelPercent > criticalFuelThreshold && _criticalFuelWarningShown)
            {
                _criticalFuelWarningShown = false;
                OnCriticalFuelWarning.Invoke(false);
            }

            // Low fuel warning
            if (fuelPercent <= lowFuelThreshold && !_lowFuelWarningShown && !_criticalFuelWarningShown)
            {
                _lowFuelWarningShown = true;
                OnLowFuelWarning.Invoke(true);
                ModLogger.LogFuelWarning(_vehicleGUID, currentFuelLevel, "LOW");
            }
            else if (fuelPercent > lowFuelThreshold && _lowFuelWarningShown && !_criticalFuelWarningShown)
            {
                _lowFuelWarningShown = false;
                OnLowFuelWarning.Invoke(false);
            }
        }

        /// <summary>
        /// Handle out of fuel condition
        /// </summary>
        private void HandleOutOfFuel()
        {
            if (_landVehicle == null) return;

            ModLogger.LogFuelWarning(_vehicleGUID, 0f, "OUT OF FUEL");
            OnFuelEmpty.Invoke(true);

            // Engine effects will be handled by the Harmony patch
            // that modifies the ApplyThrottle method
        }

        /// <summary>
        /// Consume fuel from the tank
        /// </summary>
        /// <param name="amount">Amount of fuel to consume in liters</param>
        public void ConsumeFuel(float amount)
        {
            if (amount <= 0f) return;

            float oldFuelLevel = currentFuelLevel;
            currentFuelLevel = Math.Max(0f, currentFuelLevel - amount);

            if (Math.Abs(oldFuelLevel - currentFuelLevel) > 0.001f)
            {
                ModLogger.LogFuelConsumption(_vehicleGUID, amount, currentFuelLevel);
                ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... fuel consumed: {amount:F3}L, " +
                                   $"new level: {currentFuelLevel:F2}L ({FuelPercentage:F1}%)");
                TriggerFuelLevelChanged();
            }
        }

        /// <summary>
        /// Add fuel to the tank
        /// </summary>
        /// <param name="amount">Amount of fuel to add in liters</param>
        /// <returns>Amount actually added</returns>
        public float AddFuel(float amount)
        {
            if (amount <= 0f) return 0f;

            float oldFuelLevel = currentFuelLevel;
            currentFuelLevel = Math.Min(maxFuelCapacity, currentFuelLevel + amount);
            float actualAdded = currentFuelLevel - oldFuelLevel;

            if (actualAdded > 0f)
            {
                ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... refueled: +{actualAdded:F1}L");
                TriggerFuelLevelChanged();

                // Reset warnings if fuel is above thresholds
                if (FuelPercentage > lowFuelThreshold)
                {
                    _lowFuelWarningShown = false;
                    _criticalFuelWarningShown = false;
                    OnLowFuelWarning.Invoke(false);
                    OnCriticalFuelWarning.Invoke(false);
                    OnFuelEmpty.Invoke(false);
                }
            }

            return actualAdded;
        }

        public float FullTank()
        {
            // Fill the tank to maximum capacity
            float oldFuelLevel = currentFuelLevel;
            currentFuelLevel = maxFuelCapacity;
            if (Math.Abs(oldFuelLevel - currentFuelLevel) > 0.001f)
            {
                ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... tank filled to {currentFuelLevel:F1}L");
                TriggerFuelLevelChanged();
            }
            return currentFuelLevel;
        }

        /// <summary>
        /// Set fuel level directly
        /// </summary>
        /// <param name="level">New fuel level in liters</param>
        public void SetFuelLevel(float level)
        {
            float oldLevel = currentFuelLevel;
            currentFuelLevel = Math.Clamp(level, 0f, maxFuelCapacity);

            if (Math.Abs(oldLevel - currentFuelLevel) > 0.001f)
            {
                ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... fuel level set to {currentFuelLevel:F1}L");
                TriggerFuelLevelChanged();
            }
        }

        /// <summary>
        /// Set maximum fuel capacity
        /// </summary>
        /// <param name="capacity">New maximum capacity in liters</param>
        public void SetMaxCapacity(float capacity)
        {
            maxFuelCapacity = Math.Max(1f, capacity);
            currentFuelLevel = Math.Min(currentFuelLevel, maxFuelCapacity);
            TriggerFuelLevelChanged();
        }

        /// <summary>
        /// Trigger fuel level changed events
        /// </summary>
        private void TriggerFuelLevelChanged()
        {
            OnFuelLevelChanged.Invoke(currentFuelLevel);
            OnFuelPercentageChanged.Invoke(FuelPercentage);
        }

        /// <summary>
        /// Vehicle started event handler
        /// </summary>
        private void OnVehicleStarted()
        {
            ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... started");
        }

        /// <summary>
        /// Vehicle stopped event handler
        /// </summary>
        private void OnVehicleStopped()
        {
            _isEngineRunning = false;
            ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... stopped");
        }

        /// <summary>
        /// Get fuel data for saving
        /// </summary>
#if !MONO
        [HideFromIl2Cpp]
#endif
        public FuelData GetFuelData()
        {
            return new FuelData
            {
                CurrentFuelLevel = currentFuelLevel,
                MaxFuelCapacity = maxFuelCapacity,
                FuelConsumptionRate = baseFuelConsumptionRate
            };
        }

        /// <summary>
        /// Load fuel data from save
        /// </summary>
#if !MONO
        [HideFromIl2Cpp]
#endif
        public void LoadFuelData(FuelData data)
        {
            maxFuelCapacity = data.MaxFuelCapacity;
            currentFuelLevel = data.CurrentFuelLevel;
            baseFuelConsumptionRate = data.FuelConsumptionRate;
            InitializeFuelTypeState();

            TriggerFuelLevelChanged();
            ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... fuel data loaded");
        }

        /// <summary>
        /// Get fuel data for saving (IL2CPP compatible version)
        /// </summary>
        /// <param name="currentLevel">Output: current fuel level</param>
        /// <param name="maxCapacity">Output: maximum fuel capacity</param>
        /// <param name="consumptionRate">Output: fuel consumption rate</param>
        public void GetFuelDataValues(out float currentLevel)
        {
            currentLevel = currentFuelLevel;
        }

        /// <summary>
        /// Load fuel data from save (IL2CPP compatible version)
        /// </summary>
        /// <param name="currentLevel">Current fuel level</param>
        /// <param name="maxCapacity">Maximum fuel capacity</param>
        /// <param name="consumptionRate">Fuel consumption rate</param>
        public void LoadFuelDataValues(float currentLevel)
        {
            currentFuelLevel = currentLevel;
            SetBaseFuelConsumption();
            maxFuelCapacity = SetMaxFuelCapacity();
            InitializeFuelTypeState();

            TriggerFuelLevelChanged();
            ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... fuel data loaded");
        }

        private void SetVehicleType()
        {
            switch (_landVehicle.vehicleName)
            {
                case "Shitbox":
                    _vehicleType = VehicleType.Shitbox;
                    break;
                case "Veeper":
                    _vehicleType = VehicleType.Veeper;
                    break;
                case "Bruiser":
                    _vehicleType = VehicleType.Bruiser;
                    break;
                case "Dinkler":
                    _vehicleType = VehicleType.Dinkler;
                    break;
                case "Hounddog":
                    _vehicleType = VehicleType.Hounddog;
                    break;
                case "Cheetah":
                    _vehicleType = VehicleType.Cheetah;
                    break;
                case "Hotbox":
                    _vehicleType = VehicleType.Hotbox;
                    break;
                case "Bugatti_Tourbillon":
                    _vehicleType = VehicleType.BugattiTourbillon;
                    break;
                case "canofsoupcar":
                    _vehicleType = VehicleType.CanOfSoupCar;
                    break;
                case "Cyber_Truck":
                    _vehicleType = VehicleType.CyberTruck;
                    break;
                case "Demon":
                    _vehicleType = VehicleType.Demon;
                    break;
                case "driftcar":
                    _vehicleType = VehicleType.Driftcar;
                    break;
                case "GTR_R34":
                    _vehicleType = VehicleType.GTR_R34;
                    break;
                case "GTR_R35":
                    _vehicleType = VehicleType.GTR_R35;
                    break;
                case "Lamborghini_Veneno":
                    _vehicleType = VehicleType.LamborghiniVeneno;
                    break;
                case "Rolls_Royce_Ghost":
                    _vehicleType = VehicleType.RollsRoyceGhost;
                    break;
                case "supercar":
                    _vehicleType = VehicleType.Supercar;
                    break;
                case "Koenigsegg_CC850":
                    _vehicleType = VehicleType.KoenigseggCC850;
                    break;
                default:
                    _vehicleType = VehicleType.Other;
                    break;
            }
        }

        private void InitializeFuelTypeState()
        {
            _fuelQuality = 1.0f;

            if (FuelTypeManager.Instance != null)
            {
                _currentFuelType = FuelTypeManager.Instance.GetRecommendedFuelType(_vehicleType);
            }
            else
            {
                _currentFuelType = FuelTypeId.Regular;
            }
        }

        public bool ChangeFuelType(FuelTypeId newFuelType, float refuelAmount)
        {
            try
            {
                if (!IsFuelCompatible(newFuelType))
                {
                    ModLogger.Warning($"Fuel type {newFuelType} is not compatible with {_vehicleType}");
                    return false;
                }

                if (_currentFuelType != newFuelType && CurrentFuelLevel > 0.1f)
                {
                    CalculateFuelMixingEffect(newFuelType, refuelAmount);
                }
                else
                {
                    _currentFuelType = newFuelType;
                    _fuelQuality = 1.0f;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error changing fuel type", ex);
                return false;
            }
        }

        private void CalculateFuelMixingEffect(FuelTypeId newFuelType, float refuelAmount)
        {
            try
            {
                float totalFuel = Mathf.Max(0.01f, CurrentFuelLevel + Mathf.Max(0f, refuelAmount));
                float oldRatio = Mathf.Clamp01(CurrentFuelLevel / totalFuel);
                float newRatio = Mathf.Clamp01(Mathf.Max(0f, refuelAmount) / totalFuel);

                if (GetFuelCompatibilityScore(_currentFuelType, newFuelType) < 0.8f)
                {
                    _fuelQuality = Mathf.Max(0.7f, _fuelQuality * 0.9f);
                }
                else
                {
                    // Blend quality towards the better of the two fuels when compatible
                    _fuelQuality = Mathf.Clamp01(Mathf.Lerp(_fuelQuality, 1.0f, newRatio));
                }

                if (newRatio > 0.6f || oldRatio < 0.2f)
                {
                    _currentFuelType = newFuelType;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error calculating fuel mixing effect", ex);
            }
        }

        private float GetFuelCompatibilityScore(FuelTypeId fuel1, FuelTypeId fuel2)
        {
            if (fuel1 == fuel2)
                return 1.0f;

            if (fuel1 == FuelTypeId.Regular || fuel2 == FuelTypeId.Regular)
                return 0.8f;

            return 0.6f;
        }

        private void OnDestroy()
        {
            try
            {
                // Unsubscribe from events
                if (_landVehicle != null)
                {
#if !MONO
                    if (_landVehicle.onVehicleStart != null)
                        _landVehicle.onVehicleStart.RemoveListener(new System.Action(OnVehicleStarted));
                    if (_landVehicle.onVehicleStop != null)
                        _landVehicle.onVehicleStop.RemoveListener(new System.Action(OnVehicleStopped));
#else
                    if (_landVehicle.onVehicleStart != null)
                        _landVehicle.onVehicleStart.RemoveListener(OnVehicleStarted);
                    if (_landVehicle.onVehicleStop != null)
                        _landVehicle.onVehicleStop.RemoveListener(OnVehicleStopped);
#endif
                }

                ModLogger.FuelDebug($"VehicleFuelSystem destroyed for vehicle {_vehicleGUID.Substring(0, 8)}...");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleFuelSystem.OnDestroy", ex);
            }
        }
    }

    /// <summary>
    /// Data structure for fuel system save/load
    /// </summary>
    [Serializable]
    public class FuelData
    {
        public float CurrentFuelLevel;
        public float MaxFuelCapacity;
        public float FuelConsumptionRate;
    }

    public enum VehicleType
    {
        Shitbox,
        Veeper,
        Bruiser,
        Dinkler,
        Hounddog,
        Cheetah,
        Hotbox,
        BugattiTourbillon,
        CanOfSoupCar,
        CyberTruck,
        Demon,
        Driftcar,
        GTR_R34,
        GTR_R35,
        LamborghiniVeneno,
        RollsRoyceGhost,
        Supercar,
        KoenigseggCC850,
        Other
    }
}
