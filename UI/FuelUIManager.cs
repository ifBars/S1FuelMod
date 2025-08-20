using UnityEngine.SceneManagement;
#if MONO
using ScheduleOne.Vehicles;
using ScheduleOne.PlayerScripts;
#else
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.PlayerScripts;
#endif
using S1FuelMod.Utils;
using S1FuelMod.Systems;
using UnityEngine.Events;

namespace S1FuelMod.UI
{
    /// <summary>
    /// Manages fuel-related UI elements
    /// </summary>
    public class FuelUIManager : IDisposable
    {
        private readonly Dictionary<string, FuelGaugeUI> _activeFuelGauges = new Dictionary<string, FuelGaugeUI>();
        private readonly Dictionary<string, FuelGauge> _activeNewGauges = new Dictionary<string, FuelGauge>();
        private Player? _localPlayer;
        private LandVehicle? _currentVehicle;
        private string _currentVehicleGUID = string.Empty;
        private FuelGaugeUI? _currentGauge;
        private FuelGauge? _currentNewGauge;

        public FuelUIManager()
        {
            ModLogger.UIDebug("FuelUIManager: Initializing...");

            // Subscribe to scene change events
            SceneManager.sceneLoaded += (UnityAction<Scene, LoadSceneMode>)OnSceneLoaded;
            SceneManager.sceneUnloaded += (UnityAction<Scene>)OnSceneUnloaded;

            // Find local player
            FindLocalPlayer();

            ModLogger.UIDebug("FuelUIManager: Initialized");
        }

        /// <summary>
        /// Update the fuel UI manager
        /// </summary>
        public void Update()
        {
            try
            {
                if (!Core.Instance?.ShowFuelGauge == true)
                    return;

                // Check if we're in the correct scene
                if (!IsInGameScene())
                    return;

                // Find local player if not found
                if (_localPlayer == null)
                {
                    FindLocalPlayer();
                    return;
                }

                // Check if player changed vehicles
                CheckVehicleChange();

                // Update current gauges if active
                _currentGauge?.Update();
                _currentNewGauge?.Update();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelUIManager.Update", ex);
            }
        }

        /// <summary>
        /// Handle scene loaded event
        /// </summary>
        /// <param name="scene">The loaded scene</param>
        /// <param name="mode">Load scene mode</param>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                ModLogger.UIDebug($"FuelUIManager: Scene loaded: {scene.name}");

                // Reset player reference when scene changes
                _localPlayer = null;
                _currentVehicle = null;
                _currentVehicleGUID = string.Empty;

                // Hide current gauges if any
                if (_currentGauge != null)
                {
                    _currentGauge.Hide();
                    _currentGauge = null;
                }
                if (_currentNewGauge != null)
                {
                    _currentNewGauge.Hide();
                    _currentNewGauge = null;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error handling scene loaded event", ex);
            }
        }

