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
using ScheduleOne.UI;
using ScheduleOne;
#else
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
#endif

namespace S1FuelMod.Systems
{
#if !MONO
    [MelonLoader.RegisterTypeInIl2Cpp]
#endif
    public class Equippable_GasolineCan : Equippable
    {
        public float InteractionRange = 3f;

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
            // Don't call base.Update() to avoid circular calls
            TryDetectAndPrompt();
            if (isRefueling)
            {
                ContinueRefueling();
            }
        }

        private void TryDetectAndPrompt()
        {
            if (PlayerSingleton<PlayerCamera>.Instance == null)
            {
                return;
            }
            if (PlayerSingleton<PlayerCamera>.Instance.activeUIElementCount > 0)
            {
                return;
            }
            if (targetVehicle == null)
            {
                targetVehicle = RaycastForVehicle();
            }
            if (targetVehicle != null && !isRefueling)
            {
                ConfigurePromptForVehicle(targetVehicle);
                if (GameInput.GetButtonDown(GameInput.ButtonCode.Interact))
                {
                    ModLogger.Debug($"Gasoline can: Starting refuel of {targetVehicle.VehicleName}");
                    BeginRefuel(targetVehicle);
                }
            }
        }

        private LandVehicle RaycastForVehicle()
        {
            try
            {
                Ray ray = new Ray(PlayerSingleton<PlayerCamera>.Instance.Camera.transform.position, PlayerSingleton<PlayerCamera>.Instance.Camera.transform.forward);
                int vehicleLayer = LayerMask.NameToLayer("Vehicle");
                int mask = (vehicleLayer >= 0) ? (1 << vehicleLayer) : Physics.DefaultRaycastLayers;
                if (Physics.Raycast(ray, out var hit, InteractionRange, mask))
                {
                    LandVehicle lv = hit.collider.GetComponentInParent<LandVehicle>();
                    if (lv != null && lv.IsPlayerOwned)
                    {
                        return lv;
                    }
                }
                // Fallback: small sphere around camera
                Collider[] cols = Physics.OverlapSphere(PlayerSingleton<PlayerCamera>.Instance.Camera.transform.position + PlayerSingleton<PlayerCamera>.Instance.Camera.transform.forward * 1.5f, 1.75f);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == null) continue;
                    LandVehicle lv2 = cols[i].GetComponentInParent<LandVehicle>();
                    if (lv2 != null && lv2.IsPlayerOwned)
                    {
                        return lv2;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"GasCan vehicle detection error: {ex.Message}");
            }
            return null;
        }

        private void ConfigurePromptForVehicle(LandVehicle vehicle)
        {
            try
            {
                if (Core.Instance?.GetFuelSystemManager() == null) return;
                
                var fuelSystem = Core.Instance.GetFuelSystemManager().GetFuelSystem(vehicle.GUID.ToString());
                if (fuelSystem == null) return;

                string label = vehicle.VehicleName;
                float fuelNeeded = fuelSystem.MaxFuelCapacity - fuelSystem.CurrentFuelLevel;
                string fuelTypeName = GetFuelTypeDisplayName(FuelTypeForCan);
                string compatibilityTag = BuildFuelCompatibilityTag(fuelSystem, FuelTypeForCan);

                if (fuelNeeded > 0.1f) // Only show if vehicle needs fuel
                {
                    string message = $"Refuel {label} [{fuelTypeName}{compatibilityTag}] (Hold)";
                    Singleton<InteractionCanvas>.Instance.EnableInteractionDisplay(
                        vehicle.transform.position, 
                        Singleton<InteractionCanvas>.Instance.KeyIcon, 
                        Singleton<InteractionManager>.Instance.InteractKeyStr, 
                        message, 
                        Singleton<InteractionCanvas>.Instance.DefaultMessageColor, 
                        Singleton<InteractionCanvas>.Instance.DefaultKeyColor);
                }
                else
                {
                    string message = $"{label} - Tank Full";
                    Singleton<InteractionCanvas>.Instance.EnableInteractionDisplay(
                        vehicle.transform.position, 
                        Singleton<InteractionCanvas>.Instance.CrossIcon, 
                        "", 
                        message, 
                        Singleton<InteractionCanvas>.Instance.InvalidMessageColor, 
                        Singleton<InteractionCanvas>.Instance.InvalidIconColor);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error configuring prompt for vehicle: {ex.Message}");
            }
        }

        private void BeginRefuel(LandVehicle vehicle)
        {
            if (Core.Instance == null || Core.Instance.GetFuelSystemManager() == null)
            {
                return;
            }
            var fsm = Core.Instance.GetFuelSystemManager();
            targetFuelSystem = fsm.GetFuelSystem(vehicle.GUID.ToString());
            if (targetFuelSystem == null)
            {
                return;
            }

            // Check if vehicle needs fuel
            float fuelNeeded = targetFuelSystem.MaxFuelCapacity - targetFuelSystem.CurrentFuelLevel;
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

            // Show appropriate fuel gauge when starting refueling
            ShowFuelGaugeForVehicle(vehicle);

            string fuelTypeName = GetFuelTypeDisplayName(FuelTypeForCan);
            string compatibilityTag = BuildFuelCompatibilityTag(targetFuelSystem, FuelTypeForCan);
            ShowMessage($"Refueling {vehicle.VehicleName} with {fuelTypeName}{compatibilityTag}...", MessageType.Info);
            
            ModLogger.Debug($"Started refueling {vehicle.VehicleName} with gasoline can");
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

            float addLimitedByCan = Mathf.Min(fuelToAdd, canLitersAvailable);
            float actuallyAdded = targetFuelSystem.AddFuel(addLimitedByCan);
            if (actuallyAdded > 0f)
            {
                pendingCanConsumption += actuallyAdded;
                totalFuelAdded += actuallyAdded;
                ConsumeUnitsIfNeeded(slot);
            }

            // Stop if input released
            if (!GameInput.GetButton(GameInput.ButtonCode.Interact))
            {
                StopRefuel();
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

        private void StopRefuel()
        {
            if (!isRefueling) return;

            // Hide fuel gauge when ending refueling
            if (targetVehicle != null)
            {
                HideFuelGaugeForVehicle(targetVehicle.GUID.ToString());
            }

            // Show completion message
            if (totalFuelAdded > 0.01f)
            {
                string fuelTypeName = GetFuelTypeDisplayName(FuelTypeForCan);
                ShowMessage($"Refueled {totalFuelAdded:F1}L of {fuelTypeName} with gasoline can", MessageType.Success);
                ModLogger.Debug($"Completed gasoline can refueling: {totalFuelAdded:F1}L of {fuelTypeName}");
            }
            else if (targetVehicle != null)
            {
                ShowMessage("No fuel added", MessageType.Warning);
            }

            isRefueling = false;
            targetVehicle = null;
            targetFuelSystem = null;
            pendingCanConsumption = 0f;
            totalFuelAdded = 0f;
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


