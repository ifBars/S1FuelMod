using System;
using UnityEngine;
using UnityEngine.Events;
using ScheduleOne.Vehicles;
using ScheduleOne.DevUtilities;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Component that manages fuel for a single LandVehicle
    /// </summary>
    public class VehicleFuelSystem : MonoBehaviour
    {
        [Header("Fuel Settings")]
        [SerializeField] private float currentFuelLevel = 50f;
        [SerializeField] private float maxFuelCapacity = 50f;
        [SerializeField] private float baseFuelConsumptionRate = 6f; // liters per hour at full throttle
        [SerializeField] private float idleConsumptionRate = 0.5f; // liters per hour when idling

        [Header("Warning Settings")]
        [SerializeField] private float lowFuelThreshold = 20f; // percentage
        [SerializeField] private float criticalFuelThreshold = 5f; // percentage

        // Component references
        private LandVehicle? _landVehicle;
        private string _vehicleGUID = string.Empty;

        // State tracking
        private bool _isEngineRunning = false;
        private bool _lowFuelWarningShown = false;
        private bool _criticalFuelWarningShown = false;
        private float _lastConsumptionTime = 0f;

        // Events for UI updates
        public UnityEvent<float> OnFuelLevelChanged = new UnityEvent<float>();
        public UnityEvent<float> OnFuelPercentageChanged = new UnityEvent<float>();
        public UnityEvent<bool> OnLowFuelWarning = new UnityEvent<bool>();
        public UnityEvent<bool> OnCriticalFuelWarning = new UnityEvent<bool>();
        public UnityEvent<bool> OnFuelEmpty = new UnityEvent<bool>();

        // Public properties
        public float CurrentFuelLevel => currentFuelLevel;
        public float MaxFuelCapacity => maxFuelCapacity;
        public float FuelPercentage => maxFuelCapacity > 0 ? (currentFuelLevel / maxFuelCapacity) * 100f : 0f;
        public bool IsOutOfFuel => currentFuelLevel <= Constants.Fuel.ENGINE_CUTOFF_FUEL_LEVEL;
        public bool IsLowFuel => FuelPercentage <= lowFuelThreshold;
        public bool IsCriticalFuel => FuelPercentage <= criticalFuelThreshold;
        public bool IsEngineRunning => _isEngineRunning;
        public string VehicleGUID => _vehicleGUID;

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
                    maxFuelCapacity = Core.Instance.DefaultFuelCapacity;
                    currentFuelLevel = maxFuelCapacity; // Start with full tank for testing
                    baseFuelConsumptionRate = Constants.Fuel.BASE_CONSUMPTION_RATE * Core.Instance.FuelConsumptionMultiplier;
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
                    _landVehicle.onVehicleStart?.AddListener(OnVehicleStarted);
                    _landVehicle.onVehicleStop?.AddListener(OnVehicleStopped);
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
                // Skip update if fuel system is disabled or vehicle is not occupied by the local player
                if (!Core.Instance?.EnableFuelSystem == true || _landVehicle == null || !_landVehicle.localPlayerIsInVehicle)
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
        public void LoadFuelData(FuelData data)
        {
            maxFuelCapacity = data.MaxFuelCapacity;
            currentFuelLevel = data.CurrentFuelLevel;
            baseFuelConsumptionRate = data.FuelConsumptionRate;
            
            TriggerFuelLevelChanged();
            ModLogger.FuelDebug($"Vehicle {_vehicleGUID.Substring(0, 8)}... fuel data loaded");
        }

        private void OnDestroy()
        {
            try
            {
                // Unsubscribe from events
                if (_landVehicle != null)
                {
                    _landVehicle.onVehicleStart?.RemoveListener(OnVehicleStarted);
                    _landVehicle.onVehicleStop?.RemoveListener(OnVehicleStopped);
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
}
