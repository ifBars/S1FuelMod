using UnityEngine;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems.FuelTypes
{
    public sealed class DieselFuel : FuelType
    {
        private static readonly Color DieselFuelColor = new Color(0.6f, 0.4f, 0.1f, 1.0f);

        private static readonly HashSet<VehicleType> CompatibleVehicleTypes = new HashSet<VehicleType>
        {
            VehicleType.Dinkler,
            VehicleType.Bruiser,
            VehicleType.CyberTruck
        };

        public override FuelTypeId Id => FuelTypeId.Diesel;
        public override string DisplayName => "Diesel";
        public override string Description => "High-efficiency fuel for heavy vehicles.";
        public override float PriceMultiplier => 1.15f;
        public override float ConsumptionEfficiency => 0.75f;
        public override float TorqueModifier => 1.25f;
        public override float AccelerationModifier => 0.90f;
        public override float TopSpeedModifier => 0.95f;
        public override Color UIColor => DieselFuelColor;

        public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        {
            return (50f, 75f);
        }

        protected override HashSet<VehicleType> GetCompatibleVehicleTypes()
        {
            return CompatibleVehicleTypes;
        }
    }
}
