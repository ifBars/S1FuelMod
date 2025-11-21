# S1FuelMod Multi-Fuel Type System Implementation Plan

## Overview
This comprehensive plan outlines the implementation of a multi-fuel type system for S1FuelMod based on current Trello assignments, building upon the existing robust fuel system architecture while ensuring full IL2CPP/Mono compatibility.

## Trello Requirements Analysis

### 1. New FuelTypes (Regular, Diesel, Premium) for cars to use
**Trello Card ID**: 6896925c7361aeef499cc53f
**Current Status**: Not implemented
**Description**: "Lets add more fuel types to the game to make things more realistic. We are going to need regular, premium, and diesel fuel to begin with. Trucks are going to be getting diesel, larger sedans will get premium, and the utility vehicles will run on regular"

**Checklist Requirements**:
- ✅ Create new FuelType interface for each new Fuel to inherit
- ✅ Create new Regular Fuel
- ✅ Create new Diesel Fuel
- ✅ Create new Premium Fuel
- ✅ Assign different engine/performance effects based on fuel type (IE Diesel has more torque, premium more acceleration, etc)
- ✅ Integrate OnInteract behavior at the FuelStation so that the correct fuel type/price is displayed at the pump

### 2. Make fuel consume * speed
**Trello Card ID**: 68a29737a13bbbbacf8b37cf
**Current Status**: Partially implemented (basic speed multiplier exists)
**Requirements**: Enhance the current speed-based consumption algorithm to make fuel consumption more significantly tied to vehicle speed

## Current Architecture Analysis

### Existing Strengths
- **IL2CPP/Mono Compatibility**: Proper conditional compilation with `#if MONO/#else` blocks
- **Vehicle Integration**: Robust `VehicleFuelSystem` component with speed tracking
- **Fuel Station System**: Complete `FuelStation` interaction system
- **UI Framework**: Functional fuel gauge and UI management
- **Configuration System**: Comprehensive MelonPreferences setup
- **Performance**: Existing speed-based consumption foundation

### Current Speed Consumption Logic
```csharp
// Current implementation in VehicleFuelSystem.cs:287-290
if (_landVehicle.speed_Kmh > 50f)
{
    float speedMultiplier = 1f + ((_landVehicle.speed_Kmh - 50f) / 100f);
    consumptionRate *= speedMultiplier;
}
```

## Implementation Strategy

### Phase 1: Fuel Type Foundation (Week 1)
**Priority**: Critical - Establishes the extensible fuel type architecture

#### 1.1 Core Fuel Type Abstract Base Class
```csharp
// Systems/FuelTypes/FuelType.cs
namespace S1FuelMod.Systems.FuelTypes
{
    public abstract class FuelType
    {
        // Core properties that all fuel types must implement
        public abstract FuelTypeId Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract float PriceMultiplier { get; }
        public abstract float ConsumptionEfficiency { get; }
        public abstract float TorqueModifier { get; }
        public abstract float AccelerationModifier { get; }
        public abstract float TopSpeedModifier { get; }
        public abstract UnityEngine.Color UIColor { get; }

        // Virtual methods with default implementations that can be overridden
        public virtual bool IsCompatibleWith(VehicleType vehicleType)
        {
            return GetCompatibleVehicleTypes().Contains(vehicleType);
        }

        public virtual float CalculateConsumptionModifier(float speedKmh, float throttleInput, VehicleType vehicleType)
        {
            float speedCurve = CalculateSpeedEfficiencyCurve(speedKmh);
            float throttleMod = CalculateThrottleModifier(throttleInput);
            float vehicleMod = GetVehicleTypeModifier(vehicleType);

            return speedCurve * throttleMod * vehicleMod;
        }

        public virtual (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        {
            return (45f, 75f); // Default range
        }

        public virtual float GetIncompatibilityPenalty(VehicleType vehicleType)
        {
            return IsCompatibleWith(vehicleType) ? 1.0f : 0.7f; // 30% penalty
        }

        // Protected methods for subclass use
        protected abstract System.Collections.Generic.HashSet<VehicleType> GetCompatibleVehicleTypes();

        protected virtual float CalculateSpeedEfficiencyCurve(float speedKmh)
        {
            var (minOptimal, maxOptimal) = GetOptimalSpeedRange();

            if (speedKmh >= minOptimal && speedKmh <= maxOptimal)
                return 1.0f; // Optimal efficiency

            float deviation = speedKmh < minOptimal ?
                (minOptimal - speedKmh) : (speedKmh - maxOptimal);

            return System.Math.Max(0.7f, 1.0f - (deviation * 0.01f));
        }

        protected virtual float CalculateThrottleModifier(float throttleInput)
        {
            return 1.0f + (throttleInput * throttleInput * 0.5f);
        }

        protected virtual float GetVehicleTypeModifier(VehicleType vehicleType)
        {
            return IsCompatibleWith(vehicleType) ? 1.0f : 1.3f; // 30% penalty for incompatible
        }
    }
}
```

