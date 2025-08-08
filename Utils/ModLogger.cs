using MelonLoader;
using System;

namespace S1FuelMod.Utils
{
    /// <summary>
    /// Centralized logging service for the S1FuelMod
    /// </summary>
    public static class ModLogger
    {
        /// <summary>
        /// Log an informational message
        /// </summary>
        public static void Info(string message)
        {
            MelonLogger.Msg(message);
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        public static void Warning(string message)
        {
            MelonLogger.Warning(message);
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        public static void Error(string message)
        {
            MelonLogger.Error(message);
        }

        /// <summary>
        /// Log an error with exception details
        /// </summary>
        public static void Error(string message, Exception exception)
        {
            MelonLogger.Error($"{message}: {exception.Message}");
            MelonLogger.Error($"Stack trace: {exception.StackTrace}");
        }

        /// <summary>
        /// Log a debug message (only when debug logging is enabled)
        /// </summary>
        public static void Debug(string message)
        {
            if (Core.Instance?.EnableDebugLogging == true)
            {
                MelonLogger.Msg($"[DEBUG] {message}");
            }
        }

        /// <summary>
        /// Log fuel system specific debug message
        /// </summary>
        public static void FuelDebug(string message)
        {
            if (Core.Instance?.EnableDebugLogging == true)
            {
                MelonLogger.Msg($"[FUEL] {message}");
            }
        }

        /// <summary>
        /// Log UI system specific debug message
        /// </summary>
        public static void UIDebug(string message)
        {
            if (Core.Instance?.EnableDebugLogging == true)
            {
                MelonLogger.Msg($"[UI] {message}");
            }
        }

        /// <summary>
        /// Log mod initialization
        /// </summary>
        public static void LogInitialization()
        {
            Info($"Initializing {Constants.MOD_NAME} v{Constants.MOD_VERSION} by {Constants.MOD_AUTHORS}");
            Info(Constants.MOD_DESCRIPTION);
        }

        /// <summary>
        /// Log vehicle fuel information
        /// </summary>
        public static void LogVehicleFuel(string vehicleCode, string vehicleGUID, float currentFuel, float maxCapacity)
        {
            if (Core.Instance?.EnableDebugLogging == true)
            {
                MelonLogger.Msg($"[FUEL] {vehicleCode} ({vehicleGUID.Substring(0, 8)}...): {currentFuel:F1}L / {maxCapacity:F1}L ({(currentFuel / maxCapacity * 100):F1}%)");
            }
        }

        /// <summary>
        /// Log fuel consumption event
        /// </summary>
        public static void LogFuelConsumption(string vehicleGUID, float consumed, float remaining)
        {
            if (Core.Instance?.EnableDebugLogging == true)
            {
                MelonLogger.Msg($"[FUEL] Vehicle {vehicleGUID.Substring(0, 8)}... consumed {consumed:F3}L, remaining: {remaining:F1}L");
            }
        }

        /// <summary>
        /// Log fuel warning
        /// </summary>
        public static void LogFuelWarning(string vehicleGUID, float fuelLevel, string warningType)
        {
            MelonLogger.Warning($"[FUEL] Vehicle {vehicleGUID.Substring(0, 8)}... {warningType} fuel warning: {fuelLevel:F1}L");
        }
    }
}
