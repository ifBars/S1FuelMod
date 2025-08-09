using HarmonyLib;
using ScheduleOne.Vehicles;
using ScheduleOne.PlayerScripts;
using S1FuelMod.Utils;
using S1FuelMod.Systems;
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
            {
                ModLogger.Error("Error in Player.ExitVehicle postfix", ex);
            }
        }

        /// <summary>
        /// Patch LandVehicle.GetVehicleData to include fuel data in saves
        /// </summary>
        [HarmonyPatch(typeof(LandVehicle), "GetVehicleData")]
        [HarmonyPostfix]
        public static void LandVehicle_GetVehicleData_Postfix(LandVehicle __instance, ref ScheduleOne.Persistence.Datas.VehicleData __result)
        {
            try
            {
                if (_modInstance?.EnableFuelSystem != true || __instance == null)
                    return;

                // Get fuel system for this vehicle
                VehicleFuelSystem? fuelSystem = __instance.GetComponent<VehicleFuelSystem>();
                if (fuelSystem == null)
                    return;

                // Store fuel data in a way that doesn't break existing save system
                // We'll use a separate save mechanism for fuel data
                ModLogger.FuelDebug($"Saving fuel data for vehicle {__instance.GUID.ToString().Substring(0, 8)}...");
                
                // TODO: Implement fuel data saving through generic saveable system
                // This will be implemented when we add the persistence integration
            }
            catch (System.Exception ex)
            {
                ModLogger.Error("Error in LandVehicle.GetVehicleData postfix", ex);
            }
        }

        /// <summary>
        /// Patch LandVehicle.Load to restore fuel data from saves
        /// </summary>
        [HarmonyPatch(typeof(LandVehicle), "Load")]
        [HarmonyPostfix]
        public static void LandVehicle_Load_Postfix(LandVehicle __instance, ScheduleOne.Persistence.Datas.VehicleData data, string containerPath)
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
                
                // TODO: Implement fuel data loading through generic saveable system
                // For now, vehicles will start with default fuel levels
                
                // Set default fuel level for newly loaded vehicles if no save data exists
                if (fuelSystem.CurrentFuelLevel == fuelSystem.MaxFuelCapacity)
                {
                    // This is likely a newly loaded vehicle, so set a realistic fuel level
                    float randomFuelLevel = UnityEngine.Random.Range(0.2f, 1.0f) * fuelSystem.MaxFuelCapacity;
                    fuelSystem.SetFuelLevel(randomFuelLevel);
                    ModLogger.FuelDebug($"Set random fuel level for loaded vehicle: {randomFuelLevel:F1}L");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error("Error in LandVehicle.Load postfix", ex);
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
            catch (System.Exception ex)
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
                        ModLogger.FuelDebug($"Vehicle {fuelSystem.VehicleGUID.Substring(0, 8)}... engine stuttering");
                    }
                }

                // TODO: Add other performance effects like:
                // - Reduced acceleration when fuel is low
                // - Engine sound changes
                // - Particle effects for exhaust
            }
            catch (System.Exception ex)
            {
                ModLogger.Error("Error applying fuel performance effects", ex);
            }
        }
    }
}