        /// <summary>
        /// Handle scene unloaded event
        /// </summary>
        /// <param name="scene">The unloaded scene</param>
        private void OnSceneUnloaded(Scene scene)
        {
            try
            {
                ModLogger.UIDebug($"FuelUIManager: Scene unloaded: {scene.name}");

                // Clean up UI elements from the unloaded scene
                var toRemove = new List<string>();
                
                // Clean up old gauges
                foreach (var kvp in _activeFuelGauges)
                {
                    if (kvp.Value != null && kvp.Value.IsVisible == false)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (string guid in toRemove)
                {
                    RemoveFuelGaugeForVehicle(guid);
                }

                // Clean up new gauges
                toRemove.Clear();
                foreach (var kvp in _activeNewGauges)
                {
                    if (kvp.Value != null && kvp.Value.IsVisible == false)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (string guid in toRemove)
                {
                    RemoveNewFuelGaugeForVehicle(guid);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error handling scene unloaded event", ex);
            }
        }

        /// <summary>
        /// Check if we're currently in a game scene where UI should be active
        /// </summary>
        /// <returns>True if in game scene, false otherwise</returns>
        private bool IsInGameScene()
        {
            try
            {
                string currentSceneName = SceneManager.GetActiveScene().name;

                // Don't show UI in lobby/menu scenes
                if (currentSceneName.Contains(Constants.Game.MENU_SCENE))
                {
                    return false;
                }

                // We're likely in a game scene
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error checking game scene", ex);
                return false;
            }
        }

        /// <summary>
        /// Find the local player
        /// </summary>
        private void FindLocalPlayer()
        {
            try
            {
                if (Player.Local != null)
                {
                    _localPlayer = Player.Local;
                    ModLogger.UIDebug("FuelUIManager: Found local player");
                }
                else
                {
                    ModLogger.UIDebug("FuelUIManager: Local player not found");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error finding local player", ex);
            }
        }

        /// <summary>
        /// Check if the player has changed vehicles
        /// </summary>
        private void CheckVehicleChange()
        {
            try
            {
                if (_localPlayer == null) return;

                LandVehicle currentVehicle = null;
                string currentVehicleGUID = string.Empty;

                // Check if player is in a vehicle
                if (_localPlayer.CurrentVehicle != null)
                {
                    var vehicleNetworkObject = _localPlayer.CurrentVehicle;
                    currentVehicle = vehicleNetworkObject.GetComponent<LandVehicle>();
                    if (currentVehicle != null)
                    {
                        currentVehicleGUID = currentVehicle.GUID.ToString();
                    }
                }

                // Check if vehicle changed
                if (currentVehicleGUID != _currentVehicleGUID)
                {
                    // Hide previous gauges
                    if (_currentGauge != null)
                    {
                        _currentGauge.Hide();
                        _currentGauge = null;
                    }
                    if (_currentNewGauge != null)
                    {
                        _currentNewGauge.Hide();
                        _currentNewGauge = null;
                    }

                    // Update current vehicle tracking
                    _currentVehicle = currentVehicle;
                    _currentVehicleGUID = currentVehicleGUID;

                    // Show appropriate gauge based on preference if player entered a vehicle
                    if (_currentVehicle != null)
                    {
                        bool useNewGauge = Core.Instance?.UseNewGaugeUI ?? false;
                        if (useNewGauge)
                        {
                            // Use the new circular gauge (FuelGauge class)
                            ShowNewFuelGaugeForVehicle(_currentVehicle);
                        }
                        else
                        {
                            // Use the old slider-based gauge (FuelGaugeUI class)
                            ShowFuelGaugeForVehicle(_currentVehicle);
                        }
                    }

                    ModLogger.UIDebug($"FuelUIManager: Vehicle changed to {(_currentVehicle?.VehicleName ?? "none")}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error checking vehicle change", ex);
            }
        }

        /// <summary>
        /// Show fuel gauge for a specific vehicle
        /// </summary>
        /// <param name="vehicle">The vehicle to show gauge for</param>
        public void ShowFuelGaugeForVehicle(LandVehicle vehicle)
        {
            try
            {
                if (vehicle == null) return;

                string vehicleGUID = vehicle.GUID.ToString();

                // Get or create fuel gauge for this vehicle
                if (!_activeFuelGauges.ContainsKey(vehicleGUID))
                {
                    // Get fuel system for this vehicle
                    VehicleFuelSystem? fuelSystem = Core.Instance?.GetFuelSystemManager()?.GetFuelSystem(vehicle.GUID.ToString());
                    if (fuelSystem == null)
                    {
                        ModLogger.Warning($"FuelUIManager: No fuel system found for vehicle {vehicleGUID.Substring(0, 8)}...");
                        return;
                    }

                    // Create new fuel gauge
                    FuelGaugeUI gauge = new FuelGaugeUI(fuelSystem);
                    _activeFuelGauges[vehicleGUID] = gauge;
                    ModLogger.UIDebug($"FuelUIManager: Created fuel gauge for vehicle {vehicleGUID.Substring(0, 8)}...");
                }

                // Show the gauge
                _currentGauge = _activeFuelGauges[vehicleGUID];
                _currentGauge.Show();

                ModLogger.UIDebug($"FuelUIManager: Showing fuel gauge for {vehicle.VehicleName}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing fuel gauge for vehicle", ex);
            }
        }

        /// <summary>
        /// Hide fuel gauge for a specific vehicle
        /// </summary>
        /// <param name="vehicleGUID">GUID of the vehicle</param>
        public void HideFuelGaugeForVehicle(string vehicleGUID)
        {
            try
            {
                if (_activeFuelGauges.TryGetValue(vehicleGUID, out FuelGaugeUI gauge))
                {
                    gauge.Hide();
                    ModLogger.UIDebug($"FuelUIManager: Hidden fuel gauge for vehicle {vehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error hiding fuel gauge for vehicle", ex);
            }
        }

        /// <summary>
        /// Remove fuel gauge for a specific vehicle
        /// </summary>
        /// <param name="vehicleGUID">GUID of the vehicle</param>
        public void RemoveFuelGaugeForVehicle(string vehicleGUID)
        {
            try
            {
                if (_activeFuelGauges.TryGetValue(vehicleGUID, out FuelGaugeUI gauge))
                {
                    gauge.Dispose();
                    _activeFuelGauges.Remove(vehicleGUID);

                    if (_currentGauge == gauge)
                    {
                        _currentGauge = null;
                    }

                    ModLogger.UIDebug($"FuelUIManager: Removed fuel gauge for vehicle {vehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error removing fuel gauge for vehicle", ex);
            }
        }

        /// <summary>
        /// Show new fuel gauge for a specific vehicle
        /// </summary>
        /// <param name="vehicle">The vehicle to show gauge for</param>
        public void ShowNewFuelGaugeForVehicle(LandVehicle vehicle)
        {
            try
            {
                if (vehicle == null) return;

                string vehicleGUID = vehicle.GUID.ToString();

                // Get or create new fuel gauge for this vehicle
                if (!_activeNewGauges.ContainsKey(vehicleGUID))
                {
                    // Get fuel system for this vehicle
                    VehicleFuelSystem? fuelSystem = Core.Instance?.GetFuelSystemManager()?.GetFuelSystem(vehicle.GUID.ToString());
                    if (fuelSystem == null)
                    {
                        ModLogger.Warning($"FuelUIManager: No fuel system found for vehicle {vehicleGUID.Substring(0, 8)}...");
                        return;
                    }

                    // Create new fuel gauge
                    FuelGauge gauge = new FuelGauge(fuelSystem);
                    _activeNewGauges[vehicleGUID] = gauge;
                    ModLogger.UIDebug($"FuelUIManager: Created new fuel gauge for vehicle {vehicleGUID.Substring(0, 8)}...");
                }

                // Show the gauge
                _currentNewGauge = _activeNewGauges[vehicleGUID];
                _currentNewGauge.Show();

                ModLogger.UIDebug($"FuelUIManager: Showing new fuel gauge for {vehicle.VehicleName}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing new fuel gauge for vehicle", ex);
            }
        }

        /// <summary>
        /// Hide new fuel gauge for a specific vehicle
        /// </summary>
        /// <param name="vehicleGUID">GUID of the vehicle</param>
        public void HideNewFuelGaugeForVehicle(string vehicleGUID)
        {
            try
            {
                if (_activeNewGauges.TryGetValue(vehicleGUID, out FuelGauge gauge))
                {
                    gauge.Hide();
                    ModLogger.UIDebug($"FuelUIManager: Hidden new fuel gauge for vehicle {vehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error hiding new fuel gauge for vehicle", ex);
            }
        }

        /// <summary>
        /// Remove new fuel gauge for a specific vehicle
        /// </summary>
        /// <param name="vehicleGUID">GUID of the vehicle</param>
        public void RemoveNewFuelGaugeForVehicle(string vehicleGUID)
        {
            try
            {
                if (_activeNewGauges.TryGetValue(vehicleGUID, out FuelGauge gauge))
                {
                    gauge.Dispose();
                    _activeNewGauges.Remove(vehicleGUID);

                    if (_currentNewGauge == gauge)
                    {
                        _currentNewGauge = null;
                    }

                    ModLogger.UIDebug($"FuelUIManager: Removed new fuel gauge for vehicle {vehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error removing new fuel gauge for vehicle", ex);
            }
        }

        /// <summary>
        /// Hide all fuel gauges
        /// </summary>
        public void HideAllGauges()
        {
            try
            {
                foreach (var gauge in _activeFuelGauges.Values)
                {
                    gauge?.Hide();
                }

                foreach (var gauge in _activeNewGauges.Values)
                {
                    gauge?.Hide();
                }

                _currentGauge = null;
                _currentNewGauge = null;
                ModLogger.UIDebug("FuelUIManager: Hidden all fuel gauges");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error hiding all fuel gauges", ex);
            }
        }

        /// <summary>
        /// Show all fuel gauges (for debugging)
        /// </summary>
        public void ShowAllGauges()
        {
            try
            {
                foreach (var gauge in _activeFuelGauges.Values)
                {
                    gauge?.Show();
                }

                foreach (var gauge in _activeNewGauges.Values)
                {
                    gauge?.Show();
                }

                ModLogger.UIDebug("FuelUIManager: Shown all fuel gauges (debug mode)");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing all fuel gauges", ex);
            }
        }

        /// <summary>
        /// Refresh all active fuel gauges to apply preference changes
        /// </summary>
        //public void RefreshAllGauges()
        //{
        //    try
        //    {
        //        ModLogger.UIDebug("FuelUIManager: Refreshing all fuel gauges...");
                
        //        foreach (var gauge in _activeFuelGauges.Values)
        //        {
        //            gauge?.UpdateGaugeDirection();
        //        }

        //        foreach (var gauge in _activeNewGauges.Values)
        //        {
        //            gauge?.Update();
        //        }
                
        //        ModLogger.UIDebug($"FuelUIManager: Refreshed {_activeFuelGauges.Count + _activeNewGauges.Count} fuel gauges");
        //    }
        //    catch (Exception ex)
        //    {
        //        ModLogger.Error("Error refreshing fuel gauges", ex);
        //    }
        //}

        /// <summary>
        /// Get statistics about active UI elements
        /// </summary>
        /// <returns>UI statistics</returns>
        public FuelUIStats GetStatistics()
        {
            var stats = new FuelUIStats();

            try
            {
                stats.TotalGauges = _activeFuelGauges.Count + _activeNewGauges.Count;
                stats.HasCurrentGauge = _currentGauge != null || _currentNewGauge != null;
                stats.CurrentVehicleName = _currentVehicle?.VehicleName ?? "None";
                stats.LocalPlayerInVehicle = _localPlayer?.CurrentVehicle != null;

                // Count old gauges
                foreach (var gauge in _activeFuelGauges.Values)
                {
                    if (gauge != null)
                    {
                        if (gauge.IsVisible)
                            stats.VisibleGauges++;
                        else
                            stats.HiddenGauges++;
                    }
                    else
                    {
                        stats.InvalidGauges++;
                    }
                }

                // Count new gauges
                foreach (var gauge in _activeNewGauges.Values)
                {
                    if (gauge != null)
                    {
                        if (gauge.IsVisible)
                            stats.VisibleGauges++;
                        else
                            stats.HiddenGauges++;
                    }
                    else
                    {
                        stats.InvalidGauges++;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error calculating fuel UI statistics", ex);
            }

            return stats;
        }

        /// <summary>
        /// Refresh all active fuel gauges to apply preference changes
        /// </summary>
        public void RefreshAllGauges()
        {
            try
            {
                ModLogger.UIDebug("FuelUIManager: Refreshing all gauges...");

                // Hide current gauges
                if (_currentGauge != null)
                {
                    _currentGauge.Hide();
                    _currentGauge = null;
                }
                if (_currentNewGauge != null)
                {
                    _currentNewGauge.Hide();
                    _currentNewGauge = null;
                }

                // If we have a current vehicle, show the appropriate gauge based on new preferences
                if (_currentVehicle != null)
                {
                    bool useNewGauge = Core.Instance?.UseNewGaugeUI ?? false;
                    if (useNewGauge)
                    {
                        // Switch to new circular gauge
                        ShowNewFuelGaugeForVehicle(_currentVehicle);
                    }
                    else
                    {
                        // Switch to old slider-based gauge
                        ShowFuelGaugeForVehicle(_currentVehicle);
                    }
                }

                ModLogger.UIDebug("FuelUIManager: All gauges refreshed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error refreshing fuel gauges", ex);
            }
        }

        /// <summary>
        /// Dispose of the fuel UI manager
        /// </summary>
        public void Dispose()
        {
            try
            {
                ModLogger.UIDebug("FuelUIManager: Disposing...");

                // Unsubscribe from scene events
                SceneManager.sceneLoaded -= (UnityAction<Scene, LoadSceneMode>)OnSceneLoaded;
                SceneManager.sceneUnloaded -= (UnityAction<Scene>)OnSceneUnloaded;

                // Dispose of all old gauges
                foreach (var gauge in _activeFuelGauges.Values)
                {
                    gauge?.Dispose();
                }

                // Dispose of all new gauges
                foreach (var gauge in _activeNewGauges.Values)
                {
                    gauge?.Dispose();
                }

                _activeFuelGauges.Clear();
                _activeNewGauges.Clear();
                _currentGauge = null;
                _currentNewGauge = null;
                _currentVehicle = null;
                _localPlayer = null;

                ModLogger.Info("FuelUIManager: Disposed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error disposing FuelUIManager", ex);
            }
        }
    }

    /// <summary>
    /// Statistics about fuel UI elements
    /// </summary>
    public class FuelUIStats
    {
        public int TotalGauges { get; set; }
        public int VisibleGauges { get; set; }
        public int HiddenGauges { get; set; }
        public int InvalidGauges { get; set; }
        public bool HasCurrentGauge { get; set; }
        public string CurrentVehicleName { get; set; } = string.Empty;
        public bool LocalPlayerInVehicle { get; set; }
    }
}
