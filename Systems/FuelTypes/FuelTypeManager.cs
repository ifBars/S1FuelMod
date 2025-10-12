using UnityEngine;
#if !MONO
#endif
using S1FuelMod.Utils;
using MelonLoader;
using Il2CppInterop.Runtime.Injection;

namespace S1FuelMod.Systems.FuelTypes
{
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class FuelTypeManager : MonoBehaviour
    {
        private static FuelTypeManager? _instance;
        public static FuelTypeManager? Instance => _instance;

        private readonly Dictionary<FuelTypeId, FuelType> _fuelTypes = new Dictionary<FuelTypeId, FuelType>();
        private readonly Dictionary<VehicleType, FuelTypeId> _recommendedFuelTypes = new Dictionary<VehicleType, FuelTypeId>();

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public FuelTypeManager(IntPtr ptr) : base(ptr) { }

        /// <summary>
        /// Mono-side constructor for instantiation from managed code
        /// </summary>
        public FuelTypeManager() : base(ClassInjector.DerivedConstructorPointer<FuelTypeManager>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
#endif

        private void Awake()
        {
            try
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                _instance = this;
                DontDestroyOnLoad(gameObject);

                InitializeFuelTypes();
                ModLogger.Debug("FuelTypeManager initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelTypeManager.Awake", ex);
            }
        }

        private void InitializeFuelTypes()
        {
            _fuelTypes.Clear();

            RegisterFuelType(new RegularFuel());
            RegisterFuelType(new MidGradeFuel());
            RegisterFuelType(new PremiumFuel());
            RegisterFuelType(new DieselFuel());

            BuildRecommendationMatrix();
        }

        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private void RegisterFuelType(FuelType fuelType)
        {
            if (fuelType == null)
            {
                ModLogger.Warning("Attempted to register a null fuel type");
                return;
            }

            _fuelTypes[fuelType.Id] = fuelType;
        }

        private void BuildRecommendationMatrix()
        {
            _recommendedFuelTypes.Clear();

            // Basic vehicles - Regular fuel
            SetRecommendation(VehicleType.Shitbox, FuelTypeId.Regular);
            SetRecommendation(VehicleType.Veeper, FuelTypeId.Regular);
            SetRecommendation(VehicleType.Hotbox, FuelTypeId.Regular);
            SetRecommendation(VehicleType.CanOfSoupCar, FuelTypeId.Regular);
            SetRecommendation(VehicleType.Other, FuelTypeId.Regular);

            // Mid-tier vehicles - Mid-Grade fuel
            SetRecommendation(VehicleType.Hounddog, FuelTypeId.MidGrade);
            SetRecommendation(VehicleType.Cheetah, FuelTypeId.MidGrade);
            SetRecommendation(VehicleType.Supercar, FuelTypeId.MidGrade);
            SetRecommendation(VehicleType.Demon, FuelTypeId.MidGrade);
            SetRecommendation(VehicleType.Driftcar, FuelTypeId.MidGrade);

            // High-end vehicles - Premium fuel
            SetRecommendation(VehicleType.BugattiTourbillon, FuelTypeId.Premium);
            SetRecommendation(VehicleType.GTR_R34, FuelTypeId.Premium);
            SetRecommendation(VehicleType.GTR_R35, FuelTypeId.Premium);
            SetRecommendation(VehicleType.LamborghiniVeneno, FuelTypeId.Premium);
            SetRecommendation(VehicleType.RollsRoyceGhost, FuelTypeId.Premium);
            SetRecommendation(VehicleType.KoenigseggCC850, FuelTypeId.Premium);

            // Heavy vehicles - Diesel fuel
            SetRecommendation(VehicleType.Bruiser, FuelTypeId.Diesel);
            SetRecommendation(VehicleType.Dinkler, FuelTypeId.Diesel);
            SetRecommendation(VehicleType.CyberTruck, FuelTypeId.Diesel);
        }

        private void SetRecommendation(VehicleType vehicleType, FuelTypeId fuelTypeId)
        {
            _recommendedFuelTypes[vehicleType] = fuelTypeId;
        }

        public FuelTypeId GetRecommendedFuelType(VehicleType vehicleType)
        {
            return _recommendedFuelTypes.TryGetValue(vehicleType, out var recommended)
                ? recommended
                : FuelTypeId.Regular;
        }

        public bool IsFuelCompatible(FuelTypeId fuelTypeId, VehicleType vehicleType)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType) && fuelType.IsCompatibleWith(vehicleType);
        }

        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        public FuelType? GetFuelType(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType) ? fuelType : null;
        }

        public string GetFuelDisplayName(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.DisplayName
                : "Unknown";
        }

        public float GetFuelPrice(FuelTypeId fuelTypeId)
        {
            if (!_fuelTypes.TryGetValue(fuelTypeId, out var fuelType))
            {
                return Constants.Fuel.FUEL_PRICE_PER_LITER;
            }

            return Constants.Fuel.FUEL_PRICE_PER_LITER * fuelType.PriceMultiplier;
        }

        public float GetFuelTorqueModifier(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.TorqueModifier
                : 1.0f;
        }

        public float GetFuelAccelerationModifier(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.AccelerationModifier
                : 1.0f;
        }

        public float GetFuelTopSpeedModifier(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.TopSpeedModifier
                : 1.0f;
        }

        public Color GetFuelUIColor(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.UIColor
                : Color.white;
        }

        public float GetFuelEfficiency(FuelTypeId fuelTypeId, VehicleType vehicleType, float speedKmh, float throttleInput)
        {
            if (!_fuelTypes.TryGetValue(fuelTypeId, out var fuelType))
            {
                return 1.0f;
            }

            return fuelType.CalculateConsumptionModifier(speedKmh, throttleInput, vehicleType);
        }

        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        public FuelTypeId[] GetCompatibleFuelTypesArray(VehicleType vehicleType)
        {
            var compatible = new List<FuelTypeId>();
            foreach (var kvp in _fuelTypes)
            {
                if (kvp.Value.IsCompatibleWith(vehicleType))
                {
                    compatible.Add(kvp.Key);
                }
            }

            return compatible.ToArray();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

#if MONO
        public List<FuelTypeId> GetCompatibleFuelTypes(VehicleType vehicleType)
        {
            var compatible = new List<FuelTypeId>();
            foreach (var kvp in _fuelTypes)
            {
                if (kvp.Value.IsCompatibleWith(vehicleType))
                {
                    compatible.Add(kvp.Key);
                }
            }

            return compatible;
        }
#endif
    }
}
