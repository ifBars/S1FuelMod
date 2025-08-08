using System;
using System.Collections.Generic;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Vehicles;
using ScheduleOne.Vehicles.Modification;
using S1FuelMod.Utils;
using UnityEngine;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Manages persistence of fuel data for vehicles
    /// </summary>
    public class FuelPersistenceManager
    {
        private readonly FuelSystemManager _fuelSystemManager;
        private readonly Dictionary<string, FuelData> _pendingSaveData = new Dictionary<string, FuelData>();
        private readonly Dictionary<string, FuelData> _loadedFuelData = new Dictionary<string, FuelData>();

        public FuelPersistenceManager(FuelSystemManager fuelSystemManager)
        {
            _fuelSystemManager = fuelSystemManager ?? throw new ArgumentNullException(nameof(fuelSystemManager));
            ModLogger.Info("FuelPersistenceManager: Initialized");
        }

        /// <summary>
        /// Extract fuel data from vehicle and prepare it for saving
        /// </summary>
        /// <param name="vehicle">Vehicle to extract fuel data from</param>
        /// <returns>Fuel data or null if not available</returns>
        public FuelData? ExtractFuelData(LandVehicle vehicle)
        {
            try
            {
                if (vehicle == null) return null;

                var fuelSystem = vehicle.GetComponent<VehicleFuelSystem>();
                if (fuelSystem == null)
                {
                    ModLogger.Warning($"FuelPersistence: No fuel system found for vehicle {vehicle.GUID}");
                    return null;
                }

                var fuelData = fuelSystem.GetFuelData();
                ModLogger.FuelDebug($"FuelPersistence: Extracted fuel data for vehicle {vehicle.GUID.ToString().Substring(0, 8)}... - " +
                                   $"Fuel: {fuelData.CurrentFuelLevel:F1}L/{fuelData.MaxFuelCapacity:F1}L");

                return fuelData;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error extracting fuel data for vehicle {vehicle?.GUID}", ex);
                return null;
            }
        }

        /// <summary>
        /// Apply loaded fuel data to a vehicle
        /// </summary>
        /// <param name="vehicle">Vehicle to apply fuel data to</param>
        /// <param name="fuelData">Fuel data to apply</param>
        public void ApplyFuelData(LandVehicle vehicle, FuelData fuelData)
        {
            try
            {
                if (vehicle == null || fuelData == null) return;

                // Ensure vehicle has fuel system
                var fuelSystem = _fuelSystemManager.AddFuelSystemToVehicle(vehicle);
                if (fuelSystem == null)
                {
                    ModLogger.Error($"FuelPersistence: Could not add fuel system to vehicle {vehicle.GUID}");
                    return;
                }

                // Apply the loaded fuel data
                fuelSystem.LoadFuelData(fuelData);
                ModLogger.FuelDebug($"FuelPersistence: Applied fuel data to vehicle {vehicle.GUID.ToString().Substring(0, 8)}... - " +
                                   $"Fuel: {fuelData.CurrentFuelLevel:F1}L/{fuelData.MaxFuelCapacity:F1}L");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error applying fuel data to vehicle {vehicle?.GUID}", ex);
            }
        }

        /// <summary>
        /// Inject fuel data into VehicleData JSON string
        /// </summary>
        /// <param name="vehicleDataJson">Original VehicleData JSON</param>
        /// <param name="vehicle">Vehicle to get fuel data from</param>
        /// <returns>Enhanced JSON with fuel data</returns>
        public string InjectFuelDataIntoJson(string vehicleDataJson, LandVehicle vehicle)
        {
            try
            {
                if (string.IsNullOrEmpty(vehicleDataJson) || vehicle == null)
                    return vehicleDataJson;

                var fuelData = ExtractFuelData(vehicle);
                if (fuelData == null)
                    return vehicleDataJson;

                // Parse the original VehicleData JSON using Unity JsonUtility (avoids dynamic types)
                var originalVehicleData = JsonUtility.FromJson<VehicleData>(vehicleDataJson);
                if (originalVehicleData == null)
                    return vehicleDataJson;

                // Create extended vehicle data object by copying properties from the concrete VehicleData
                var extendedData = new ExtendedVehicleData
                {
                    // Copy original VehicleData properties
                    GUID = originalVehicleData.GUID,
                    VehicleCode = originalVehicleData.VehicleCode,
                    Position = originalVehicleData.Position,
                    Rotation = originalVehicleData.Rotation,
                    Color = originalVehicleData.Color,
                    VehicleContents = originalVehicleData.VehicleContents,
                    DataType = originalVehicleData.DataType,
                    DataVersion = originalVehicleData.DataVersion,
                    GameVersion = originalVehicleData.GameVersion,

                    // Add fuel data
                    FuelData = fuelData
                };

                string enhancedJson = JsonUtility.ToJson(extendedData, true);
                ModLogger.FuelDebug($"FuelPersistence: Injected fuel data into vehicle JSON for {vehicle.GUID.ToString().Substring(0, 8)}...");

                return enhancedJson;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error injecting fuel data into JSON for vehicle {vehicle?.GUID}", ex);
                return vehicleDataJson; // Return original JSON on error
            }
        }

        /// <summary>
        /// Extract fuel data from enhanced VehicleData JSON string
        /// </summary>
        /// <param name="vehicleDataJson">Enhanced VehicleData JSON that may contain fuel data</param>
        /// <returns>Extracted fuel data or null if not found</returns>
        public FuelData? ExtractFuelDataFromJson(string vehicleDataJson)
        {
            try
            {
                if (string.IsNullOrEmpty(vehicleDataJson))
                    return null;

                // Try to parse as ExtendedVehicleData first using Unity JsonUtility
                var extendedData = JsonUtility.FromJson<ExtendedVehicleData>(vehicleDataJson);
                if (extendedData?.FuelData != null)
                {
                    ModLogger.FuelDebug($"FuelPersistence: Extracted fuel data from JSON - " +
                                       $"Fuel: {extendedData.FuelData.CurrentFuelLevel:F1}L/{extendedData.FuelData.MaxFuelCapacity:F1}L");
                    return extendedData.FuelData;
                }

                // If no fuel data found, return null (this is normal for vehicles saved before the mod)
                ModLogger.FuelDebug("FuelPersistence: No fuel data found in JSON (likely pre-mod save)");
                return null;
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"FuelPersistence: Could not extract fuel data from JSON (normal for pre-mod saves): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Store fuel data for a vehicle GUID to be used during loading
        /// </summary>
        /// <param name="vehicleGuid">Vehicle GUID</param>
        /// <param name="fuelData">Fuel data to store</param>
        public void StoreFuelDataForLoading(string vehicleGuid, FuelData fuelData)
        {
            if (string.IsNullOrEmpty(vehicleGuid) || fuelData == null) return;

            _loadedFuelData[vehicleGuid] = fuelData;
            ModLogger.FuelDebug($"FuelPersistence: Stored fuel data for loading vehicle {vehicleGuid.Substring(0, 8)}...");
        }

        /// <summary>
        /// Retrieve and consume stored fuel data for a vehicle GUID
        /// </summary>
        /// <param name="vehicleGuid">Vehicle GUID</param>
        /// <returns>Stored fuel data or null if not found</returns>
        public FuelData? ConsumeStoredFuelData(string vehicleGuid)
        {
            if (string.IsNullOrEmpty(vehicleGuid)) return null;

            if (_loadedFuelData.TryGetValue(vehicleGuid, out FuelData fuelData))
            {
                _loadedFuelData.Remove(vehicleGuid);
                ModLogger.FuelDebug($"FuelPersistence: Consumed stored fuel data for vehicle {vehicleGuid.Substring(0, 8)}...");
                return fuelData;
            }

            return null;
        }

        /// <summary>
        /// Clear all stored fuel data (useful when loading a new save)
        /// </summary>
        public void ClearStoredFuelData()
        {
            _loadedFuelData.Clear();
            _pendingSaveData.Clear();
            ModLogger.FuelDebug("FuelPersistence: Cleared all stored fuel data");
        }

        /// <summary>
        /// Get statistics about stored fuel data
        /// </summary>
        public FuelPersistenceStats GetStatistics()
        {
            return new FuelPersistenceStats
            {
                StoredFuelDataCount = _loadedFuelData.Count,
                PendingSaveDataCount = _pendingSaveData.Count
            };
        }
    }

    /// <summary>
    /// Extended vehicle data that includes fuel information
    /// This allows us to store fuel data alongside regular vehicle data without modifying the original classes
    /// </summary>
    [Serializable]
    public class ExtendedVehicleData
    {
        // Original VehicleData fields (using public fields for Unity JsonUtility compatibility)
        public string GUID = string.Empty;
        public string VehicleCode = string.Empty;
        public Vector3 Position;
        public Quaternion Rotation;
        public string Color = string.Empty;
        public ItemSet VehicleContents;
        public string DataType = string.Empty;
        public int DataVersion;
        public string GameVersion = string.Empty;

        // Extended fields for fuel mod
        public FuelData FuelData;
    }

    /// <summary>
    /// Statistics about fuel persistence system
    /// </summary>
    public class FuelPersistenceStats
    {
        public int StoredFuelDataCount { get; set; }
        public int PendingSaveDataCount { get; set; }
    }
}