#### 1.2 Fuel Type Enumeration
```csharp
// Utils/FuelTypeId.cs
namespace S1FuelMod.Utils
{
    public enum FuelTypeId
    {
        Regular = 0,
        Premium = 1,
        Diesel = 2
    }
}
```

#### 1.3 Vehicle Type Extensions
```csharp
// Utils/VehicleTypeExtensions.cs
namespace S1FuelMod.Utils
{
    public static class VehicleTypeExtensions
    {
        public static VehicleClass GetVehicleClass(this VehicleType vehicleType)
        {
            // Categorize vehicles into Economy, Sports, Truck classes
        }
    }

    public enum VehicleClass
    {
        Economy,     // Shitbox, Veeper, CanOfSoupCar, Other
        Sports,      // Cheetah, Hounddog, Supercars, GTRs
        Truck,       // Dinkler, Bruiser, CyberTruck
        Luxury       // Rolls Royce, Premium sedans
    }
}
```

### Phase 2: Concrete Fuel Type Implementation (Week 1-2)

#### 2.1 Specific Fuel Type Implementations

**Regular Fuel** - Economy Focus:
```csharp
// Systems/FuelTypes/RegularFuel.cs
public sealed class RegularFuel : FuelType
{
    public override FuelTypeId Id => FuelTypeId.Regular;
    public override string DisplayName => "Regular";
    public override string Description => "Standard gasoline for everyday driving";
    public override float PriceMultiplier => 1.0f;
    public override float ConsumptionEfficiency => 1.0f;
    public override float TorqueModifier => 1.0f;
    public override float AccelerationModifier => 1.0f;
    public override float TopSpeedModifier => 1.0f;
    public override UnityEngine.Color UIColor => new UnityEngine.Color(0.2f, 0.8f, 0.2f, 1.0f);

    public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        => (45f, 65f); // City/suburban driving

    protected override System.Collections.Generic.HashSet<VehicleType> GetCompatibleVehicleTypes()
    {
        return new System.Collections.Generic.HashSet<VehicleType>
        {
            VehicleType.Shitbox, VehicleType.Veeper, VehicleType.Hotbox,
            VehicleType.CanOfSoupCar, VehicleType.Other
        };
    }
}
```

**Premium Fuel** - Performance Focus:
```csharp
// Systems/FuelTypes/PremiumFuel.cs
public sealed class PremiumFuel : FuelType
{
    public override FuelTypeId Id => FuelTypeId.Premium;
    public override string DisplayName => "Premium";
    public override string Description => "High-octane fuel for performance vehicles";
    public override float PriceMultiplier => 1.35f;
    public override float ConsumptionEfficiency => 0.95f; // 5% better efficiency
    public override float TorqueModifier => 1.08f; // +8% torque
    public override float AccelerationModifier => 1.12f; // +12% acceleration
    public override float TopSpeedModifier => 1.06f; // +6% top speed
    public override UnityEngine.Color UIColor => new UnityEngine.Color(0.8f, 0.2f, 0.8f, 1.0f);

    public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        => (60f, 85f); // Highway performance driving

    protected override System.Collections.Generic.HashSet<VehicleType> GetCompatibleVehicleTypes()
    {
        return new System.Collections.Generic.HashSet<VehicleType>
        {
            VehicleType.Cheetah, VehicleType.Hounddog, VehicleType.SuperCar,
            VehicleType.BugattiTourbillon, VehicleType.GTRR34, VehicleType.GTRR35,
            VehicleType.LamborghiniVeneno, VehicleType.RollsRoyceGhost, VehicleType.KoenigseggCC850
        };
    }
}
```

