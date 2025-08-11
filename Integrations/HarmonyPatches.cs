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
    /// 
    /// IL2CPP Compatibility Notes:
    /// - JSON parsing uses explicit token type checking instead of "as" casting
    /// - JArray/JObject casting is handled differently for IL2CPP vs Mono
    /// - All JSON operations include try-catch blocks for error handling
    /// - FuelVehicleData serialization uses field injection rather than inheritance
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
                
                // Get vehicles array in a way that works with both Mono and IL2CPP
                JToken vehiclesToken;
                if (!root.TryGetValue("Vehicles", out vehiclesToken))
                    return;

                JArray vehicles = null;
                bool vehiclesNeedsReplacement = false;
                try
                {
                    if (vehiclesToken is JArray directArray)
                    {
                        vehicles = directArray;
                    }
                    else if (vehiclesToken.Type == JTokenType.Array)
                    {
#if MONO
                        vehicles = vehiclesToken as JArray;
#else
                        // In IL2CPP, we need to create a new array and replace it back
                        vehicles = JArray.Parse(vehiclesToken.ToString());
                        vehiclesNeedsReplacement = true;
                        ModLogger.FuelDebug("GetSaveString: IL2CPP - Created disconnected vehicles array, will replace back");
#endif
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"GetSaveString: Error parsing vehicles array: {ex.Message}");
                    return;
                }

                if (vehicles == null)
                    return;

                bool anyFuelDataAdded = false;
                for (int i = 0; i < vehicles.Count; i++)
                {
                    var vehToken = vehicles[i];
                    JObject vehObj = null;
                    bool needsReplacement = false;
                    
                    try
                    {
                        if (vehToken is JObject directObject)
                        {
                            vehObj = directObject;
                        }
                        else if (vehToken.Type == JTokenType.Object)
                        {
#if MONO
                            vehObj = vehToken as JObject;
#else
                            // In IL2CPP, we need to create a new object and replace it back
                            vehObj = JObject.Parse(vehToken.ToString());
                            needsReplacement = true;
                            ModLogger.FuelDebug($"GetSaveString: IL2CPP - Created disconnected vehicle object {i}, will replace back");
#endif
                        }
                        
                        if (vehObj == null)
                            continue;
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"GetSaveString: Error parsing vehicle {i}: {ex.Message}");
                        continue;
                    }

                    var guid = vehObj.TryGetValue("GUID", out var guidTok) ? (string)guidTok : null;
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    LandVehicle? vehicle = FindVehicleByGuid(guid);
                    if (vehicle == null)
                        continue;

                    var fuelSystem = vehicle.GetComponent<VehicleFuelSystem>();
                    float currentLevel, maxCapacity, consumptionRate;
                    if (fuelSystem != null)
                    {
#if MONO
                        FuelData fuelData = fuelSystem.GetFuelData();
                        currentLevel = fuelData.CurrentFuelLevel;
                        maxCapacity = fuelData.MaxFuelCapacity;
                        consumptionRate = fuelData.FuelConsumptionRate;
#else
                        fuelSystem.GetFuelDataValues(out currentLevel, out maxCapacity, out consumptionRate);
#endif
                    }
                    else
                    {
                        currentLevel = _modInstance.DefaultFuelCapacity * 0.75f;
                        maxCapacity = _modInstance.DefaultFuelCapacity;
                        consumptionRate = Constants.Fuel.BASE_CONSUMPTION_RATE;
                    }

                    // Stamp type for readability
                    vehObj["DataType"] = "FuelVehicleData";
                    // Inject fields
                    vehObj["CurrentFuelLevel"] = currentLevel;
                    vehObj["MaxFuelCapacity"] = maxCapacity;
                    vehObj["FuelConsumptionRate"] = consumptionRate;
                    vehObj["FuelDataVersion"] = 1;
                    anyFuelDataAdded = true;

#if !MONO
                    // In IL2CPP, we need to replace the modified object back into the vehicles array
                    if (needsReplacement)
                    {
                        vehicles[i] = vehObj;
                        ModLogger.FuelDebug($"GetSaveString: IL2CPP - Replaced vehicle object {i} back into vehicles array");
                    }
