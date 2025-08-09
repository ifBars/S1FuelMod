using MelonLoader;
using HarmonyLib;
using System;
#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
#endif
using MelonLoader.Preferences;
using S1FuelMod.Utils;
using S1FuelMod.Integrations;
using S1FuelMod.Systems;
using S1FuelMod.UI;

[assembly: MelonInfo(typeof(S1FuelMod.Core), Constants.MOD_NAME, Constants.MOD_VERSION, Constants.MOD_AUTHORS)]
[assembly: MelonGame(Constants.Game.GAME_STUDIO, Constants.Game.GAME_NAME)]

namespace S1FuelMod
{
    /// <summary>
    /// Main MelonMod class for the S1FuelMod
    /// Adds a comprehensive fuel system to LandVehicles in Schedule I
    /// </summary>
    public class Core : MelonMod
    {
        public static Core? Instance { get; private set; }

        // MelonPreferences
        private MelonPreferences_Category? _preferencesCategory;
        private MelonPreferences_Entry<bool>? _enableFuelSystem;
        private MelonPreferences_Entry<float>? _fuelConsumptionMultiplier;
        private MelonPreferences_Entry<float>? _defaultFuelCapacity;
        private MelonPreferences_Entry<float>? _shitboxFuelCapacity;
        private MelonPreferences_Entry<float>? _veeperFuelCapacity;
        private MelonPreferences_Entry<float>? _bruiserFuelCapacity;
        private MelonPreferences_Entry<float>? _dinklerFuelCapacity;
        private MelonPreferences_Entry<float>? _hounddogFuelCapacity;
        private MelonPreferences_Entry<float>? _cheetahFuelCapacity;
        private MelonPreferences_Entry<bool>? _showFuelGauge;
        private MelonPreferences_Entry<bool>? _enableDynamicPricing;
        private MelonPreferences_Entry<bool>? _enablePricingOnTier;
        private MelonPreferences_Entry<bool>? _enableDebugLogging;

        // Mod Systems
        private FuelSystemManager? _fuelSystemManager;
        private FuelUIManager? _fuelUIManager;
        private FuelStationManager? _fuelStationManager;

        // Public properties for accessing preferences
        public bool EnableFuelSystem => _enableFuelSystem?.Value ?? true;
        public float FuelConsumptionMultiplier => _fuelConsumptionMultiplier?.Value ?? 1.0f;
        public float DefaultFuelCapacity => _defaultFuelCapacity?.Value ?? Constants.Defaults.DEFAULT_FUEL_CAPACITY;
        public float ShitboxFuelCapacity => _shitboxFuelCapacity?.Value ?? Constants.Defaults.SHITBOX_FUEL_CAPACITY;
        public float VeeperFuelCapacity => _veeperFuelCapacity?.Value ?? Constants.Defaults.VEEPER_FUEL_CAPACITY;
        public float BruiserFuelCapacity => _bruiserFuelCapacity?.Value ?? Constants.Defaults.BRUISER_FUEL_CAPACITY;
        public float DinklerFuelCapacity => _dinklerFuelCapacity?.Value ?? Constants.Defaults.DINKLER_FUEL_CAPACITY;
        public float HounddogFuelCapacity => _hounddogFuelCapacity?.Value ?? Constants.Defaults.HOUNDDOG_FUEL_CAPACITY;
        public float CheetahFuelCapacity => _cheetahFuelCapacity?.Value ?? Constants.Defaults.CHEETAH_FUEL_CAPACITY;
        public bool ShowFuelGauge => _showFuelGauge?.Value ?? true;
        public bool EnableDynamicPricing => _enableDynamicPricing?.Value ?? true;
        public bool EnablePricingOnTier => _enablePricingOnTier?.Value ?? true;
        public bool EnableDebugLogging => _enableDebugLogging?.Value ?? false;