**Diesel Fuel** - Efficiency/Torque Focus:
```csharp
// Systems/FuelTypes/DieselFuel.cs
public sealed class DieselFuel : FuelType
{
    public override FuelTypeId Id => FuelTypeId.Diesel;
    public override string DisplayName => "Diesel";
    public override string Description => "High-efficiency fuel for heavy vehicles";
    public override float PriceMultiplier => 1.15f;
    public override float ConsumptionEfficiency => 0.75f; // 25% better efficiency
    public override float TorqueModifier => 1.25f; // +25% torque
    public override float AccelerationModifier => 0.90f; // -10% acceleration
    public override float TopSpeedModifier => 0.95f; // -5% top speed
    public override UnityEngine.Color UIColor => new UnityEngine.Color(0.6f, 0.4f, 0.1f, 1.0f);

    public override (float minOptimalSpeed, float maxOptimalSpeed) GetOptimalSpeedRange()
        => (50f, 75f); // Steady highway cruising

    protected override System.Collections.Generic.HashSet<VehicleType> GetCompatibleVehicleTypes()
    {
        return new System.Collections.Generic.HashSet<VehicleType>
        {
            VehicleType.Dinkler, VehicleType.Bruiser, VehicleType.CyberTruck
        };
    }
}
```

### Phase 3: Fuel Type Management System (Week 2)

#### 3.1 Fuel Type Manager
```csharp
// Systems/FuelTypes/FuelTypeManager.cs
namespace S1FuelMod.Systems.FuelTypes
{
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
    public class FuelTypeManager : UnityEngine.MonoBehaviour
    {
        private static FuelTypeManager? _instance;
        public static FuelTypeManager Instance => _instance;

        private readonly System.Collections.Generic.Dictionary<FuelTypeId, FuelType> _fuelTypes
            = new System.Collections.Generic.Dictionary<FuelTypeId, FuelType>();

        private readonly System.Collections.Generic.Dictionary<VehicleType, FuelTypeId> _recommendedFuelTypes
            = new System.Collections.Generic.Dictionary<VehicleType, FuelTypeId>();

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public FuelTypeManager(IntPtr ptr) : base(ptr) { }
#endif

        private void Awake()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = this;
                    InitializeFuelTypes();
                    UnityEngine.Object.DontDestroyOnLoad(gameObject);
                }
                else
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelTypeManager.Awake", ex);
            }
        }

        private void InitializeFuelTypes()
        {
            RegisterFuelType(new RegularFuel());
            RegisterFuelType(new PremiumFuel());
            RegisterFuelType(new DieselFuel());
            BuildRecommendationMatrix();
        }

        private void RegisterFuelType(FuelType fuelType)
        {
            _fuelTypes[fuelType.Id] = fuelType;
        }

        // All methods return basic types for IL2CPP compatibility
        public FuelTypeId GetRecommendedFuelType(VehicleType vehicleType)
        {
            return _recommendedFuelTypes.TryGetValue(vehicleType, out var recommended)
                ? recommended : FuelTypeId.Regular;
        }

        public bool IsFuelCompatible(FuelTypeId fuelTypeId, VehicleType vehicleType)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                && fuelType.IsCompatibleWith(vehicleType);
        }

        public float GetFuelEfficiency(FuelTypeId fuelTypeId, VehicleType vehicleType,
            float speedKmh, float throttleInput)
        {
            if (!_fuelTypes.TryGetValue(fuelTypeId, out var fuelType))
                return 1.0f;

            return fuelType.CalculateConsumptionModifier(speedKmh, throttleInput, vehicleType);
        }

        public string GetFuelDisplayName(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.DisplayName : "Unknown";
        }

        public float GetFuelPrice(FuelTypeId fuelTypeId)
        {
            if (!_fuelTypes.TryGetValue(fuelTypeId, out var fuelType))
                return Constants.Fuel.FUEL_PRICE_PER_LITER;

            return Constants.Fuel.FUEL_PRICE_PER_LITER * fuelType.PriceMultiplier;
        }

        public float GetFuelTorqueModifier(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.TorqueModifier : 1.0f;
        }

        public float GetFuelAccelerationModifier(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.AccelerationModifier : 1.0f;
        }

        public float GetFuelTopSpeedModifier(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.TopSpeedModifier : 1.0f;
        }

        public UnityEngine.Color GetFuelUIColor(FuelTypeId fuelTypeId)
        {
            return _fuelTypes.TryGetValue(fuelTypeId, out var fuelType)
                ? fuelType.UIColor : UnityEngine.Color.white;
        }

        // Return array of compatible fuel type IDs instead of List
        public FuelTypeId[] GetCompatibleFuelTypesArray(VehicleType vehicleType)
        {
            var compatible = new System.Collections.Generic.List<FuelTypeId>();
            foreach (var kvp in _fuelTypes)
            {
                if (kvp.Value.IsCompatibleWith(vehicleType))
                    compatible.Add(kvp.Key);
            }
            return compatible.ToArray();
        }

        // This method can return the concrete base class - fully IL2CPP compatible
        public FuelType GetFuelType(FuelTypeId fuelTypeId)
        {
            _fuelTypes.TryGetValue(fuelTypeId, out var fuelType);
            return fuelType ?? _fuelTypes[FuelTypeId.Regular];
        }

        // Alternative method for Mono compatibility returning List instead of array
#if MONO
        public System.Collections.Generic.List<FuelTypeId> GetCompatibleFuelTypes(VehicleType vehicleType)
        {
            var compatible = new System.Collections.Generic.List<FuelTypeId>();
            foreach (var kvp in _fuelTypes)
            {
                if (kvp.Value.IsCompatibleWith(vehicleType))
                    compatible.Add(kvp.Key);
            }
            return compatible;
        }
#endif
    }
}
```

