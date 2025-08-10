using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if MONO
using ScheduleOne.Vehicles;
using ScheduleOne.DevUtilities;
#else
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.DevUtilities;
using MelonLoader;
#endif
using S1FuelMod.Utils;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Attributes;

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
        private bool _lowFuelWarningShown = false;
        private bool _criticalFuelWarningShown = false;
        private float _lastConsumptionTime = 0f;
        private VehicleType _vehicleType = VehicleType.Other;

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
        public string VehicleGUID => _vehicleGUID;

        /// <summary>
        /// Get the network ID for this vehicle (consistent across all clients)
        /// </summary>
        public string NetworkID => _landVehicle?.NetworkObject?.ObjectId.ToString() ?? _vehicleGUID;

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
                    if (_landVehicle.onVehicleStart != null)
                        _landVehicle.onVehicleStart.AddListener(new System.Action(OnVehicleStarted));
                    if (_landVehicle.onVehicleStop != null)
                        _landVehicle.onVehicleStop.AddListener(new System.Action(OnVehicleStopped));
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
        private float SetMaxFuelCapacity()
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
                VehicleType.Other => Core.Instance.DefaultFuelCapacity,
                _ => Core.Instance.DefaultFuelCapacity // Fallback for null or unknown types
            };
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
                VehicleType.Veeper => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.0f, // Standard consumption
                VehicleType.Bruiser => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.15f, // Heavy, thirsty vehicle
                VehicleType.Dinkler => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.5f, // Heavy truck, thirsty vehicle
                VehicleType.Hounddog => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.15f, // Performance vehicle, higher consumption
                VehicleType.Cheetah => Constants.Fuel.BASE_CONSUMPTION_RATE * 1.3f, // High-performance sports car, high consumption
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
                ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... engine state changed: {_isEngineRunning} " +
                                   $"(occupied: {_landVehicle.isOccupied}, throttle: {_landVehicle.currentThrottle:F2}, speed: {_landVehicle.speed_Kmh:F1})");
            }
        }

        /// <summary>
        /// Calculate and apply fuel consumption based on vehicle state
        /// </summary>
        private void UpdateFuelConsumption()
        {
            if (_landVehicle == null || currentFuelLevel <= 0f) return;

            float deltaTime = Time.time - _lastConsumptionTime;
            _lastConsumptionTime = Time.time;

            // Calculate consumption rate based on throttle and speed
            float throttleInput = Math.Abs(_landVehicle.currentThrottle);
            float consumptionRate = 0f;

            if (throttleInput > 0.01f)
            {
                // Active driving - scale consumption with throttle input
                consumptionRate = Mathf.Lerp(idleConsumptionRate, baseFuelConsumptionRate, throttleInput);
                
                // Additional consumption for high speeds
                if (_landVehicle.speed_Kmh > 50f)
                {
                    float speedMultiplier = 1f + ((_landVehicle.speed_Kmh - 50f) / 100f);
                    consumptionRate *= speedMultiplier;
                }
            }
            else if (_landVehicle.isOccupied)
            {
                // Idling when occupied
                consumptionRate = idleConsumptionRate;
            }

            // Apply consumption (convert from per-hour to per-second)
            if (consumptionRate > 0f)
            {
                float fuelConsumed = (consumptionRate / 3600f) * deltaTime;
                if (fuelConsumed > 0.001f) // Only consume if significant amount
                {
                    ConsumeFuel(fuelConsumed);
                }
            }
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
                default:
                    _vehicleType = VehicleType.Other;
                    break;
            }
        }

        private void OnDestroy()
        {
            try
            {
                // Unsubscribe from events
                if (_landVehicle != null)
                {
                    if (_landVehicle.onVehicleStart != null)
                        _landVehicle.onVehicleStart.RemoveListener(new System.Action(OnVehicleStarted));
                    if (_landVehicle.onVehicleStop != null)
                        _landVehicle.onVehicleStop.RemoveListener(new System.Action(OnVehicleStopped));
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
        Other
    }
}