        /// <summary>
        /// Called when the mod is being loaded
        /// </summary>
        public override void OnInitializeMelon()
        {
            Instance = this;
            ModLogger.LogInitialization();

            try
            {
                // Initialize MelonPreferences
                InitializePreferences();

                // Set up Harmony patches
                HarmonyPatches.SetModInstance(this);

                ModLogger.Info("S1FuelMod initialized successfully");
                ModLogger.Info($"Fuel System Enabled: {EnableFuelSystem}");
                ModLogger.Info($"Fuel Consumption Multiplier: {FuelConsumptionMultiplier}x");
                ModLogger.Info($"Default Fuel Capacity: {DefaultFuelCapacity}L");
                ModLogger.Info($"Show Fuel Gauge: {ShowFuelGauge}");
                ModLogger.Info("Debug Controls:");
                ModLogger.Info("  F6 - Toggle Debug Logging (FuelDebug/UIDebug messages)");
                ModLogger.Info("  F7 - Test Fuel Consumption (5L) on Current Vehicle");
                ModLogger.Info("  F8 - Show Current Vehicle Info");
                ModLogger.Info("  F9 - Toggle Fuel System Debug Info");
                ModLogger.Info("  F10 - Refill All Vehicles");
                ModLogger.Info("  F11 - Drain All Vehicles (10L)");
                ModLogger.Info("  F12 - Test UI Elements Directly");
                ModLogger.Info("  F5 - Show Fuel Station Info & Force Rescan");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Failed to initialize S1FuelMod", ex);
            }
        }

        /// <summary>
        /// Called when a scene is initialized
        /// </summary>
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            try
            {
                ModLogger.Info($"Scene initialized: {sceneName} (index: {buildIndex})");

                // Initialize systems when we're in the main game scene
                if (sceneName.Contains(Constants.Game.MAIN_SCENE))
                {
                    ModLogger.Info("Main game scene detected, initializing fuel systems...");
                    InitializeSystems();
                }
                else
                {
                    ModLogger.Debug($"Scene '{sceneName}' is not a main game scene, skipping system initialization");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error during scene initialization", ex);
            }
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void OnUpdate()
        {
            try
            {
                // Handle debug key inputs
                HandleDebugInputs();

                // Update fuel systems (also pumps networking in manager)
                _fuelSystemManager?.Update();
                _fuelUIManager?.Update();
                _fuelStationManager?.Update();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error during update", ex);
            }
        }

        /// <summary>
        /// Initialize MelonPreferences for mod configuration
        /// </summary>
        private void InitializePreferences()
        {
            try
            {
                _preferencesCategory = MelonPreferences.CreateCategory(Constants.PREFERENCES_CATEGORY);

                // Core fuel system settings
                _enableFuelSystem = _preferencesCategory.CreateEntry<bool>(
                    "EnableFuelSystem",
                    Constants.Defaults.ENABLE_FUEL_SYSTEM,
                    "Enable Fuel System",
                    "If enabled, vehicles will consume fuel and require refueling"
                );

                _fuelConsumptionMultiplier = _preferencesCategory.CreateEntry<float>(
                    "FuelConsumptionMultiplier",
                    Constants.Defaults.FUEL_CONSUMPTION_MULTIPLIER,
                    "Fuel Consumption Multiplier",
                    "Multiplier for fuel consumption rate (1.0 = normal, 0.5 = half consumption, 2.0 = double consumption)",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_CONSUMPTION_MULTIPLIER, Constants.Constraints.MAX_CONSUMPTION_MULTIPLIER)
                );

                _defaultFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "DefaultFuelCapacity",
                    Constants.Defaults.DEFAULT_FUEL_CAPACITY,
                    "Default Fuel Capacity (L)",
                    "Default fuel tank capacity for vehicles in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _shitboxFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "ShitboxFuelCapacity",
                    Constants.Defaults.SHITBOX_FUEL_CAPACITY,
                    "Shitbox Fuel Capacity (L)",
                    "Fuel capacity for the Shitbox vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _veeperFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "VeeperFuelCapacity",
                    Constants.Defaults.VEEPER_FUEL_CAPACITY,
                    "Veeper Fuel Capacity (L)",
                    "Fuel capacity for the Veeper vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _bruiserFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "BruiserFuelCapacity",
                    Constants.Defaults.BRUISER_FUEL_CAPACITY,
                    "Bruiser Fuel Capacity (L)",
                    "Fuel capacity for the Bruiser vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _dinklerFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "DinklerFuelCapacity",
                    Constants.Defaults.DINKLER_FUEL_CAPACITY,
                    "Dinkler Fuel Capacity (L)",
                    "Fuel capacity for the Dinkler vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _hounddogFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "HounddogFuelCapacity",
                    Constants.Defaults.HOUNDDOG_FUEL_CAPACITY,
                    "Hounddog Fuel Capacity (L)",
                    "Fuel capacity for the Hounddog vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _cheetahFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "CheetahFuelCapacity",
                    Constants.Defaults.CHEETAH_FUEL_CAPACITY,
                    "Cheetah Fuel Capacity (L)",
                    "Fuel capacity for the Cheetah vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                // UI settings
                _showFuelGauge = _preferencesCategory.CreateEntry<bool>(
                    "ShowFuelGauge",
                    Constants.Defaults.SHOW_FUEL_GAUGE,
                    "Show Fuel Gauge",
                    "If enabled, shows fuel gauge UI when driving vehicles"
                );

                _enableDynamicPricing = _preferencesCategory.CreateEntry<bool>(
                    "EnableDynamicPricing",
                    true,
                    "Enable Dynamic Pricing",
                    "If enabled, fuel prices will vary based on which day it is"
                );

                _enablePricingOnTier = _preferencesCategory.CreateEntry<bool>(
                    "EnablePricingOnTier",
                    true,
                    "Enable Pricing on Tier",
                    "If enabled, fuel prices will be inflated based on the player's current tier"
                );

                // Debug settings
                _enableDebugLogging = _preferencesCategory.CreateEntry<bool>(
                    "EnableDebugLogging",
                    Constants.Defaults.ENABLE_DEBUG_LOGGING,
                    "Enable Debug Logging",
                    "If enabled, shows detailed debug information in console"
                );

                ModLogger.Info("MelonPreferences initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Failed to initialize MelonPreferences", ex);
            }
        }