### Phase 4: Enhanced Speed-Based Consumption (Week 2)

#### 4.1 Advanced Consumption Algorithm
Enhance the existing `VehicleFuelSystem.UpdateFuelConsumption()` method:

```csharp
// Enhanced consumption algorithm for VehicleFuelSystem.cs
private void UpdateFuelConsumption()
{
    if (_landVehicle == null || currentFuelLevel <= 0f) return;

    float deltaTime = UnityEngine.Time.deltaTime;
    float throttleInput = _landVehicle.currentThrottle;

    // Base consumption calculation (existing logic enhanced)
    float baseConsumption = CalculateBaseConsumption(throttleInput);

    // NEW: Enhanced speed-based consumption multiplier
    float speedMultiplier = CalculateAdvancedSpeedMultiplier(_landVehicle.speed_Kmh);

    // NEW: Fuel type efficiency modifier
    float fuelTypeModifier = GetCurrentFuelEfficiencyModifier();

    // Final consumption calculation
    float finalConsumption = baseConsumption * speedMultiplier * fuelTypeModifier;

    // Apply fuel consumption
    ConsumeFuel((finalConsumption / 3600f) * deltaTime);

    _lastConsumptionTime = UnityEngine.Time.time;
}

private float CalculateAdvancedSpeedMultiplier(float speedKmh)
{
    // More aggressive speed-based consumption curve
    if (speedKmh <= 20f) return 0.8f; // Efficiency bonus for very low speeds
    if (speedKmh <= 40f) return 1.0f; // Baseline city driving
    if (speedKmh <= 60f) return 1.0f + (speedKmh - 40f) * 0.025f; // Linear increase
    if (speedKmh <= 80f) return 1.5f + (speedKmh - 60f) * 0.05f; // Steeper increase

    // Exponential penalty for very high speeds
    float excessSpeed = speedKmh - 80f;
    return 2.5f + (excessSpeed * excessSpeed) * 0.002f;
}

private float GetCurrentFuelEfficiencyModifier()
{
    if (FuelTypeManager.Instance == null) return 1.0f;

    return FuelTypeManager.Instance.GetFuelEfficiency(
        _currentFuelType, _vehicleType,
        _landVehicle.speed_Kmh, _landVehicle.currentThrottle);
}
```

### Phase 5: Vehicle Integration Updates (Week 2-3)

