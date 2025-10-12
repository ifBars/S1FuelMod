using MelonLoader;
using S1FuelMod.Systems.FuelTypes;
using S1FuelMod.Utils;
#if MONO
using ScheduleOne.Equipping;
using ScheduleOne.Interaction;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
using ScheduleOne.ItemFramework;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
#else
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.DevUtilities;
#endif

namespace S1FuelMod.Systems
{
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class VehicleRefuelInteractable : InteractableObject
    {
        private LandVehicle _vehicle;
        private VehicleFuelSystem _fuelSystem;
        private Equippable_GasolineCan _activeGasCan;

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public VehicleRefuelInteractable(IntPtr ptr) : base(ptr) { }

        /// <summary>
        /// Mono-side constructor for instantiation from managed code
        /// </summary>
        public VehicleRefuelInteractable() : base(ClassInjector.DerivedConstructorPointer<VehicleRefuelInteractable>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
#endif

        private void Awake()
        {
            try
            {
                _vehicle = GetComponent<LandVehicle>();
                _fuelSystem = GetComponent<VehicleFuelSystem>();

                if (_vehicle == null || _fuelSystem == null)
                {
                    ModLogger.Warning("VehicleRefuelInteractable: Missing required components");
                    enabled = false;
                    return;
                }

                // Configure interaction settings
                MaxInteractionRange = 1f;
                RequiresUniqueClick = false; // Allow holding
                Priority = 10;

                ModLogger.Debug($"VehicleRefuelInteractable initialized for {_vehicle.VehicleName}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in VehicleRefuelInteractable.Awake", ex);
            }
        }

        public override void Hovered()
        {
            try
            {
                // Check if player has a gasoline can equipped
                var equippable = PlayerSingleton<PlayerInventory>.Instance?.equippedSlot?.Equippable;
                if (equippable == null)
                {
                    return;
                }

                _activeGasCan = equippable.GetComponent<Equippable_GasolineCan>();
                if (_activeGasCan == null)
                {
                    return;
                }

                // Only show prompt for player-owned vehicles
                if (!_vehicle.IsPlayerOwned)
                {
                    return;
                }

                // Check fuel status and update interaction state
                float fuelNeeded = _fuelSystem.MaxFuelCapacity - _fuelSystem.CurrentFuelLevel;
                
                if (fuelNeeded > 0.1f)
                {
                    // Vehicle needs fuel - show refuel prompt
                    string fuelTypeName = GetFuelTypeDisplayName(_activeGasCan.FuelTypeForCan);
                    string compatibilityTag = BuildFuelCompatibilityTag(_fuelSystem, _activeGasCan.FuelTypeForCan);
                    
                    SetMessage($"Refuel {_vehicle.VehicleName} [{fuelTypeName}{compatibilityTag}] (Hold)");
                    SetInteractableState(EInteractableState.Default);
                    SetInteractionType(EInteractionType.Key_Press);
                }
                else
                {
                    // Vehicle is full
                    SetMessage($"{_vehicle.VehicleName} - Tank Full");
                    SetInteractableState(EInteractableState.Invalid);
                }

                // Manually invoke the onHovered event
#if !MONO
                if (onHovered != null)
                {
                    onHovered.Invoke();
                }
#else
                onHovered?.Invoke();
#endif
                
                // Show the message we configured above (don't call base.Hovered to avoid recursion)
                if (_interactionState != EInteractableState.Disabled)
                {
                    ShowMessage();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error in VehicleRefuelInteractable.Hovered: {ex.Message}");
            }
        }

        public override void StartInteract()
        {
            try
            {
                if (_interactionState != EInteractableState.Invalid)
                {
                    // Invoke the onInteractStart event
#if !MONO
                    if (onInteractStart != null)
                    {
                        onInteractStart.Invoke();
                    }
#else
                    onInteractStart?.Invoke();
#endif
                    
                    // Scale effect from base class
                    Singleton<InteractionCanvas>.Instance.LerpDisplayScale(0.9f);
                }

                if (_activeGasCan != null && _fuelSystem != null)
                {
                    // Begin refueling through the gas can
                    _activeGasCan.BeginRefuelInteraction(_vehicle, _fuelSystem);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error in VehicleRefuelInteractable.StartInteract: {ex.Message}");
            }
        }

        public override void EndInteract()
        {
            try
            {
                // Invoke the onInteractEnd event
#if !MONO
                if (onInteractEnd != null)
                {
                    onInteractEnd.Invoke();
                }
#else
                onInteractEnd?.Invoke();
#endif
                
                // Scale effect from base class
                Singleton<InteractionCanvas>.Instance.LerpDisplayScale(1f);

                if (_activeGasCan != null)
                {
                    // End refueling through the gas can
                    _activeGasCan.EndRefuelInteraction();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error in VehicleRefuelInteractable.EndInteract: {ex.Message}");
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
    }
}

