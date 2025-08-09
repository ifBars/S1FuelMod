using HarmonyLib;
using S1FuelMod.Systems;
using S1FuelMod.Utils;
#if MONO
using Newtonsoft.Json.Linq;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Persistence.Loaders;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
#else
using Il2CppNewtonsoft.Json.Linq;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.Persistence.Loaders;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
#endif
using UnityEngine;

namespace S1FuelMod.Integrations
{
    /// <summary>
    /// Harmony patches for integrating with Schedule I's vehicle system
    /// </summary>
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        private static Core? _modInstance;

        /// <summary>
        /// Set the mod instance for patch callbacks
        /// </summary>
        public static void SetModInstance(Core modInstance)
        {
            _modInstance = modInstance;
        }

        /// <summary>
        /// Patch LandVehicle.ApplyThrottle to integrate fuel consumption and engine cutoff
        /// </summary>
        [HarmonyPatch(typeof(LandVehicle), "ApplyThrottle")]
        [HarmonyPrefix]
        public static bool LandVehicle_ApplyThrottle_Prefix(LandVehicle __instance)
        {
            try
            {
                // Only interfere if fuel system is enabled
                if (_modInstance?.EnableFuelSystem != true)
                {
                    return true; // Continue with original method
                }

                // Get fuel system for this vehicle
                VehicleFuelSystem? fuelSystem = __instance.GetComponent<VehicleFuelSystem>();
                if (fuelSystem == null)
                {
                    // Try to add fuel system if it doesn't exist
                    var fuelManager = _modInstance.GetFuelSystemManager();
                    fuelSystem = fuelManager?.AddFuelSystemToVehicle(__instance);

                    if (fuelSystem == null)
                    {
                        return true; // Continue with original method if we can't add fuel system
                    }
                }

                // Check if vehicle is out of fuel
                if (fuelSystem.IsOutOfFuel)
                {
                    // Prevent engine from running when out of fuel
                    ModLogger.FuelDebug($"Vehicle {fuelSystem.VehicleGUID.Substring(0, 8)}... engine disabled - out of fuel");

                    // Zero out the throttle input
                    __instance.currentThrottle = 0f;

                    // Apply engine braking/coasting behavior
                    ApplyCoastingBehavior(__instance);

                    return false; // Skip original method
                }

                // Allow normal operation if fuel is available
                return true; // Continue with original method
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in LandVehicle.ApplyThrottle prefix", ex);
                return true; // Continue with original method on error
            }
        }