#### 5.1 VehicleFuelSystem Enhancements
```csharp
// Additional properties and methods for VehicleFuelSystem.cs
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
public class VehicleFuelSystem : MonoBehaviour
{
    // NEW: Fuel type management
    private FuelTypeId _currentFuelType = FuelTypeId.Regular;
    private float _fuelQuality = 1.0f; // Track fuel contamination/mixing

#if !MONO
    /// <summary>
    /// IL2CPP constructor required for RegisterTypeInIl2Cpp
    /// </summary>
    public VehicleFuelSystem(IntPtr ptr) : base(ptr) { }
#endif

    // IL2CPP-safe properties
    public FuelTypeId CurrentFuelType => _currentFuelType;
    public float FuelQuality => _fuelQuality;
    public VehicleType VehicleType => _vehicleType;

    // IL2CPP-safe methods
    public FuelTypeId GetRecommendedFuelType()
    {
        return FuelTypeManager.Instance?.GetRecommendedFuelType(_vehicleType) ?? FuelTypeId.Regular;
    }

    public bool IsFuelCompatible(FuelTypeId fuelTypeId)
    {
        return FuelTypeManager.Instance?.IsFuelCompatible(fuelTypeId, _vehicleType) ?? true;
    }

    public string GetCurrentFuelDisplayName()
    {
        return FuelTypeManager.Instance?.GetFuelDisplayName(_currentFuelType) ?? "Regular";
    }

    // NEW: Fuel type changing logic
    public bool ChangeFuelType(FuelTypeId newFuelType, float refuelAmount)
    {
        try
        {
            if (!IsFuelCompatible(newFuelType))
            {
                ModLogger.LogWarning($"Fuel type {newFuelType} is not compatible with {_vehicleType}");
                return false;
            }

            // Calculate fuel mixing quality impact
            if (_currentFuelType != newFuelType && CurrentFuelLevel > 0.1f)
            {
                CalculateFuelMixingEffect(newFuelType, refuelAmount);
            }
            else
            {
                _currentFuelType = newFuelType;
                _fuelQuality = 1.0f;
            }

            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error("Error changing fuel type", ex);
            return false;
        }
    }

    private void CalculateFuelMixingEffect(FuelTypeId newFuelType, float refuelAmount)
    {
        try
        {
            // Simple fuel mixing calculation
            float totalFuel = CurrentFuelLevel + refuelAmount;
            float oldRatio = CurrentFuelLevel / totalFuel;
            float newRatio = refuelAmount / totalFuel;

            // Quality penalty for mixing incompatible fuels
            if (GetFuelCompatibilityScore(_currentFuelType, newFuelType) < 0.8f)
            {
                _fuelQuality = System.Math.Max(0.7f, _fuelQuality * 0.9f);
            }

            // Switch to new fuel type if it's majority
            if (newRatio > 0.6f)
            {
                _currentFuelType = newFuelType;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error("Error calculating fuel mixing effect", ex);
        }
    }

    private float GetFuelCompatibilityScore(FuelTypeId fuel1, FuelTypeId fuel2)
    {
        if (fuel1 == fuel2) return 1.0f;
        if (fuel1 == FuelTypeId.Regular || fuel2 == FuelTypeId.Regular) return 0.8f;
        return 0.6f; // Premium + Diesel mixing
    }
}
```

### Phase 6: Fuel Station Integration (Week 3)

