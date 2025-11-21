using UnityEngine;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems.FuelTypes
{
    /// <summary>
    /// Shared abstraction describing a fuel type supported by the mod.
    /// </summary>
    public abstract class FuelType
    {
        public abstract FuelTypeId Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract float PriceMultiplier { get; }
        public abstract float ConsumptionEfficiency { get; }
        public abstract float TorqueModifier { get; }
        public abstract float AccelerationModifier { get; }
        public abstract float TopSpeedModifier { get; }
        public abstract Color UIColor { get; }

        public virtual bool IsCompatibleWith(VehicleType vehicleType)
        {
            try
            {
                HashSet<VehicleType> compatibleTypes = GetCompatibleVehicleTypes();
                return compatibleTypes != null && compatibleTypes.Contains(vehicleType);
            }
            catch (Exception ex)
            {
                ModLogger.Error($"FuelType compatibility check failed for {DisplayName}", ex);
                return false;
            }
        }

        public virtual float CalculateConsumptionModifier(float speedKmh, float throttleInput, VehicleType vehicleType)
        {
            try
            {
                float speedCurve = CalculateSpeedEfficiencyCurve(speedKmh);
                float throttleMod = CalculateThrottleModifier(throttleInput);
                float vehicleMod = GetVehicleTypeModifier(vehicleType);

                return speedCurve * throttleMod * vehicleMod;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"FuelType consumption modifier failed for {DisplayName}", ex);
                return 1.0f;
            }
        }

        public virtual (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        {
            return (45f, 75f);
        }

        public virtual float GetIncompatibilityPenalty(VehicleType vehicleType)
        {
            return IsCompatibleWith(vehicleType) ? 1.0f : 0.7f;
        }

        protected abstract HashSet<VehicleType> GetCompatibleVehicleTypes();

        protected virtual float CalculateSpeedEfficiencyCurve(float speedKmh)
        {
            (float minOptimal, float maxOptimal) = GetOptimalSpeedRange();

            if (speedKmh >= minOptimal && speedKmh <= maxOptimal)
            {
                return 1.0f;
            }

            float deviation = speedKmh < minOptimal
                ? minOptimal - speedKmh
                : speedKmh - maxOptimal;

            float adjusted = 1.0f - (deviation * 0.01f);
            return Mathf.Max(0.7f, adjusted);
        }

        protected virtual float CalculateThrottleModifier(float throttleInput)
        {
            float clampedInput = Mathf.Clamp01(Mathf.Abs(throttleInput));
            return 1.0f + (clampedInput * clampedInput * 0.5f);
        }

        protected virtual float GetVehicleTypeModifier(VehicleType vehicleType)
        {
            return IsCompatibleWith(vehicleType) ? 1.0f : 1.3f;
        }
    }
}
