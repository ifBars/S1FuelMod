using MelonLoader;
using S1FuelMod.Systems.FuelTypes;
using UnityEngine;
using UnityEngine.Rendering;
using S1FuelMod.Utils;


#if MONO
using ScheduleOne.Equipping;
using ScheduleOne.Interaction;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.UI;
using ScheduleOne;
#else
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppInterop.Runtime.Injection;
#endif

namespace S1FuelMod.Systems
{
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class Equippable_GasolineCan : Equippable
    {
        public float RefuelRate = 3f; // liters per second

        public float LitersPerCan = 10f;

        public FuelTypeId FuelTypeForCan = FuelTypeId.Regular;

        private bool isRefueling;

        private LandVehicle targetVehicle;

        private VehicleFuelSystem targetFuelSystem;

        private float pendingCanConsumption;

        private float totalFuelAdded;

        private float refuelStartTime;

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public Equippable_GasolineCan(IntPtr ptr) : base(ptr) { }

        /// <summary>
        /// Mono-side constructor for instantiation from managed code
        /// </summary>
        public Equippable_GasolineCan() : base(ClassInjector.DerivedConstructorPointer<Equippable_GasolineCan>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
#endif

        public override void Equip(ItemInstance item)
        {
            base.Equip(item);
            // Set up viewmodel positioning (similar to Equippable_Viewmodel)
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = Vector3.zero;
            transform.localScale = Vector3.one;
            LayerUtility.SetLayerRecursively(gameObject, LayerMask.NameToLayer("Viewmodel"));
            MeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer meshRenderer in componentsInChildren)
            {
                if (meshRenderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                {
                    meshRenderer.enabled = false;
                }
                else
                {
                    meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        public void Update()
        {
            // Continue refueling if active
            if (isRefueling)
            {
                ContinueRefueling();
            }
        }

        /// <summary>
        /// Called by VehicleRefuelInteractable when player starts interacting with a vehicle
        /// </summary>
        public void BeginRefuelInteraction(LandVehicle vehicle, VehicleFuelSystem fuelSystem)
        {
            if (vehicle == null || fuelSystem == null)
            {
                return;
            }

            // Don't reset if already refueling the same vehicle
            if (isRefueling && targetVehicle == vehicle && targetFuelSystem == fuelSystem)
            {
                return;
            }

            var recommendedFuelType = fuelSystem.CurrentFuelType;
            FuelTypeForCan = recommendedFuelType;

            // Check if vehicle needs fuel
            float fuelNeeded = fuelSystem.MaxFuelCapacity - fuelSystem.CurrentFuelLevel;
            if (fuelNeeded <= 0.1f)
            {
                ShowMessage("Vehicle tank is already full!", MessageType.Warning);
                return;
            }

            isRefueling = true;
            pendingCanConsumption = 0f;
            totalFuelAdded = 0f;
            refuelStartTime = Time.time;
            targetVehicle = vehicle;
            targetFuelSystem = fuelSystem;

            // Show appropriate fuel gauge when starting refueling
            ShowFuelGaugeForVehicle(vehicle);

            string fuelTypeName = GetFuelTypeDisplayName(FuelTypeForCan);
            string compatibilityTag = BuildFuelCompatibilityTag(fuelSystem, FuelTypeForCan);
            
            ModLogger.Debug($"Started refueling {vehicle.VehicleName} with gasoline can");
        }

        /// <summary>
        /// Called by VehicleRefuelInteractable when player stops interacting
        /// </summary>
        public void EndRefuelInteraction()
        {
            StopRefuel();
        }

        private void ContinueRefueling()
        {
            if (targetFuelSystem == null || targetVehicle == null)
            {
                StopRefuel();
                return;
            }

            // Determine available fuel in can (treat each unit as a can with LitersPerCan)
            var slot = PlayerSingleton<PlayerInventory>.Instance.equippedSlot;
            if (slot == null || slot.ItemInstance == null)
            {
                StopRefuel();
                return;
            }

            int availableUnits = slot.Quantity;
            if (availableUnits <= 0)
            {
                StopRefuel();
                // Clear empty can
                slot.ClearStoredInstance();
                return;
            }

            float dt = Time.deltaTime;
            float desiredFuel = Mathf.Max(0f, RefuelRate * dt);

            // Stop if vehicle full
            float capacityRemaining = Mathf.Max(0f, targetFuelSystem.MaxFuelCapacity - targetFuelSystem.CurrentFuelLevel);
            if (capacityRemaining <= 0.001f)
            {
                StopRefuel();
                return;
            }

            float fuelToAdd = Mathf.Min(desiredFuel, capacityRemaining);

            // Ensure fuel type compatibility/mixing
            if (!targetFuelSystem.ChangeFuelType(FuelTypeForCan, fuelToAdd))
            {
                StopRefuel();
                return;
            }

            // Constrain by can contents (integer units -> liters)
            float canLitersAvailable = availableUnits * LitersPerCan - pendingCanConsumption;
            if (canLitersAvailable <= 0.001f)
            {
                // Consume a unit if buffer says so
                ConsumeUnitsIfNeeded(slot);
                StopRefuel();
                return;
            }

            // Check against max fuel per can use limit
            float maxFuelPerCanUse = Core.Instance?.MaxFuelPerCanUse ?? Constants.Defaults.MAX_FUEL_PER_CAN_USE;
            float remainingLimit = maxFuelPerCanUse - totalFuelAdded;
            if (remainingLimit <= 0.001f)
            {
                // Limit reached, stop refueling and consume the item
                StopRefuel();
                return;
            }

            // Limit fuel addition to remaining limit
            float addLimitedByCan = Mathf.Min(fuelToAdd, canLitersAvailable, remainingLimit);
            float actuallyAdded = targetFuelSystem.AddFuel(addLimitedByCan);
            if (actuallyAdded > 0f)
            {
                pendingCanConsumption += actuallyAdded;
                totalFuelAdded += actuallyAdded;
                ConsumeUnitsIfNeeded(slot);

                // Check if limit reached after adding fuel
                if (totalFuelAdded >= maxFuelPerCanUse - 0.001f)
                {
                    // Limit reached, stop refueling and consume the item
                    StopRefuel();
                    return;
                }
            }
        }

        private void ConsumeUnitsIfNeeded(HotbarSlot slot)
        {
            while (pendingCanConsumption >= LitersPerCan && slot != null && slot.ItemInstance != null)
            {
                slot.ChangeQuantity(-1);
                pendingCanConsumption -= LitersPerCan;
                if (slot.Quantity <= 0)
                {
                    slot.ClearStoredInstance();
                    StopRefuel();
                    break;
                }
            }
        }

        private void ConsumeOnFinish(HotbarSlot slot)         {
            while (pendingCanConsumption > 0f && slot != null && slot.ItemInstance != null)
            {
                slot.ChangeQuantity(-1);
                pendingCanConsumption -= LitersPerCan;
                if (slot.Quantity <= 0)
                {
                    slot.ClearStoredInstance();
                    break;
                }
            }
        }

        private void StopRefuel()
        {
            if (!isRefueling) return;

            // Store values before resetting to ensure message shows correct amount
            float fuelAddedForMessage = totalFuelAdded;
            LandVehicle vehicleForMessage = targetVehicle;
            FuelTypeId fuelTypeForMessage = FuelTypeForCan;

            // Hide fuel gauge when ending refueling
            if (targetVehicle != null)
            {
                HideFuelGaugeForVehicle(targetVehicle.GUID.ToString());
            }

            // Get the slot to consume from
            var slot = PlayerSingleton<PlayerInventory>.Instance.equippedSlot;

            // Show completion message using stored value
            if (fuelAddedForMessage > 0.01f)
            {
                string fuelTypeName = GetFuelTypeDisplayName(fuelTypeForMessage);
                
                // Check if limit was reached
                float maxFuelPerCanUse = Core.Instance?.MaxFuelPerCanUse ?? Constants.Defaults.MAX_FUEL_PER_CAN_USE;
                bool limitReached = fuelAddedForMessage >= maxFuelPerCanUse - 0.001f;
                
                if (limitReached)
                {
                    ShowMessage($"Refueled {fuelAddedForMessage:F1}L of {fuelTypeName} (limit reached)", MessageType.Success);
                    // Consume the item when limit is reached
                    ConsumeItemOnLimit(slot);
                }
                else
                {
                    ShowMessage($"Refueled {fuelAddedForMessage:F1}L of {fuelTypeName} with gasoline can", MessageType.Success);
                    ConsumeOnFinish(slot);
                }
                
                ModLogger.Debug($"Completed gasoline can refueling: {fuelAddedForMessage:F1}L of {fuelTypeName}");
            }
            else if (vehicleForMessage != null)
            {
                ShowMessage("No fuel added", MessageType.Warning);
            }

            // Reset state after showing message
            isRefueling = false;
            targetVehicle = null;
            targetFuelSystem = null;
            pendingCanConsumption = 0f;
            totalFuelAdded = 0f;
        }

        /// <summary>
        /// Consume the item when the fuel limit per can use is reached
        /// </summary>
        private void ConsumeItemOnLimit(HotbarSlot slot)
        {
            if (slot == null || slot.ItemInstance == null)
                return;

            // Consume one unit (the can) when limit is reached
            slot.ChangeQuantity(-1);
            if (slot.Quantity <= 0)
            {
                slot.ClearStoredInstance();
            }
        }

        /// <summary>
        /// Show appropriate fuel gauge when starting refueling
        /// </summary>
        private void ShowFuelGaugeForVehicle(LandVehicle vehicle)
        {
            try
            {
                var uiManager = Core.Instance?.GetFuelUIManager();
                if (uiManager != null)
                {
                    bool useNewGauge = Core.Instance?.UseNewGaugeUI ?? false;
                    if (useNewGauge)
                    {
                        uiManager.ShowNewFuelGaugeForVehicle(vehicle);
                    }
                    else
                    {
                        uiManager.ShowFuelGaugeForVehicle(vehicle);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error showing fuel gauge UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Hide appropriate fuel gauge when ending refueling
        /// </summary>
        private void HideFuelGaugeForVehicle(string vehicleGUID)
        {
            try
            {
                var uiManager = Core.Instance?.GetFuelUIManager();
                if (uiManager != null)
                {
                    bool useNewGauge = Core.Instance?.UseNewGaugeUI ?? false;
                    if (useNewGauge)
                    {
                        uiManager.HideNewFuelGaugeForVehicle(vehicleGUID);
                    }
                    else
                    {
                        uiManager.HideFuelGaugeForVehicle(vehicleGUID);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error hiding fuel gauge UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Get fuel type display name
        /// </summary>
        private string GetFuelTypeDisplayName(FuelTypeId fuelTypeId)
        {
            if (FuelTypeManager.Instance != null)
            {
                string displayName = FuelTypeManager.Instance.GetFuelDisplayName(fuelTypeId);
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }
            }
            return fuelTypeId.ToString();
        }

        /// <summary>
        /// Build fuel compatibility tag similar to FuelStation
        /// </summary>
        private string BuildFuelCompatibilityTag(VehicleFuelSystem fuelSystem, FuelTypeId fuelType)
        {
            if (fuelSystem == null)
            {
                return string.Empty;
            }

            try
            {
                FuelTypeId recommended = fuelSystem.GetRecommendedFuelType();
                bool compatible = fuelSystem.IsFuelCompatible(fuelType);

                if (fuelType == recommended && compatible)
                {
                    return " (Recommended)";
                }

                if (compatible)
                {
                    return " (Compatible)";
                }

                return " (Penalty)";
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error building fuel compatibility tag: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Show a message to the player (similar to FuelStation)
        /// </summary>
        private void ShowMessage(string message, MessageType type)
        {
            try
            {
                switch (type)
                {
                    case MessageType.Error:
                        ModLogger.Warning($"Gasoline Can Error: {message}");
                        break;
                    case MessageType.Warning:
                        ModLogger.Warning($"Gasoline Can Warning: {message}");
                        break;
                    case MessageType.Success:
                        ModLogger.Info($"Gasoline Can Success: {message}");
                        break;
                    case MessageType.Info:
                    default:
                        ModLogger.Info($"Gasoline Can: {message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing gasoline can message", ex);
            }
        }

        /// <summary>
        /// Message types for user feedback
        /// </summary>
        private enum MessageType
        {
            Info,
            Warning,
            Error,
            Success
        }
    }
}