#### 6.1 Enhanced Fuel Station System
```csharp
// Enhancements to FuelStation.cs
#if !MONO
    [RegisterTypeInIl2Cpp]
#endif
public class FuelStation : InteractableObject
{
    // NEW: Multi-fuel support
    private System.Collections.Generic.Dictionary<FuelTypeId, float> _fuelTypePrices
        = new System.Collections.Generic.Dictionary<FuelTypeId, float>();

    private FuelTypeId _selectedFuelType = FuelTypeId.Regular;
    private bool _showFuelSelectionUI = false;

#if !MONO
    /// <summary>
    /// IL2CPP constructor required for RegisterTypeInIl2Cpp
    /// </summary>
    public FuelStation(IntPtr ptr) : base(ptr) { }
#endif

    protected override void Start()
    {
        try
        {
            base.Start();
            InitializeFuelPrices();
        }
        catch (Exception ex)
        {
            ModLogger.Error("Error in FuelStation.Start", ex);
        }
    }

    private void InitializeFuelPrices()
    {
        try
        {
            if (FuelTypeManager.Instance == null) return;

            foreach (FuelTypeId fuelType in System.Enum.GetValues(typeof(FuelTypeId)))
            {
                _fuelTypePrices[fuelType] = FuelTypeManager.Instance.GetFuelPrice(fuelType);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error("Error initializing fuel prices", ex);
        }
    }

    public override void OnInteract(PlayerInteractionController interactionController)
    {
        if (!CanInteract()) return;

        VehicleFuelSystem fuelSystem = GetNearestVehicleFuelSystem();
        if (fuelSystem == null) return;

        // NEW: Show fuel type selection if multiple types are compatible
        var compatibleFuels = GetCompatibleFuelTypes(fuelSystem);
        if (compatibleFuels.Length > 1)
        {
            ShowFuelSelectionUI(fuelSystem, compatibleFuels);
        }
        else
        {
            _selectedFuelType = compatibleFuels.Length > 0 ? compatibleFuels[0] : FuelTypeId.Regular;
            StartRefueling(fuelSystem);
        }
    }

    private FuelTypeId[] GetCompatibleFuelTypes(VehicleFuelSystem fuelSystem)
    {
        if (FuelTypeManager.Instance == null)
            return new FuelTypeId[] { FuelTypeId.Regular };

        return FuelTypeManager.Instance.GetCompatibleFuelTypesArray(fuelSystem.VehicleType);
    }

    private void ShowFuelSelectionUI(VehicleFuelSystem fuelSystem,
        FuelTypeId[] compatibleFuels)
    {
        // Implementation for fuel selection UI
        _showFuelSelectionUI = true;

        // Display fuel options with prices and compatibility indicators
        foreach (var fuelType in compatibleFuels)
        {
            string displayName = FuelTypeManager.Instance.GetFuelDisplayName(fuelType);
            float price = _fuelTypePrices[fuelType];
            bool isRecommended = fuelType == fuelSystem.GetRecommendedFuelType();

            // UI display logic here
        }
    }

    public void SelectFuelType(FuelTypeId fuelTypeId)
    {
        _selectedFuelType = fuelTypeId;
        _showFuelSelectionUI = false;

        VehicleFuelSystem fuelSystem = GetNearestVehicleFuelSystem();
        if (fuelSystem != null)
        {
            StartRefueling(fuelSystem);
        }
    }

    private void StartRefueling(VehicleFuelSystem fuelSystem)
    {
        // Enhanced refueling with fuel type support
        float selectedPrice = _fuelTypePrices[_selectedFuelType];

        // Start refueling process with selected fuel type
        StartCoroutine(RefuelVehicleWithType(fuelSystem, _selectedFuelType, selectedPrice));
    }
}
```

### Phase 7: Performance Effects Integration (Week 3)

#### 7.1 Vehicle Performance Modifier System
```csharp
// Systems/Performance/FuelPerformanceModifier.cs
namespace S1FuelMod.Systems.Performance
{
    public static class FuelPerformanceModifier
    {
        public struct PerformanceModifiers
        {
            public float TorqueMultiplier;
            public float AccelerationMultiplier;
            public float TopSpeedMultiplier;
            public float ThrottleResponseMultiplier;
            public float EngineEfficiencyMultiplier;
            public float FuelQualityPenalty;
        }

        public static PerformanceModifiers CalculateModifiers(FuelTypeId fuelTypeId,
            VehicleType vehicleType, float fuelQuality, float currentSpeedKmh, float throttleInput)
        {
            if (FuelTypeManager.Instance == null)
                return GetDefaultModifiers();

            // Can now directly access the concrete FuelType class
            var fuelType = FuelTypeManager.Instance.GetFuelType(fuelTypeId);
            var modifiers = new PerformanceModifiers
            {
                TorqueMultiplier = fuelType.TorqueModifier,
                AccelerationMultiplier = fuelType.AccelerationModifier,
                TopSpeedMultiplier = fuelType.TopSpeedModifier,
                ThrottleResponseMultiplier = CalculateThrottleResponse(fuelTypeId, throttleInput),
                EngineEfficiencyMultiplier = fuelType.CalculateConsumptionModifier(currentSpeedKmh, throttleInput, vehicleType),
                FuelQualityPenalty = CalculateQualityPenalty(fuelQuality)
            };

            // Apply compatibility penalties
            if (!fuelType.IsCompatibleWith(vehicleType))
            {
                float penalty = fuelType.GetIncompatibilityPenalty(vehicleType);
                modifiers.TorqueMultiplier *= penalty;
                modifiers.AccelerationMultiplier *= penalty;
                modifiers.TopSpeedMultiplier *= penalty;
            }

            return modifiers;
        }

        private static float CalculateThrottleResponse(FuelTypeId fuelTypeId, float throttleInput)
        {
            // Different fuel types have different throttle response characteristics
            return fuelTypeId switch
            {
                FuelTypeId.Premium => 1.0f + (throttleInput * 0.15f), // More responsive
                FuelTypeId.Diesel => 1.0f - (throttleInput * 0.05f),  // Less responsive at high throttle
                _ => 1.0f
            };
        }

        private static float CalculateQualityPenalty(float fuelQuality)
        {
            // Poor fuel quality reduces performance
            return 0.5f + (fuelQuality * 0.5f);
        }
    }
}
```

