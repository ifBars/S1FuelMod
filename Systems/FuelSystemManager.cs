using UnityEngine;
using ScheduleOne.Vehicles;
using ScheduleOne.DevUtilities;
using S1FuelMod.Utils;
using S1FuelMod.Networking;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Manages fuel systems for all vehicles in the game
    /// </summary>
    public class FuelSystemManager : IDisposable
    {
        private readonly Dictionary<string, VehicleFuelSystem> _vehicleFuelSystems = new Dictionary<string, VehicleFuelSystem>();
        private bool _debugInfoEnabled = false;
        private float _lastDebugLogTime = 0f;
        private const float DEBUG_LOG_INTERVAL = 5f; // seconds
        private readonly FuelNetworkManager _network = new FuelNetworkManager();

        public FuelSystemManager()
        {
            ModLogger.Info("FuelSystemManager: Initializing...");
            
            // Find and setup existing vehicles
            InitializeExistingVehicles();
            
            ModLogger.Info($"FuelSystemManager: Initialized with {_vehicleFuelSystems.Count} vehicles");

            // Networking
            _network.Initialize();
            foreach (var fs in _vehicleFuelSystems.Values)
            {
                _network.RegisterFuelSystem(fs);
            }
            
            ModLogger.Info($"FuelSystemManager: Registered {_vehicleFuelSystems.Count} fuel systems with network manager");
        }

        /// <summary>
        /// Update all fuel systems
        /// </summary>
        public void Update()
        {
            try
            {
                // Log debug information periodically
                if (_debugInfoEnabled && Time.time - _lastDebugLogTime > DEBUG_LOG_INTERVAL)
                {
                    LogDebugInfo();
                    _lastDebugLogTime = Time.time;
                }

                // Check for new vehicles that need fuel systems
                CheckForNewVehicles();

                // Pump networking
                _network.Update();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelSystemManager.Update", ex);
            }
        }

        /// <summary>
        /// Initialize fuel systems for existing vehicles in the scene
        /// </summary>
        private void InitializeExistingVehicles()
        {
            try
            {
                // Find all LandVehicle objects in the scene
                LandVehicle[] vehicles = UnityEngine.Object.FindObjectsOfType<LandVehicle>();
                
                foreach (LandVehicle vehicle in vehicles)
                {
                    if (vehicle != null)
                    {
                        AddFuelSystemToVehicle(vehicle);
                    }
                }

                ModLogger.Info($"FuelSystemManager: Found and initialized {vehicles.Length} existing vehicles");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error initializing existing vehicles", ex);
            }
        }

        /// <summary>
        /// Check for new vehicles that need fuel systems
        /// </summary>
        private void CheckForNewVehicles()
        {
            try
            {
                // Get all vehicles from VehicleManager if available
                if (NetworkSingleton<VehicleManager>.InstanceExists)
                {
                    var vehicleManager = NetworkSingleton<VehicleManager>.Instance;
                    
                    foreach (LandVehicle vehicle in vehicleManager.AllVehicles)
                    {
                        if (vehicle != null && !_vehicleFuelSystems.ContainsKey(vehicle.GUID.ToString()))
                        {
                            var fs = AddFuelSystemToVehicle(vehicle);
                            if (fs != null)
                            {
                                _network.RegisterFuelSystem(fs);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error checking for new vehicles", ex);
            }
        }

        /// <summary>
        /// Add a fuel system to a vehicle
        /// </summary>
        /// <param name="vehicle">The vehicle to add fuel system to</param>
        /// <returns>The created VehicleFuelSystem component</returns>
        public VehicleFuelSystem? AddFuelSystemToVehicle(LandVehicle vehicle)
        {
            try
            {
                if (vehicle == null)
                {
                    ModLogger.Warning("FuelSystemManager: Attempted to add fuel system to null vehicle");
                    return null;
                }

                string vehicleGUID = vehicle.GUID.ToString();

                // Check if vehicle already has a fuel system
                if (_vehicleFuelSystems.ContainsKey(vehicleGUID))
                {
                    ModLogger.Debug($"FuelSystemManager: Vehicle {vehicleGUID.Substring(0, 8)}... already has fuel system");
                    return _vehicleFuelSystems[vehicleGUID];
                }

                // Check if vehicle already has the component
                VehicleFuelSystem existingSystem = vehicle.GetComponent<VehicleFuelSystem>();
                if (existingSystem != null)
                {
                    _vehicleFuelSystems[vehicleGUID] = existingSystem;
                    ModLogger.Debug($"FuelSystemManager: Found existing fuel system for vehicle {vehicleGUID.Substring(0, 8)}...");
                    return existingSystem;
                }

                // Add fuel system component
                VehicleFuelSystem fuelSystem = vehicle.gameObject.AddComponent<VehicleFuelSystem>();
                _vehicleFuelSystems[vehicleGUID] = fuelSystem;
                _network.RegisterFuelSystem(fuelSystem);

                ModLogger.Info($"FuelSystemManager: Added fuel system to {vehicle.VehicleName} ({vehicleGUID.Substring(0, 8)}...)");
                return fuelSystem;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error adding fuel system to vehicle", ex);
                return null;
            }
        }

        /// <summary>
        /// Remove a fuel system from tracking
        /// </summary>
        /// <param name="vehicleGUID">GUID of the vehicle</param>
        public void RemoveFuelSystem(string vehicleGUID)
        {
            try
            {
                if (_vehicleFuelSystems.ContainsKey(vehicleGUID))
                {
                    var fs = _vehicleFuelSystems[vehicleGUID];
                    if (fs != null)
                    {
                        _network.UnregisterFuelSystem(fs);
                    }
                    _vehicleFuelSystems.Remove(vehicleGUID);
                    ModLogger.Debug($"FuelSystemManager: Removed fuel system for vehicle {vehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error removing fuel system", ex);
            }
        }

        /// <summary>
        /// Get fuel system for a specific vehicle
        /// </summary>
        /// <param name="vehicleGUID">GUID of the vehicle</param>
        /// <returns>VehicleFuelSystem or null if not found</returns>
        public VehicleFuelSystem? GetFuelSystem(string vehicleGUID)
        {
            return _vehicleFuelSystems.TryGetValue(vehicleGUID, out VehicleFuelSystem fuelSystem) ? fuelSystem : null;
        }

        /// <summary>
        /// Get fuel system for a specific vehicle
        /// </summary>
        /// <param name="vehicle">The vehicle</param>
        /// <returns>VehicleFuelSystem or null if not found</returns>
        public VehicleFuelSystem? GetFuelSystem(LandVehicle vehicle)
        {
            if (vehicle == null) return null;
            return GetFuelSystem(vehicle.GUID.ToString());
        }

        /// <summary>
        /// Get all active fuel systems
        /// </summary>
        /// <returns>Collection of all fuel systems</returns>
        public IReadOnlyCollection<VehicleFuelSystem> GetAllFuelSystems()
        {
            return _vehicleFuelSystems.Values;
        }

        /// <summary>
        /// Refill all vehicles to full capacity
        /// </summary>
        public void RefillAllVehicles()
        {
            try
            {
                int refilled = 0;
                foreach (var fuelSystem in _vehicleFuelSystems.Values)
                {
                    if (fuelSystem != null && fuelSystem.gameObject != null)
                    {
                        fuelSystem.SetFuelLevel(fuelSystem.MaxFuelCapacity);
                        refilled++;
                    }
                }

                ModLogger.Info($"FuelSystemManager: Refilled {refilled} vehicles");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error refilling all vehicles", ex);
            }
        }

        /// <summary>
        /// Drain fuel from all vehicles
        /// </summary>
        /// <param name="amount">Amount to drain in liters</param>
        public void DrainAllVehicles(float amount)
        {
            try
            {
                int drained = 0;
                foreach (var fuelSystem in _vehicleFuelSystems.Values)
                {
                    if (fuelSystem != null && fuelSystem.gameObject != null)
                    {
                        fuelSystem.ConsumeFuel(amount);
                        drained++;
                    }
                }

                ModLogger.Info($"FuelSystemManager: Drained {amount}L from {drained} vehicles");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error draining all vehicles", ex);
            }
        }

        /// <summary>
        /// Toggle debug information display
        /// </summary>
        public void ToggleDebugInfo()
        {
            _debugInfoEnabled = !_debugInfoEnabled;
            ModLogger.Info($"FuelSystemManager: Debug info {(_debugInfoEnabled ? "enabled" : "disabled")}");
            
            if (_debugInfoEnabled)
            {
                LogDebugInfo();
            }
        }

        /// <summary>
        /// Log debug information about all fuel systems
        /// </summary>
        private void LogDebugInfo()
        {
            try
            {
                ModLogger.Info($"=== Fuel System Debug Info ({_vehicleFuelSystems.Count} vehicles) ===");
                
                foreach (var kvp in _vehicleFuelSystems)
                {
                    var fuelSystem = kvp.Value;
                    if (fuelSystem != null && fuelSystem.gameObject != null)
                    {
                        var vehicle = fuelSystem.GetComponent<LandVehicle>();
                        string vehicleName = vehicle?.VehicleName ?? "Unknown";
                        string vehicleCode = vehicle?.VehicleCode ?? "unknown";
                        
                        ModLogger.Info($"  {vehicleName} ({vehicleCode}): {fuelSystem.CurrentFuelLevel:F1}L / {fuelSystem.MaxFuelCapacity:F1}L " +
                                     $"({fuelSystem.FuelPercentage:F1}%) - Engine: {fuelSystem.IsEngineRunning} - " +
                                     $"Warnings: {(fuelSystem.IsLowFuel ? "LOW" : "")} {(fuelSystem.IsCriticalFuel ? "CRITICAL" : "")} " +
                                     $"{(fuelSystem.IsOutOfFuel ? "EMPTY" : "")}");
                    }
                    else
                    {
                        ModLogger.Warning($"  Invalid fuel system found for vehicle {kvp.Key}");
                    }
                }
                
                ModLogger.Info("=== End Fuel System Debug Info ===");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error logging debug info", ex);
            }
        }

        /// <summary>
        /// Get fuel system statistics
        /// </summary>
        /// <returns>Statistics about fuel systems</returns>
        public FuelSystemStats GetStatistics()
        {
            var stats = new FuelSystemStats();
            
            try
            {
                stats.TotalVehicles = _vehicleFuelSystems.Count;
                
                foreach (var fuelSystem in _vehicleFuelSystems.Values)
                {
                    if (fuelSystem != null && fuelSystem.gameObject != null)
                    {
                        stats.ActiveVehicles++;
                        
                        if (fuelSystem.IsEngineRunning)
                            stats.RunningVehicles++;
                        
                        if (fuelSystem.IsOutOfFuel)
                            stats.EmptyVehicles++;
                        else if (fuelSystem.IsCriticalFuel)
                            stats.CriticalFuelVehicles++;
                        else if (fuelSystem.IsLowFuel)
                            stats.LowFuelVehicles++;
                        
                        stats.TotalFuelCapacity += fuelSystem.MaxFuelCapacity;
                        stats.TotalCurrentFuel += fuelSystem.CurrentFuelLevel;
                    }
                    else
                    {
                        stats.InactiveVehicles++;
                    }
                }
                
                stats.AverageFuelLevel = stats.ActiveVehicles > 0 ? stats.TotalCurrentFuel / stats.ActiveVehicles : 0f;
                stats.OverallFuelPercentage = stats.TotalFuelCapacity > 0 ? (stats.TotalCurrentFuel / stats.TotalFuelCapacity) * 100f : 0f;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error calculating fuel system statistics", ex);
            }
            
            return stats;
        }

        /// <summary>
        /// Dispose of the fuel system manager
        /// </summary>
        public void Dispose()
        {
            try
            {
                ModLogger.Info("FuelSystemManager: Disposing...");
                
                // Clean up tracking
                _vehicleFuelSystems.Clear();

                _network.Dispose();

                ModLogger.Info("FuelSystemManager: Disposed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error disposing FuelSystemManager", ex);
            }
        }
    }

    /// <summary>
    /// Statistics about fuel systems
    /// </summary>
    public class FuelSystemStats
    {
        public int TotalVehicles { get; set; }
        public int ActiveVehicles { get; set; }
        public int InactiveVehicles { get; set; }
        public int RunningVehicles { get; set; }
        public int EmptyVehicles { get; set; }
        public int CriticalFuelVehicles { get; set; }
        public int LowFuelVehicles { get; set; }
        public float TotalFuelCapacity { get; set; }
        public float TotalCurrentFuel { get; set; }
        public float AverageFuelLevel { get; set; }
        public float OverallFuelPercentage { get; set; }
    }
}
