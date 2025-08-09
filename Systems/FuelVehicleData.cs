using System;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Vehicles.Modification;
using UnityEngine;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Extended VehicleData that includes fuel information
    /// This extends the original VehicleData class to add fuel persistence without breaking compatibility
    /// </summary>
    [Serializable]
    public class FuelVehicleData : VehicleData
    {
        // Fuel-specific fields that will be serialized by Unity JsonUtility
        public float CurrentFuelLevel;
        public float MaxFuelCapacity;
        public float FuelConsumptionRate;

        // Version field to help with future migrations
        public int FuelDataVersion = 1;

        /// <summary>
        /// Constructor that matches the original VehicleData constructor and adds fuel data
        /// </summary>
        public FuelVehicleData(Guid guid, string code, Vector3 pos, Quaternion rot, EVehicleColor col, ItemSet vehicleContents, FuelData fuelData)
            : base(guid, code, pos, rot, col, vehicleContents)
        {
            if (fuelData != null)
            {
                CurrentFuelLevel = fuelData.CurrentFuelLevel;
                MaxFuelCapacity = fuelData.MaxFuelCapacity;
                FuelConsumptionRate = fuelData.FuelConsumptionRate;
            }
            else
            {
                // Default values if no fuel data provided
                CurrentFuelLevel = 50f;
                MaxFuelCapacity = 50f;
                FuelConsumptionRate = 6f;
            }
        }

        /// <summary>
        /// Extract fuel data from this vehicle data
        /// </summary>
        public FuelData GetFuelData()
        {
            return new FuelData
            {
                CurrentFuelLevel = CurrentFuelLevel,
                MaxFuelCapacity = MaxFuelCapacity,
                FuelConsumptionRate = FuelConsumptionRate
            };
        }

        /// <summary>
        /// Check if this vehicle data contains fuel information
        /// </summary>
        public static bool HasFuelData(VehicleData vehicleData)
        {
            return vehicleData is FuelVehicleData;
        }

        /// <summary>
        /// Try to extract fuel data from any VehicleData (works with both VehicleData and FuelVehicleData)
        /// </summary>
        public static FuelData? TryGetFuelData(VehicleData vehicleData)
        {
            if (vehicleData is FuelVehicleData fuelVehicleData)
            {
                return fuelVehicleData.GetFuelData();
            }
            return null;
        }

        /// <summary>
        /// Create a FuelVehicleData from regular VehicleData and fuel data
        /// </summary>
        public static FuelVehicleData FromVehicleData(VehicleData vehicleData, FuelData fuelData)
        {
            return new FuelVehicleData(
                new Guid(vehicleData.GUID),
                vehicleData.VehicleCode,
                vehicleData.Position,
                vehicleData.Rotation,
                Enum.Parse<EVehicleColor>(vehicleData.Color),
                vehicleData.VehicleContents,
                fuelData
            );
        }
    }
}