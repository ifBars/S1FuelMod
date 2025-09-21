using S1FuelMod.Systems;

namespace S1FuelMod.Utils
{
    public static class VehicleTypeExtensions
    {
        public static VehicleClass GetVehicleClass(this VehicleType vehicleType)
        {
            switch (vehicleType)
            {
                case VehicleType.Cheetah:
                case VehicleType.Hounddog:
                case VehicleType.Supercar:
                case VehicleType.BugattiTourbillon:
                case VehicleType.GTR_R34:
                case VehicleType.GTR_R35:
                case VehicleType.LamborghiniVeneno:
                case VehicleType.KoenigseggCC850:
                    return VehicleClass.Sports;

                case VehicleType.Dinkler:
                case VehicleType.Bruiser:
                case VehicleType.CyberTruck:
                    return VehicleClass.Truck;

                case VehicleType.RollsRoyceGhost:
                    return VehicleClass.Luxury;

                default:
                    return VehicleClass.Economy;
            }
        }
    }

    public enum VehicleClass
    {
        Economy,
        Sports,
        Truck,
        Luxury
    }
}