### Phase 8: UI Integration (Week 3-4)

#### 8.1 Fuel Gauge Enhancements
```csharp
// Enhancements to UI/FuelGauge.cs
public class FuelGauge : MonoBehaviour
{
    // NEW: Fuel type display
    private UnityEngine.UI.Text _fuelTypeText;
    private UnityEngine.UI.Image _fuelTypeIndicator;
    private UnityEngine.UI.Image _compatibilityWarning;

    public void UpdateFuelTypeDisplay(FuelTypeId fuelTypeId, bool isCompatible, bool isOptimal)
    {
        if (FuelTypeManager.Instance == null) return;

        // Can now directly access the concrete FuelType class
        var fuelType = FuelTypeManager.Instance.GetFuelType(fuelTypeId);
        string displayName = fuelType.DisplayName;
        UnityEngine.Color fuelColor = fuelType.UIColor;

        if (_fuelTypeText != null)
            _fuelTypeText.text = displayName;

        if (_fuelTypeIndicator != null)
            _fuelTypeIndicator.color = fuelColor;

        if (_compatibilityWarning != null)
        {
            _compatibilityWarning.gameObject.SetActive(!isCompatible);
            _compatibilityWarning.color = isOptimal ? UnityEngine.Color.green :
                                         isCompatible ? UnityEngine.Color.yellow : UnityEngine.Color.red;
        }
    }

    public void ShowFuelEfficiencyIndicator(float efficiency)
    {
        // Visual indicator of current fuel efficiency
        // Green = efficient, Yellow = normal, Red = inefficient
        UnityEngine.Color efficiencyColor = efficiency >= 1.0f ? UnityEngine.Color.green :
                                          efficiency >= 0.9f ? UnityEngine.Color.yellow : UnityEngine.Color.red;
    }
}
```

### Phase 9: Configuration Integration (Week 4)

#### 9.1 MelonPreferences Extensions
```csharp
// Core.cs additions
public class Core : MelonMod
{
    // NEW: Fuel type preferences
    private MelonPreferences_Entry<bool>? _enableMultiFuelTypes;
    private MelonPreferences_Entry<bool>? _showFuelCompatibilityWarnings;
    private MelonPreferences_Entry<float>? _fuelTypePriceVariance;
    private MelonPreferences_Entry<bool>? _allowFuelMixing;
    private MelonPreferences_Entry<float>? _speedConsumptionSensitivity;

    public static MelonPreferences_Entry<bool> EnableMultiFuelTypes => Instance?._enableMultiFuelTypes;
    public static MelonPreferences_Entry<bool> ShowFuelCompatibilityWarnings => Instance?._showFuelCompatibilityWarnings;
    public static MelonPreferences_Entry<float> FuelTypePriceVariance => Instance?._fuelTypePriceVariance;
    public static MelonPreferences_Entry<bool> AllowFuelMixing => Instance?._allowFuelMixing;
    public static MelonPreferences_Entry<float> SpeedConsumptionSensitivity => Instance?._speedConsumptionSensitivity;
}
```

### Phase 10: Testing & Optimization (Week 4)

#### 10.1 Compatibility Testing
- **Mono vs IL2CPP**: Ensure all fuel type operations work consistently
- **Save/Load**: Verify fuel type data persists correctly
- **Performance**: Monitor frame rate impact during fuel calculations
- **Network**: Test fuel synchronization in multiplayer