        /// <summary>
        /// Apply coasting behavior when engine is off due to no fuel
        /// </summary>
        private static void ApplyCoastingBehavior(LandVehicle vehicle)
        {
            try
            {
                // Apply gradual deceleration
                if (vehicle.Rb != null && !vehicle.Rb.isKinematic)
                {
                    // Apply drag force to simulate engine braking
                    Vector3 velocity = vehicle.Rb.velocity;
                    Vector3 dragForce = -velocity.normalized * Mathf.Min(velocity.magnitude * 2f, 50f);
                    vehicle.Rb.AddForce(dragForce, ForceMode.Force);
                }

                // Disable motor torque on all wheels
                foreach (var wheel in vehicle.wheels)
                {
                    if (wheel?.wheelCollider != null)
                    {
                        wheel.wheelCollider.motorTorque = 0f;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error applying coasting behavior", ex);
            }
        }

        /// <summary>
        /// Patch VehicleManager.SpawnAndReturnVehicle to add fuel systems to new vehicles
        /// </summary>
        [HarmonyPatch(typeof(VehicleManager), "SpawnAndReturnVehicle")]
        [HarmonyPostfix]
        public static void VehicleManager_SpawnAndReturnVehicle_Postfix(LandVehicle __result)
        {
            try
            {
                if (_modInstance?.EnableFuelSystem != true || __result == null)
                    return;

                // Add fuel system to newly spawned vehicle
                var fuelManager = _modInstance.GetFuelSystemManager();
                fuelManager?.AddFuelSystemToVehicle(__result);

                ModLogger.FuelDebug($"Added fuel system to newly spawned vehicle: {__result.VehicleName}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleManager.SpawnAndReturnVehicle postfix", ex);
            }
        }

        /// <summary>
        /// Patch Player.EnterVehicle to show fuel gauge when entering vehicle
        /// </summary>
        [HarmonyPatch(typeof(Player), "EnterVehicle")]
        [HarmonyPostfix]
        public static void Player_EnterVehicle_Postfix(Player __instance, LandVehicle vehicle)
        {
            try
            {
                if (_modInstance?.ShowFuelGauge != true || vehicle == null)
                    return;

                // Only handle local player
                if (__instance != Player.Local)
                    return;

                ModLogger.UIDebug($"Player entered vehicle: {vehicle.VehicleName}");

                // UI manager will handle showing the gauge in its Update method
                // through vehicle change detection
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in Player.EnterVehicle postfix", ex);
            }
        }

        /// <summary>
        /// Patch Player.ExitVehicle to hide fuel gauge when exiting vehicle
        /// </summary>
        [HarmonyPatch(typeof(Player), "ExitVehicle")]
        [HarmonyPostfix]
        public static void Player_ExitVehicle_Postfix(Player __instance, Transform exitPoint)
        {
            try
            {
                if (_modInstance?.ShowFuelGauge != true)
                    return;

                // Only handle local player
                if (__instance != Player.Local)
                    return;

                ModLogger.UIDebug("Player exited vehicle");

                // UI manager will handle hiding the gauge in its Update method
                // through vehicle change detection
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in Player.ExitVehicle postfix", ex);
            }
        }

        /// <summary>
        /// Patch VehicleManager.GetSaveString to inject fuel data into the JSON after serialization
        /// Uses JObject so extra fields persist even though Unity's JsonUtility doesn't support polymorphism
        /// </summary>
        [HarmonyPatch(typeof(VehicleManager), "GetSaveString")]
        [HarmonyPostfix]
        public static void VehicleManager_GetSaveString_Postfix(VehicleManager __instance, ref string __result)
        {
            try
            {
                if (_modInstance?.EnableFuelSystem != true || __instance == null || string.IsNullOrEmpty(__result))
                    return;

                var root = JObject.Parse(__result);
                var vehicles = root["Vehicles"] as JArray;
                if (vehicles == null)
                    return;

                bool anyFuelDataAdded = false;
                for (int i = 0; i < vehicles.Count; i++)
                {
                    var vehToken = vehicles[i];
                    if (vehToken is not JObject vehObj)
                        continue;

                    var guid = vehObj.TryGetValue("GUID", out var guidTok) ? (string)guidTok : null;
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    LandVehicle? vehicle = FindVehicleByGuid(guid);
                    if (vehicle == null)
                        continue;

                    var fuelSystem = vehicle.GetComponent<VehicleFuelSystem>();
                    FuelData fuelData = fuelSystem?.GetFuelData() ?? new FuelData
                    {
                        CurrentFuelLevel = _modInstance.DefaultFuelCapacity * 0.75f,
                        MaxFuelCapacity = _modInstance.DefaultFuelCapacity,
                        FuelConsumptionRate = Constants.Fuel.BASE_CONSUMPTION_RATE
                    };

                    // Stamp type for readability
                    vehObj["DataType"] = "FuelVehicleData";
                    // Inject fields
                    vehObj["CurrentFuelLevel"] = fuelData.CurrentFuelLevel;
                    vehObj["MaxFuelCapacity"] = fuelData.MaxFuelCapacity;
                    vehObj["FuelConsumptionRate"] = fuelData.FuelConsumptionRate;
                    vehObj["FuelDataVersion"] = 1;
                    anyFuelDataAdded = true;

                    ModLogger.FuelDebug($"GetSaveString: Injected fuel data for vehicle {guid.Substring(0, 8)}... - {fuelData.CurrentFuelLevel:F1}L/{fuelData.MaxFuelCapacity:F1}L");
                }

                if (anyFuelDataAdded)
                {
#if MONO
                    __result = root.ToString(Newtonsoft.Json.Formatting.Indented);
#else
                    __result = root.ToString(Il2CppNewtonsoft.Json.Formatting.Indented);
#endif
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleManager.GetSaveString postfix", ex);
            }
        }

        /// <summary>
        /// Helper method to find a vehicle by GUID
        /// </summary>
        private static LandVehicle? FindVehicleByGuid(string guid)
        {
            try
            {
                if (string.IsNullOrEmpty(guid)) return null;

                // Check VehicleManager if available
                if (NetworkSingleton<VehicleManager>.InstanceExists)
                {
                    var vehicleManager = NetworkSingleton<VehicleManager>.Instance;
                    foreach (LandVehicle vehicle in vehicleManager.AllVehicles)
                    {
                        if (vehicle != null && vehicle.GUID.ToString() == guid)
                        {
                            return vehicle;
                        }
                    }
                }

                // Fallback: search all LandVehicle objects in scene
                LandVehicle[] allVehicles = UnityEngine.Object.FindObjectsOfType<LandVehicle>();
                foreach (LandVehicle vehicle in allVehicles)
                {
                    if (vehicle != null && vehicle.GUID.ToString() == guid)
                    {
                        return vehicle;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error finding vehicle by GUID {guid}", ex);
                return null;
            }
        }

        /// <summary>
        /// Patch LandVehicle.Load to restore fuel data from saves
        /// </summary>
        [HarmonyPatch(typeof(LandVehicle), "Load")]
        [HarmonyPostfix]
        public static void LandVehicle_Load_Postfix(LandVehicle __instance, VehicleData data, string containerPath)
        {
            try
            {
                if (_modInstance?.EnableFuelSystem != true || __instance == null)
                    return;

                // Ensure vehicle has fuel system
                var fuelManager = _modInstance.GetFuelSystemManager();
                VehicleFuelSystem? fuelSystem = fuelManager?.AddFuelSystemToVehicle(__instance);

                if (fuelSystem == null)
                    return;

                ModLogger.FuelDebug($"Loading fuel data for vehicle {__instance.GUID.ToString().Substring(0, 8)}...");

                // Check if the vehicle data contains fuel information
                FuelData? fuelData = FuelVehicleData.TryGetFuelData(data);

                if (fuelData != null)
                {
                    // Apply loaded fuel data
                    fuelSystem.LoadFuelData(fuelData);
                    ModLogger.FuelDebug($"Applied saved fuel data: {fuelData.CurrentFuelLevel:F1}L/{fuelData.MaxFuelCapacity:F1}L");
                }
                else
                {
                    // No saved fuel data found - set a realistic default level for vehicles saved before the mod
                    if (fuelSystem.CurrentFuelLevel == fuelSystem.MaxFuelCapacity)
                    {
                        float randomFuelLevel = UnityEngine.Random.Range(0.2f, 1.0f) * fuelSystem.MaxFuelCapacity;
                        fuelSystem.SetFuelLevel(randomFuelLevel);
                        ModLogger.FuelDebug($"No saved fuel data - set random fuel level: {randomFuelLevel:F1}L");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in LandVehicle.Load postfix", ex);
            }
        }

        /// <summary>
        /// Patch VehiclesLoader.Load to read fuel data back from JSON and apply to spawned vehicles
        /// </summary>
        [HarmonyPatch(typeof(VehiclesLoader), "Load")]
        [HarmonyPostfix]
        public static void VehiclesLoader_Load_Postfix(string mainPath)
        {
            try
            {
                if (_modInstance?.EnableFuelSystem != true)
                    return;

                // Determine actual file path (Loader.TryLoadFile appends .json by default)
                string jsonPath = File.Exists(mainPath) ? mainPath : mainPath + ".json";
                if (!File.Exists(jsonPath))
                    return;

                string contents = File.ReadAllText(jsonPath);
                var root = JObject.Parse(contents);
                var vehicles = root["Vehicles"] as JArray;
                if (vehicles == null)
                    return;

                for (int i = 0; i < vehicles.Count; i++)
                {
                    var vehToken = vehicles[i];
                    if (vehToken is not JObject vehObj)
                        continue;

                    string guid = vehObj.TryGetValue("GUID", out var guidTok2) ? (string)guidTok2 ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    // Read fuel fields if present
                    if (!vehObj.TryGetValue("CurrentFuelLevel", out var curTok))
                        continue;

                    float current = (float)curTok;
                    float max = vehObj.TryGetValue("MaxFuelCapacity", out var maxTok) ? (float)maxTok : _modInstance.DefaultFuelCapacity;
                    float rate = vehObj.TryGetValue("FuelConsumptionRate", out var rateTok) ? (float)rateTok : Constants.Fuel.BASE_CONSUMPTION_RATE;

                    // Find spawned vehicle and apply
                    LandVehicle? vehicle = FindVehicleByGuid(guid);
                    if (vehicle == null)
                        continue;

                    var fuelManager = _modInstance.GetFuelSystemManager();
                    var fuelSystem = fuelManager?.AddFuelSystemToVehicle(vehicle);
                    if (fuelSystem == null)
                        continue;

                    fuelSystem.SetMaxCapacity(max);
                    fuelSystem.SetFuelLevel(current);
                    // Update base consumption (if you want this persisted)
                    // We don't have setter, but LoadFuelData covers it if needed

                    ModLogger.FuelDebug($"VehiclesLoader: Applied saved fuel to {guid.Substring(0, 8)}... {current:F1}/{max:F1}L");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehiclesLoader.Load postfix", ex);
            }
        }

        /// <summary>
        /// Optional: Patch LandVehicle.Update to add engine performance effects based on fuel level
        /// </summary>
        [HarmonyPatch(typeof(LandVehicle), "Update")]
        [HarmonyPostfix]
        public static void LandVehicle_Update_Postfix(LandVehicle __instance)
        {
            try
            {
                if (_modInstance?.EnableFuelSystem != true || __instance == null)
                    return;

                // Get fuel system for this vehicle
                VehicleFuelSystem? fuelSystem = __instance.GetComponent<VehicleFuelSystem>();
                if (fuelSystem == null)
                    return;

                // Apply performance effects based on fuel level
                ApplyFuelPerformanceEffects(__instance, fuelSystem);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in LandVehicle.Update postfix", ex);
            }
        }

        /// <summary>
        /// Apply performance effects based on fuel level
        /// </summary>
        private static void ApplyFuelPerformanceEffects(LandVehicle vehicle, VehicleFuelSystem fuelSystem)
        {
            try
            {
                // Engine stuttering when fuel is very low
                if (fuelSystem.CurrentFuelLevel <= Constants.Fuel.ENGINE_SPUTTER_FUEL_LEVEL &&
                    fuelSystem.CurrentFuelLevel > Constants.Fuel.ENGINE_CUTOFF_FUEL_LEVEL)
                {
                    // Randomly reduce throttle input to simulate engine stuttering
                    if (UnityEngine.Random.Range(0f, 1f) < 0.1f) // 10% chance per frame
                    {
                        vehicle.currentThrottle *= 0.5f; // Reduce throttle by half
                        // ModLogger.FuelDebug($"Vehicle {fuelSystem.VehicleGUID.Substring(0, 8)}... engine stuttering");
                    }
                }

                // TODO: Add other performance effects like:
                // - Reduced acceleration when fuel is low?
                // - Engine sound changes?
                // - Particle effects for exhaust?
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error applying fuel performance effects", ex);
            }
        }


    }
}