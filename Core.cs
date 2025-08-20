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
        private MelonPreferences_Category? _capacityCategory;
        private MelonPreferences_Entry<bool>? _enableFuelSystem;
        private MelonPreferences_Entry<float>? _fuelConsumptionMultiplier;
        private MelonPreferences_Entry<float>? _defaultFuelCapacity;
        private MelonPreferences_Entry<float>? _shitboxFuelCapacity;
        private MelonPreferences_Entry<float>? _veeperFuelCapacity;
        private MelonPreferences_Entry<float>? _bruiserFuelCapacity;
        private MelonPreferences_Entry<float>? _dinklerFuelCapacity;
        private MelonPreferences_Entry<float>? _hounddogFuelCapacity;
        private MelonPreferences_Entry<float>? _cheetahFuelCapacity;
        private MelonPreferences_Entry<float>? _hotboxFuelCapacity;
        private MelonPreferences_Entry<bool>? _showFuelGauge;
        private MelonPreferences_Entry<bool>? _enableDynamicPricing;
        private MelonPreferences_Entry<bool>? _enablePricingOnTier;
        private MelonPreferences_Entry<bool>? _enableDebugLogging;
        private MelonPreferences_Entry<float>? _baseFuelPricePerLiter;
        private MelonPreferences_Entry<bool>? _enableCurfewFuelTax;
        private MelonPreferences_Entry<bool>? _swapGaugeDirection;
        private MelonPreferences_Entry<bool>? _useNewGaugeUI;

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
        public float HotboxFuelCapacity => _hotboxFuelCapacity?.Value ?? Constants.Defaults.HOTBOX_FUEL_CAPACITY; // Default to 40L if not set
        public bool ShowFuelGauge => _showFuelGauge?.Value ?? true;
        public bool EnableDynamicPricing => _enableDynamicPricing?.Value ?? true;
        public bool EnablePricingOnTier => _enablePricingOnTier?.Value ?? true;
        public bool EnableDebugLogging => _enableDebugLogging?.Value ?? false;
        public float BaseFuelPricePerLiter => _baseFuelPricePerLiter?.Value ?? Constants.Fuel.FUEL_PRICE_PER_LITER;
        public bool EnableCurfewFuelTax => _enableCurfewFuelTax?.Value ?? false;
        public bool SwapGaugeDirection => _swapGaugeDirection?.Value ?? false;
        public bool UseNewGaugeUI => _useNewGaugeUI?.Value ?? true;

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
                ModLogger.Info($"Show Fuel Gauge: {ShowFuelGauge}");
                ModLogger.Info($"Gauge Direction: {(SwapGaugeDirection ? "Left to Right" : "Right to Left")}");
                ModLogger.Info($"Use New Gauge UI: {UseNewGaugeUI} ({(UseNewGaugeUI ? "Circular" : "Slider")})");
                ModLogger.Info("Debug logging can be toggled in the mod preferences");
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
                ModLogger.Debug($"Scene initialized: {sceneName} (index: {buildIndex})");

                if (sceneName.Contains(Constants.Game.MAIN_SCENE))
                {
                    ModLogger.Debug("Main game scene detected, initializing fuel systems...");
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
                _capacityCategory = MelonPreferences.CreateCategory(Constants.PREFERENCES_CATEGORY+"_Capacity", "Fuel Tank Capacity");

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

                _baseFuelPricePerLiter = _preferencesCategory.CreateEntry<float>(
                    "BaseFuelPricePerLiter",
                    Constants.Fuel.FUEL_PRICE_PER_LITER,
                    "Fuel Price Per Liter ($)",
                    "Base price per liter of fuel in dollars",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_PRICE_PER_LITER, Constants.Constraints.MAX_FUEL_PRICE_PER_LITER)
                );

                _defaultFuelCapacity = _preferencesCategory.CreateEntry<float>(
                    "DefaultFuelCapacity",
                    Constants.Defaults.DEFAULT_FUEL_CAPACITY,
                    "Default Fuel Capacity (L)",
                    "Default fuel tank capacity for vehicles in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _shitboxFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "ShitboxFuelCapacity",
                    Constants.Defaults.SHITBOX_FUEL_CAPACITY,
                    "Shitbox Fuel Capacity (L)",
                    "Fuel capacity for the Shitbox vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _veeperFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "VeeperFuelCapacity",
                    Constants.Defaults.VEEPER_FUEL_CAPACITY,
                    "Veeper Fuel Capacity (L)",
                    "Fuel capacity for the Veeper vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _bruiserFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "BruiserFuelCapacity",
                    Constants.Defaults.BRUISER_FUEL_CAPACITY,
                    "Bruiser Fuel Capacity (L)",
                    "Fuel capacity for the Bruiser vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _dinklerFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "DinklerFuelCapacity",
                    Constants.Defaults.DINKLER_FUEL_CAPACITY,
                    "Dinkler Fuel Capacity (L)",
                    "Fuel capacity for the Dinkler vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _hounddogFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "HounddogFuelCapacity",
                    Constants.Defaults.HOUNDDOG_FUEL_CAPACITY,
                    "Hounddog Fuel Capacity (L)",
                    "Fuel capacity for the Hounddog vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _cheetahFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "CheetahFuelCapacity",
                    Constants.Defaults.CHEETAH_FUEL_CAPACITY,
                    "Cheetah Fuel Capacity (L)",
                    "Fuel capacity for the Cheetah vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

                _hotboxFuelCapacity = _capacityCategory.CreateEntry<float>(
                    "HotboxFuelCapacity",
                    Constants.Defaults.HOTBOX_FUEL_CAPACITY,
                    "Hotbox Fuel Capacity (L)",
                    "Fuel capacity for the Hotbox vehicle in liters",
                    validator: new ValueRange<float>(Constants.Constraints.MIN_FUEL_CAPACITY, Constants.Constraints.MAX_FUEL_CAPACITY)
                );

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

                _enableCurfewFuelTax = _preferencesCategory.CreateEntry<bool>(
                    "EnableCurfewFuelTax",
                    true,
                    "Enable Curfew Fuel Tax",
                    "If enabled, fuel price is doubled during curfew hours"
                );

                _enableDebugLogging = _preferencesCategory.CreateEntry<bool>(
                    "EnableDebugLogging",
                    Constants.Defaults.ENABLE_DEBUG_LOGGING,
                    "Enable Debug Logging",
                    "If enabled, shows detailed debug information in console"
                );

                _swapGaugeDirection = _preferencesCategory.CreateEntry<bool>(
                    "SwapGaugeDirection",
                    false,
                    "Swap Gauge Direction",
                    "If enabled, fuel gauge fills from left to right instead of right to left"
                );

                _useNewGaugeUI = _preferencesCategory.CreateEntry<bool>(
                    "UseNewGaugeUI",
                    true,
                    "Use New Gauge UI",
                    "If enabled, uses the new circular fuel gauge instead of the old slider-based gauge. Change requires vehicle re-entry to take effect."
                );

                ModLogger.Debug("MelonPreferences initialized successfully");
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
                    ModLogger.Debug("Fuel system disabled via preferences");
                    return;
                }

                // Initialize fuel system manager
                _fuelSystemManager = new FuelSystemManager();
                ModLogger.Debug("Fuel system manager initialized");

                // Update Harmony patches with the fuel system manager now available
                HarmonyPatches.SetModInstance(this);
                ModLogger.Debug("Harmony patches updated with fuel systems");

                // Initialize UI manager
                _fuelUIManager = new FuelUIManager();
                ModLogger.Debug("Fuel UI manager initialized");

                // Initialize fuel station manager
                _fuelStationManager = new FuelStationManager();
                ModLogger.Debug("Fuel station manager initialized");

                ModLogger.Debug("All fuel systems initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Failed to initialize fuel systems", ex);
            }
        }

        /// <summary>
        /// Toggle debug logging on/off
        /// </summary>
        public void ToggleDebugLogging()
        {
            try
            {
                if (_enableDebugLogging != null)
                {
                    _enableDebugLogging.Value = !_enableDebugLogging.Value;
                    ModLogger.Info($"Debug logging {(_enableDebugLogging.Value ? "enabled" : "disabled")}");

                    if (_enableDebugLogging.Value)
                    {
                        ModLogger.Info("Debug logging is now ON - you should see [FUEL] and [UI] messages");
                    }
                    else
                    {
                        ModLogger.Info("Debug logging is now OFF");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error toggling debug logging", ex);
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
                ModLogger.Debug("Preferences saved successfully");
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

        /// <summary>
        /// Refresh all active fuel gauges to apply preference changes
        /// </summary>
        public void RefreshFuelGauges()
        {
            try
            {
                _fuelUIManager?.RefreshAllGauges();
                ModLogger.Debug("Fuel gauges refreshed to apply preference changes");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error refreshing fuel gauges", ex);
            }
        }

        /// <summary>
        /// Handle preference changes for the new gauge UI
        /// Call this method when the UseNewGaugeUI preference changes
        /// </summary>
        public void OnNewGaugeUIPreferenceChanged()
        {
            try
            {
                ModLogger.Info($"New Gauge UI preference changed to: {UseNewGaugeUI}");
                RefreshFuelGauges();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error handling new gauge UI preference change", ex);
            }
        }
    }
}