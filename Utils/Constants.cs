namespace S1FuelMod.Utils
{
    /// <summary>
    /// Constants and configuration values for the S1FuelMod
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Mod information
        /// </summary>
        public const string MOD_NAME = "S1FuelMod";
        public const string MOD_VERSION = "1.0.0";
        public const string MOD_AUTHORS = "Bars & SirTidez";
        public const string MOD_DESCRIPTION = "Adds a comprehensive fuel system to LandVehicles in Schedule I";

        /// <summary>
        /// MelonPreferences configuration
        /// </summary>
        public const string PREFERENCES_CATEGORY = "S1FuelMod";
        public const string PREFERENCES_FILE_PATH = "UserData/S1FuelMod.cfg";

        /// <summary>
        /// Default preference values
        /// </summary>
        public static class Defaults
        {
            public const bool ENABLE_FUEL_SYSTEM = true;
            public const float FUEL_CONSUMPTION_MULTIPLIER = 1.0f;
            public const float DEFAULT_FUEL_CAPACITY = 50f;
            public const float SHITBOX_FUEL_CAPACITY = 12f;
            public const float VEEPER_FUEL_CAPACITY = 16f;
            public const float BRUISER_FUEL_CAPACITY = 14f;
            public const float DINKLER_FUEL_CAPACITY = 16f;
            public const float HOUNDDOG_FUEL_CAPACITY = 16f;
            public const float CHEETAH_FUEL_CAPACITY = 12f;
            public const bool SHOW_FUEL_GAUGE = true;
            public const bool ENABLE_DEBUG_LOGGING = true; // Enable for testing
        }

        /// <summary>
        /// Preference value constraints
        /// </summary>
        public static class Constraints
        {
            public const float MIN_CONSUMPTION_MULTIPLIER = 0.1f;
            public const float MAX_CONSUMPTION_MULTIPLIER = 5.0f;
            public const float MIN_FUEL_CAPACITY = 10f;
            public const float MAX_FUEL_CAPACITY = 200f;
        }

        /// <summary>
        /// Fuel system constants
        /// </summary>
        public static class Fuel
        {
            // Consumption rates (liters per hour at full throttle) - made much faster for testing
            public const float BASE_CONSUMPTION_RATE = 360f; // 6L per minute instead of per hour
            public const float IDLE_CONSUMPTION_RATE = 30f; // 0.5L per minute instead of per hour

            // Warning thresholds (percentage)
            public const float LOW_FUEL_WARNING_THRESHOLD = 20f;
            public const float CRITICAL_FUEL_WARNING_THRESHOLD = 5f;

            // Fuel station settings
            public const float REFUEL_RATE = 5f; // liters per second
            public const float FUEL_PRICE_PER_LITER = 1.80f;

            // Performance effects
            public const float ENGINE_CUTOFF_FUEL_LEVEL = 0f;
            public const float ENGINE_SPUTTER_FUEL_LEVEL = 4f;
        }

        /// <summary>
        /// UI constants
        /// </summary>
        public static class UI
        {
            // Fuel gauge settings
            public const float GAUGE_UPDATE_INTERVAL = 0.1f;
            public const float GAUGE_WIDTH = 200f;
            public const float GAUGE_HEIGHT = 20f;

            // Colors (RGBA)
            public static class Colors
            {
                public static readonly UnityEngine.Color FUEL_NORMAL = new UnityEngine.Color(0.2f, 0.8f, 0.2f, 0.8f);
                public static readonly UnityEngine.Color FUEL_LOW = new UnityEngine.Color(1f, 0.8f, 0f, 0.8f);
                public static readonly UnityEngine.Color FUEL_CRITICAL = new UnityEngine.Color(1f, 0.2f, 0.2f, 0.8f);
                public static readonly UnityEngine.Color GAUGE_BACKGROUND = new UnityEngine.Color(0.1f, 0.1f, 0.1f, 0.6f);
                public static readonly UnityEngine.Color GAUGE_BORDER = new UnityEngine.Color(0.8f, 0.8f, 0.8f, 0.8f);
            }
        }

        /// <summary>
        /// Game-related constants
        /// </summary>
        public static class Game
        {
            public const string GAME_STUDIO = "TVGS";
            public const string GAME_NAME = "Schedule I";

            // Layer names
            public const string VEHICLE_LAYER = "Vehicle";
            public const string UI_LAYER = "UI";

            // Scene names
            public const string MAIN_SCENE = "Main";
            public const string MENU_SCENE = "Menu";
        }

        /// <summary>
        /// Network constants
        /// </summary>
        public static class Network
        {
            public const string FUEL_UPDATE_MESSAGE_TYPE = "fuel_update";
            public const string FUEL_SYNC_MESSAGE_TYPE = "fuel_sync";
            public const float SYNC_INTERVAL = 1f; // seconds
        }

        /// <summary>
        /// Save system constants
        /// </summary>
        public static class SaveSystem
        {
            public const string FUEL_DATA_KEY_PREFIX = "fuel_";
            public const string FUEL_LEVEL_KEY = "fuel_level";
            public const string MAX_CAPACITY_KEY = "max_capacity";
            public const string CONSUMPTION_RATE_KEY = "consumption_rate";
        }
    }
}