        /// <summary>
        /// Initialize mod systems after scene load
        /// </summary>
        private void InitializeSystems()
        {
            try
            {
                if (!EnableFuelSystem)
                {
                    ModLogger.Info("Fuel system disabled via preferences");
                    return;
                }

                // Initialize fuel system manager
                _fuelSystemManager = new FuelSystemManager();
                ModLogger.Info("Fuel system manager initialized");

                // Update Harmony patches with the fuel system manager now available
                HarmonyPatches.SetModInstance(this);
                ModLogger.Info("Harmony patches updated with fuel systems");

                // Initialize UI manager
                _fuelUIManager = new FuelUIManager();
                ModLogger.Info("Fuel UI manager initialized");

                // Initialize fuel station manager
                _fuelStationManager = new FuelStationManager();
                ModLogger.Info("Fuel station manager initialized");

                ModLogger.Info("All fuel systems initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Failed to initialize fuel systems", ex);
            }
        }

        /// <summary>
        /// Handle debug key inputs
        /// </summary>
        private void HandleDebugInputs()
        {
            try
            {
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
                {
                    _fuelSystemManager?.ToggleDebugInfo();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F10))
                {
                    _fuelSystemManager?.RefillAllVehicles();
                    ModLogger.Info("All vehicles refilled");
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F11))
                {
                    _fuelSystemManager?.DrainAllVehicles(10f);
                    ModLogger.Info("All vehicles drained by 10L");
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
                {
                    ShowCurrentVehicleInfo();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F7))
                {
                    TestCurrentVehicleFuelConsumption();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F6))
                {
                    ToggleDebugLogging();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12))
                {
                    TestUIElementsDirectly();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F5))
                {
                    ShowFuelStationInfo();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error handling debug inputs", ex);
            }
        }

        /// <summary>
        /// Test UI elements directly by manually setting values
        /// </summary>
        private void TestUIElementsDirectly()
        {
            try
            {
                var localPlayer = Player.Local;
                if (localPlayer?.CurrentVehicle == null)
                {
                    ModLogger.Info("F12: Player not in vehicle");
                    return;
                }

                var fuelUIManager = GetFuelUIManager();
                if (fuelUIManager != null)
                {
                    var stats = fuelUIManager.GetStatistics();
                    ModLogger.Info($"F12: UI Stats - Gauges: {stats.TotalGauges}, Visible: {stats.VisibleGauges}, Vehicle: {stats.CurrentVehicleName}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("F12: Error testing UI elements", ex);
            }
        }

        /// <summary>
        /// Toggle debug logging on/off
        /// </summary>
        private void ToggleDebugLogging()
        {
            try
            {
                if (_enableDebugLogging != null)
                {
                    _enableDebugLogging.Value = !_enableDebugLogging.Value;
                    ModLogger.Info($"Debug logging {(_enableDebugLogging.Value ? "enabled" : "disabled")}");

                    if (_enableDebugLogging.Value)
                    {
                        ModLogger.Info("F6 Debug: Debug logging is now ON - you should see [FUEL] and [UI] messages");
                    }
                    else
                    {
                        ModLogger.Info("F6 Debug: Debug logging is now OFF");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error toggling debug logging", ex);
            }
        }

        /// <summary>
        /// Test fuel consumption on current vehicle (for debugging UI updates)
        /// </summary>
        private void TestCurrentVehicleFuelConsumption()
        {
            try
            {
                var localPlayer = Player.Local;
                var vehicle = localPlayer?.CurrentVehicle?.GetComponent<LandVehicle>();
                var fuelSystem = _fuelSystemManager?.GetFuelSystem(vehicle.GUID.ToString());

                if (fuelSystem == null)
                {
                    ModLogger.Info("F7: No fuel system found");
                    return;
                }

                float beforeFuel = fuelSystem.CurrentFuelLevel;
                fuelSystem.ConsumeFuel(5f);
                ModLogger.Info($"F7: Fuel test {beforeFuel:F1}L → {fuelSystem.CurrentFuelLevel:F1}L");
            }
            catch (Exception ex)
            {
                ModLogger.Error("F7: Error testing fuel consumption", ex);
            }
        }

        /// <summary>
        /// Show fuel station information and force rescan
        /// </summary>
        private void ShowFuelStationInfo()
        {
            try
            {
                var fuelStationManager = GetFuelStationManager();
                if (fuelStationManager == null)
                {
                    ModLogger.Info("F5: No fuel station manager available");
                    return;
                }

                // Force a rescan
                fuelStationManager.ForceScan();

                // Get and display statistics
                var stats = fuelStationManager.GetStatistics();
                var activeFuelStations = fuelStationManager.GetActiveFuelStations();

                ModLogger.Info("=== Fuel Station Info ===");
                ModLogger.Info($"Total Stations: {stats.TotalStations}");
                ModLogger.Info($"Active Stations: {stats.ActiveStations}");
                ModLogger.Info($"Inactive Stations: {stats.InactiveStations}");

                if (activeFuelStations.Count > 0)
                {
                    ModLogger.Info("Active Fuel Stations:");
                    for (int i = 0; i < activeFuelStations.Count && i < 10; i++) // Limit to first 10
                    {
                        var station = activeFuelStations[i];
                        if (station != null && station.gameObject != null)
                        {
                            ModLogger.Info($"  {i + 1}. {station.gameObject.name} at {station.transform.position}");
                        }
                    }

                    if (activeFuelStations.Count > 10)
                    {
                        ModLogger.Info($"  ... and {activeFuelStations.Count - 10} more stations");
                    }
                }
                else
                {
                    ModLogger.Info("No active fuel stations found!");
                    ModLogger.Info("Make sure there are GameObjects named 'Bowser (EMC Merge)' or 'Bowser  (EMC Merge)' in the scene");
                }

                ModLogger.Info("=== End Fuel Station Info ===");
            }
            catch (Exception ex)
            {
                ModLogger.Error("F5: Error showing fuel station info", ex);
            }
        }

        /// <summary>
        /// Show information about the current vehicle player is in
        /// </summary>
        private void ShowCurrentVehicleInfo()
        {
            try
            {
                var localPlayer = Player.Local;
                if (localPlayer == null)
                {
                    ModLogger.Debug("No local player found");
                    return;
                }

                if (localPlayer.CurrentVehicle == null)
                {
                    ModLogger.Debug("Player is not in a vehicle");
                    return;
                }

                var vehicleNetworkObject = localPlayer.CurrentVehicle;
                var vehicle = vehicleNetworkObject.GetComponent<LandVehicle>();

                if (vehicle == null)
                {
                    ModLogger.Info("Current vehicle is not a LandVehicle");
                    return;
                }

                var fuelSystem = _fuelSystemManager?.GetFuelSystem(vehicle.GUID.ToString());
                if (fuelSystem == null)
                {
                    ModLogger.Info($"No fuel system found for vehicle {vehicle.VehicleName} ({vehicle.GUID.ToString().Substring(0, 8)}...)");
                    return;
                }

                ModLogger.Info("=== Current Vehicle Info ===");
                ModLogger.Info($"Vehicle: {vehicle.VehicleName} ({vehicle.VehicleCode})");
                ModLogger.Info($"GUID: {vehicle.GUID}");
                ModLogger.Info($"Fuel: {fuelSystem.CurrentFuelLevel:F1}L / {fuelSystem.MaxFuelCapacity:F1}L ({fuelSystem.FuelPercentage:F1}%)");
                ModLogger.Info($"Engine Running: {fuelSystem.IsEngineRunning}");
                ModLogger.Info($"Occupied: {vehicle.isOccupied}");
                ModLogger.Info($"Throttle: {vehicle.currentThrottle:F2}");
                ModLogger.Info($"Speed: {vehicle.speed_Kmh:F1} km/h");
                ModLogger.Info($"Warnings: {(fuelSystem.IsLowFuel ? "LOW " : "")}{(fuelSystem.IsCriticalFuel ? "CRITICAL " : "")}{(fuelSystem.IsOutOfFuel ? "EMPTY" : "")}");
                ModLogger.Info("=== End Vehicle Info ===");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing current vehicle info", ex);
            }
        }

        /// <summary>
        /// Called when the mod is being unloaded
        /// </summary>
        public override void OnApplicationQuit()
        {
            try
            {
                ModLogger.Info("S1FuelMod shutting down...");

                // Clean up systems
                _fuelSystemManager?.Dispose();
                _fuelUIManager?.Dispose();
                _fuelStationManager?.Dispose();

                Instance = null;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error during mod shutdown", ex);
            }
        }

        /// <summary>
        /// Save preferences when they change
        /// </summary>
        public void SavePreferences()
        {
            try
            {
                _preferencesCategory?.SaveToFile();
                ModLogger.Info("Preferences saved successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Failed to save preferences", ex);
            }
        }

        /// <summary>
        /// Get the fuel system manager instance
        /// </summary>
        public FuelSystemManager? GetFuelSystemManager()
        {
            return _fuelSystemManager;
        }

        /// <summary>
        /// Get the fuel UI manager instance
        /// </summary>
        public FuelUIManager? GetFuelUIManager()
        {
            return _fuelUIManager;
        }

        /// <summary>
        /// Get the fuel station manager instance
        /// </summary>
        public FuelStationManager? GetFuelStationManager()
        {
            return _fuelStationManager;
        }
    }
}