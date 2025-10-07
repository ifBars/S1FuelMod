using System.Collections.Generic;
using UnityEngine;
using S1FuelMod.Systems;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems.FuelTypes
{
    public sealed class MidGradeFuel : FuelType
    {
        private static readonly Color MidGradeFuelColor = new Color(0.4f, 0.6f, 0.2f, 1.0f);

        private static readonly HashSet<VehicleType> CompatibleVehicleTypes = new HashSet<VehicleType>
        {
            VehicleType.Shitbox,
            VehicleType.Veeper,
            VehicleType.Hotbox,
            VehicleType.CanOfSoupCar,
            VehicleType.Other,
            VehicleType.Hounddog,
            VehicleType.Cheetah,
            VehicleType.Supercar,
            VehicleType.BugattiTourbillon,
            VehicleType.GTR_R34,
            VehicleType.GTR_R35,
            VehicleType.LamborghiniVeneno,
            VehicleType.RollsRoyceGhost,
            VehicleType.KoenigseggCC850,
            VehicleType.Demon,
            VehicleType.Driftcar
        };

        public override FuelTypeId Id => FuelTypeId.MidGrade;
        public override string DisplayName => "Mid-Grade";
        public override string Description => "Mid-grade gasoline offering balanced performance and efficiency.";
        public override float PriceMultiplier => 1.15f;
        public override float ConsumptionEfficiency => 0.90f;
        public override float TorqueModifier => 1.02f;
        public override float AccelerationModifier => 1.04f;
        public override float TopSpeedModifier => 1.02f;
        public override Color UIColor => MidGradeFuelColor;

        public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        {
            return (50f, 70f);
        }

        protected override HashSet<VehicleType> GetCompatibleVehicleTypes()
        {
            return CompatibleVehicleTypes;
        }
    }
}
