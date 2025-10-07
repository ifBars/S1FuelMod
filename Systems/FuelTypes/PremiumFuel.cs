using System.Collections.Generic;
using UnityEngine;
using S1FuelMod.Systems;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems.FuelTypes
{
    public sealed class PremiumFuel : FuelType
    {
        private static readonly Color PremiumFuelColor = new Color(0.8f, 0.2f, 0.8f, 1.0f);

        private static readonly HashSet<VehicleType> CompatibleVehicleTypes = new HashSet<VehicleType>
        {
            VehicleType.Cheetah,
            VehicleType.Hounddog,
            VehicleType.Supercar,
            VehicleType.BugattiTourbillon,
            VehicleType.GTR_R34,
            VehicleType.GTR_R35,
            VehicleType.LamborghiniVeneno,
            VehicleType.RollsRoyceGhost,
            VehicleType.KoenigseggCC850
        };

        public override FuelTypeId Id => FuelTypeId.Premium;
        public override string DisplayName => "Premium";
        public override string Description => "High-octane fuel for performance vehicles.";
        public override float PriceMultiplier => 1.40f;
        public override float ConsumptionEfficiency => 0.85f;
        public override float TorqueModifier => 1.12f;
        public override float AccelerationModifier => 1.18f;
        public override float TopSpeedModifier => 1.08f;
        public override Color UIColor => PremiumFuelColor;

        public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        {
            return (65f, 90f);
        }

        protected override HashSet<VehicleType> GetCompatibleVehicleTypes()
        {
            return CompatibleVehicleTypes;
        }
    }
}