#### 10.2 Balance Testing
- **Consumption Rates**: Verify speed-based consumption feels realistic
- **Fuel Prices**: Ensure economic balance between fuel types
- **Performance Effects**: Validate performance modifiers enhance gameplay

## Implementation Sequence

### Week 1: Foundation
1. **Day 1-2**: Create fuel type interface, enum, and base class
2. **Day 3-4**: Implement concrete fuel type classes (Regular, Premium, Diesel)
3. **Day 5**: Create FuelTypeManager with IL2CPP compatibility
4. **Day 6-7**: Testing and initial integration

### Week 2: Core Integration
1. **Day 1-2**: Enhance VehicleFuelSystem with fuel type support
2. **Day 3-4**: Implement advanced speed-based consumption algorithm
3. **Day 5**: Add fuel type changing and mixing logic
4. **Day 6-7**: Performance modifier system implementation

### Week 3: Station & UI Integration
1. **Day 1-3**: Enhance FuelStation with multi-fuel selection
2. **Day 4-5**: Update fuel gauge UI with type indicators
3. **Day 6-7**: Implement fuel compatibility warnings and indicators

### Week 4: Polish & Testing
1. **Day 1-2**: Add configuration options and preferences
2. **Day 3-4**: Comprehensive testing across Mono/IL2CPP
3. **Day 5-6**: Performance optimization and bug fixes
4. **Day 7**: Final documentation and deployment

## Risk Mitigation

### IL2CPP Compatibility
- **Risk**: Interface return types and complex collections causing runtime errors
- **Mitigation**: Use abstract base class approach consistent with existing codebase patterns:
  - Use `[RegisterTypeInIl2Cpp]` with `IntPtr` constructors for MonoBehaviour classes
  - Use abstract base classes instead of interfaces to avoid IL2CPP interface complications
  - Return concrete base class types which are fully IL2CPP compatible
  - Provide array-based methods instead of List returns for maximum compatibility
  - Wrap all operations in defensive try-catch blocks
  - Use conditional compilation for Mono-specific features (#if MONO)

### Performance Impact
- **Risk**: Complex fuel calculations affecting game performance
- **Mitigation**: Cache calculations, limit update frequency, optimize algorithms

### Save Data Compatibility
- **Risk**: Breaking existing save files
- **Mitigation**: Implement backward-compatible loading with fuel type defaults

### User Experience
- **Risk**: Overwhelming complexity for casual players
- **Mitigation**: Provide simple/advanced modes, clear UI indicators, optional features

## Success Criteria

### Functional Requirements
✅ Three distinct fuel types with unique characteristics
✅ Vehicle-fuel compatibility system with warnings
✅ Enhanced speed-based consumption algorithm
✅ Performance modifiers affecting vehicle behavior
✅ Multi-fuel selection at stations
✅ Updated UI showing fuel type information
✅ Configuration options for customization

### Technical Requirements
✅ Full Mono and IL2CPP compatibility
✅ No runtime errors or assembly registration issues
✅ Stable performance with minimal frame rate impact
✅ Backward compatibility with existing saves
✅ Proper networking for multiplayer environments

### User Experience Requirements
✅ Intuitive fuel selection interface
✅ Clear compatibility and performance indicators
✅ Smooth gameplay with realistic fuel behavior
✅ Configurable complexity for different playstyles
✅ Educational tooltips and help information

## Future Extensibility

### Additional Fuel Types
- **Electric**: For future electric vehicles
- **Hybrid**: Mixed combustion/electric systems
- **Racing**: High-performance competition fuel
- **Eco**: Environmentally friendly alternative

### Advanced Features
- **Fuel Degradation**: Quality reduction over time
- **Regional Pricing**: Different prices by location
- **Seasonal Effects**: Weather impact on efficiency
- **Fuel Stations Brands**: Different station chains with unique characteristics

### Modding Support
- **Plugin Architecture**: Allow third-party fuel type additions
- **API Exposure**: Public interfaces for mod developers
- **Event System**: Hooks for fuel-related events
- **Configuration APIs**: Programmatic preference management

---

*This plan provides a comprehensive roadmap for implementing the multi-fuel type system while maintaining the robust architecture and IL2CPP compatibility of the existing S1FuelMod codebase.*