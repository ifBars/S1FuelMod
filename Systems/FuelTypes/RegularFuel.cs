using UnityEngine;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems.FuelTypes
{
    public sealed class RegularFuel : FuelType
    {
        private static readonly Color RegularFuelColor = new Color(0.2f, 0.8f, 0.2f, 1.0f);

        private static readonly HashSet<VehicleType> CompatibleVehicleTypes = new HashSet<VehicleType>
        {
            VehicleType.Shitbox,
            VehicleType.Veeper,
            VehicleType.Hotbox,
            VehicleType.CanOfSoupCar,
            VehicleType.Other
        };

        public override FuelTypeId Id => FuelTypeId.Regular;
        public override string DisplayName => "Regular";
        public override string Description => "Standard gasoline for everyday driving.";
        public override float PriceMultiplier => 1.0f;
        public override float ConsumptionEfficiency => 1.0f;
        public override float TorqueModifier => 1.0f;
        public override float AccelerationModifier => 1.0f;
        public override float TopSpeedModifier => 1.0f;
        public override Color UIColor => RegularFuelColor;

        public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        {
            return (40f, 60f);
        }

        protected override HashSet<VehicleType> GetCompatibleVehicleTypes()
        {
            return CompatibleVehicleTypes;
        }
    }
}
