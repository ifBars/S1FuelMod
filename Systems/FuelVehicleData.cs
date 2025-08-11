using MelonLoader;
#if MONO
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Vehicles.Modification;
#else
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.Vehicles.Modification;
using Il2CppInterop.Runtime.Attributes;
#endif
using UnityEngine;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Extended VehicleData that includes fuel information
    /// This extends the original VehicleData class to add fuel persistence without breaking compatibility
    /// </summary>
    [Serializable]
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class FuelVehicleData : VehicleData
    {
        // Fuel-specific fields that will be serialized by Unity JsonUtility
        public float CurrentFuelLevel;
        public float MaxFuelCapacity;
        public float FuelConsumptionRate;

        // Version field to help with future migrations
        public int FuelDataVersion = 1;

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public FuelVehicleData(IntPtr ptr) : base(ptr) { }
#endif

        /// <summary>
        /// Constructor that matches the original VehicleData constructor and adds fuel data
        /// </summary>
        #if !MONO
        public FuelVehicleData(Il2CppSystem.Guid guid, string code, Vector3 pos, Quaternion rot, EVehicleColor col, ItemSet vehicleContents, FuelData fuelData)
            : base(guid, code, pos, rot, col, vehicleContents)
#else
        public FuelVehicleData(Guid guid, string code, Vector3 pos, Quaternion rot, EVehicleColor col, ItemSet vehicleContents, FuelData fuelData)
            : base(guid, code, pos, rot, col, vehicleContents)
#endif
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
#if !MONO
        [HideFromIl2Cpp]
#endif
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
        /// Get fuel data values (IL2CPP compatible version)
        /// </summary>
        /// <param name="currentLevel">Output: current fuel level</param>
        /// <param name="maxCapacity">Output: maximum fuel capacity</param>
        /// <param name="consumptionRate">Output: fuel consumption rate</param>
        public void GetFuelDataValues(out float currentLevel, out float maxCapacity, out float consumptionRate)
        {
            currentLevel = CurrentFuelLevel;
            maxCapacity = MaxFuelCapacity;
            consumptionRate = FuelConsumptionRate;
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
#if !MONO
        [HideFromIl2Cpp]
#endif
        public static FuelData? TryGetFuelData(VehicleData vehicleData)
        {
            if (vehicleData is FuelVehicleData fuelVehicleData)
            {
                return fuelVehicleData.GetFuelData();
            }
            return null;
        }

        /// <summary>
        /// Try to get fuel data values from any VehicleData (IL2CPP compatible version)
        /// </summary>
        /// <param name="vehicleData">Vehicle data to check</param>
        /// <param name="currentLevel">Output: current fuel level</param>
        /// <param name="maxCapacity">Output: maximum fuel capacity</param>
        /// <param name="consumptionRate">Output: fuel consumption rate</param>
        /// <returns>True if fuel data was found, false otherwise</returns>
#if !MONO
        [HideFromIl2Cpp]
#endif
        public static bool TryGetFuelDataValues(VehicleData vehicleData, out float currentLevel, out float maxCapacity, out float consumptionRate)
        {
            if (vehicleData is FuelVehicleData fuelVehicleData)
            {
                fuelVehicleData.GetFuelDataValues(out currentLevel, out maxCapacity, out consumptionRate);
                return true;
            }
            
            currentLevel = 0f;
            maxCapacity = 0f;
            consumptionRate = 0f;
            return false;
        }

        /// <summary>
        /// Create a FuelVehicleData from regular VehicleData and fuel data
        /// </summary>
        public static FuelVehicleData FromVehicleData(VehicleData vehicleData, FuelData fuelData)
        {
            return new FuelVehicleData(
#if MONO
                new Guid(vehicleData.GUID),
#else
                new Il2CppSystem.Guid(vehicleData.GUID),
#endif
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