#endif

                    ModLogger.FuelDebug($"GetSaveString: Injected fuel data for vehicle {guid.Substring(0, 8)}... - {currentLevel:F1}L/{maxCapacity:F1}L");
                }

                if (anyFuelDataAdded)
                {
#if !MONO
                    // In IL2CPP, we need to replace the vehicles array back into the root object
                    if (vehiclesNeedsReplacement)
                    {
                        root["Vehicles"] = vehicles;
                        ModLogger.FuelDebug("GetSaveString: IL2CPP - Replaced vehicles array back into root object");
                    }
#endif

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

                ModLogger.FuelDebug($"LandVehicle_Load_Postfix: Loading fuel data for vehicle {__instance.GUID.ToString().Substring(0, 8)}...");

                // Check if the vehicle data contains fuel information
#if MONO
                FuelData? fuelData = FuelVehicleData.TryGetFuelData(data);
                if (fuelData != null)
                {
                    // Apply loaded fuel data
                    fuelSystem.LoadFuelData(fuelData);
                    ModLogger.FuelDebug($"Applied saved fuel data: {fuelData.CurrentFuelLevel:F1}L/{fuelData.MaxFuelCapacity:F1}L");
                }
                else
                {
                    ModLogger.FuelDebug($"LandVehicle_Load_Postfix: No fuel data found in VehicleData for {__instance.GUID.ToString().Substring(0, 8)}...");
                    // No saved fuel data found - set a realistic default level for vehicles saved before the mod
                    // But delay this so VehiclesLoader_Load_Postfix has a chance to load the fuel data from JSON
                    if (fuelSystem.CurrentFuelLevel == fuelSystem.MaxFuelCapacity)
                    {
                        ModLogger.FuelDebug($"LandVehicle_Load_Postfix: Vehicle at max fuel, will apply random level after VehiclesLoader processes");
                        // We'll set this in VehiclesLoader_Load_Postfix if no fuel data is found there
                    }
                }
#endif
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

                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Processing {mainPath}");
#if MONO
                ModLogger.FuelDebug("VehiclesLoader_Load_Postfix: Running on Mono");
#else
                ModLogger.FuelDebug("VehiclesLoader_Load_Postfix: Running on IL2CPP");
#endif

                // Determine actual file path (Loader.TryLoadFile appends .json by default)
                string jsonPath = File.Exists(mainPath) ? mainPath : mainPath + ".json";
                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Checking mainPath exists: {File.Exists(mainPath)}");
                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Checking jsonPath exists: {File.Exists(jsonPath)}");
                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Final jsonPath: {jsonPath}");
                
                if (!File.Exists(jsonPath))
                {
                    ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: JSON file not found: {jsonPath}");
                    
                    // Check if it's a directory and list files in it
                    if (Directory.Exists(mainPath))
                    {
                        var files = Directory.GetFiles(mainPath, "*.json");
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Directory exists, JSON files found: {string.Join(", ", files)}");
                        
                        // Try to find a file that contains vehicle data
                        foreach (var file in files)
                        {
                            try
                            {
                                var fileContents = File.ReadAllText(file);
                                if (fileContents.Contains("\"Vehicles\"") && fileContents.Contains("FuelVehicleData"))
                                {
                                    ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Found potential fuel data file: {file}");
                                    jsonPath = file;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Error reading file {file}: {ex.Message}");
                            }
                        }
                        
                        if (!File.Exists(jsonPath))
                        {
                            ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: No suitable JSON file found in directory");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                string contents = File.ReadAllText(jsonPath);
                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: File contents (first 500 chars): {contents.Substring(0, Math.Min(500, contents.Length))}");
                
                var root = JObject.Parse(contents);

                // More robust way to get vehicles array that works with both Mono and IL2CPP
                JToken vehiclesToken;
                if (!root.TryGetValue("Vehicles", out vehiclesToken))
                {
                    ModLogger.FuelDebug("VehiclesLoader_Load_Postfix: No Vehicles property found in JSON");
                    return;
                }

                // Handle both JArray and generic JToken for IL2CPP compatibility
                JArray vehicles = null;
                try
                {
                    if (vehiclesToken is JArray directArray)
                    {
                        vehicles = directArray;
                        ModLogger.FuelDebug("VehiclesLoader_Load_Postfix: Got vehicles as direct JArray");
                    }
                    else if (vehiclesToken.Type == JTokenType.Array)
                    {
                        // For IL2CPP, try to parse as JArray
#if MONO
                        vehicles = vehiclesToken as JArray;
#else
                        vehicles = JArray.Parse(vehiclesToken.ToString());
#endif
                        ModLogger.FuelDebug("VehiclesLoader_Load_Postfix: Converted vehicles token to JArray");
                    }
                    else
                    {
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicles token is not an array. Type: {vehiclesToken.Type}, Value: {vehiclesToken}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"VehiclesLoader_Load_Postfix: Error parsing vehicles array: {ex.Message}");
                    return;
                }

                if (vehicles == null)
                {
                    ModLogger.FuelDebug("VehiclesLoader_Load_Postfix: Failed to get vehicles array");
                    return;
                }

                ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Found {vehicles.Count} vehicles in JSON");

                // Get all spawned vehicles to apply fuel data to
                var allSpawnedVehicles = new List<LandVehicle>();
                if (NetworkSingleton<VehicleManager>.InstanceExists)
                {
                    foreach (LandVehicle vehicle in NetworkSingleton<VehicleManager>.Instance.AllVehicles)
                    {
                        if (vehicle != null)
                            allSpawnedVehicles.Add(vehicle);
                    }
                }

                for (int i = 0; i < vehicles.Count; i++)
                {
                    var vehToken = vehicles[i];
                    JObject vehObj = null;
                    
                    // Handle JObject casting for both Mono and IL2CPP
                    try
                    {
                        if (vehToken is JObject directObject)
                        {
                            vehObj = directObject;
                        }
                        else if (vehToken.Type == JTokenType.Object)
                        {
#if MONO
                            vehObj = vehToken as JObject;
#else
                            vehObj = JObject.Parse(vehToken.ToString());
#endif
                        }
                        
                        if (vehObj == null)
                        {
                            ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicle {i} is not a JObject. Type: {vehToken.Type}");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"VehiclesLoader_Load_Postfix: Error parsing vehicle {i}: {ex.Message}");
                        continue;
                    }

                    string guid = vehObj.TryGetValue("GUID", out var guidTok2) ? (string)guidTok2 ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(guid))
                    {
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicle {i} has no GUID");
                        continue;
                    }

                    ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Processing vehicle {guid.Substring(0, 8)}...");

                    // Read fuel fields if present
                    if (!vehObj.TryGetValue("CurrentFuelLevel", out var curTok))
                    {
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicle {guid.Substring(0, 8)}... has no CurrentFuelLevel field");
                        continue;
                    }

                    float current = (float)curTok;
                    float max = vehObj.TryGetValue("MaxFuelCapacity", out var maxTok) ? (float)maxTok : _modInstance.DefaultFuelCapacity;
                    float rate = vehObj.TryGetValue("FuelConsumptionRate", out var rateTok) ? (float)rateTok : Constants.Fuel.BASE_CONSUMPTION_RATE;

                    ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicle {guid.Substring(0, 8)}... found fuel data: {current:F1}L/{max:F1}L");

                    // Find spawned vehicle and apply
                    LandVehicle? vehicle = FindVehicleByGuid(guid);
                    if (vehicle == null)
                    {
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicle {guid.Substring(0, 8)}... not found in scene");
                        continue;
                    }

                    var fuelManager = _modInstance.GetFuelSystemManager();
                    var fuelSystem = fuelManager?.AddFuelSystemToVehicle(vehicle);
                    if (fuelSystem == null)
                    {
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Vehicle {guid.Substring(0, 8)}... could not get fuel system");
                        continue;
                    }

                    fuelSystem.SetMaxCapacity(max);
                    fuelSystem.SetFuelLevel(current);
                    // Update base consumption (if you want this persisted)
                    // We don't have setter, but LoadFuelData covers it if needed

                    ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: Applied saved fuel to {guid.Substring(0, 8)}... {current:F1}/{max:F1}L");
                    
                    // Remove this vehicle from the list so we know we processed it
                    allSpawnedVehicles.RemoveAll(v => v.GUID.ToString() == guid);
                }

                // Handle any remaining vehicles that didn't have fuel data in the JSON
                foreach (var vehicle in allSpawnedVehicles)
                {
                    var fuelManager = _modInstance.GetFuelSystemManager();
                    var fuelSystem = fuelManager?.GetFuelSystem(vehicle);
                    
                    if (fuelSystem != null && fuelSystem.CurrentFuelLevel == fuelSystem.MaxFuelCapacity)
                    {
                        fuelSystem.SetFuelLevel(fuelSystem.MaxFuelCapacity);
                        ModLogger.FuelDebug($"VehiclesLoader_Load_Postfix: No saved fuel data for {vehicle.GUID.ToString().Substring(0, 8)}... - set max fuel level: {fuelSystem.MaxFuelCapacity:F1}L");
                    }
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