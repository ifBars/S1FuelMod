using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using S1FuelMod.Utils;
using S1FuelMod.Systems.FuelTypes;
#if !MONO
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.Levelling;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppTMPro;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Law;
using ScheduleOne.Levelling;
using TMPro;
#endif

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Individual fuel sign component that manages fuel price displays
    /// Attaches to gas station sign GameObjects to display current fuel prices
    /// </summary>
#if !MONO
    [MelonLoader.RegisterTypeInIl2Cpp]
#endif
    public class FuelSign : MonoBehaviour
    {
        // Sign structure: GameObject 0-3 (front), GameObject 4-7 (back)
        // Each GameObject has Name and Price children with TMP_Text components
        private readonly Dictionary<int, FuelSignDisplay> _signDisplays = new Dictionary<int, FuelSignDisplay>();
        private bool _hasInitialized = false;

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public FuelSign(IntPtr ptr) : base(ptr) { }

        /// <summary>
        /// Mono-side constructor for instantiation from managed code
        /// </summary>
        public FuelSign() : base(ClassInjector.DerivedConstructorPointer<FuelSign>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
#endif

        private void Start()
        {
            try
            {
                InitializeFuelSign();
                ModLogger.Debug($"FuelSign initialized for {gameObject.name}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error initializing FuelSign", ex);
            }
        }

        /// <summary>
        /// Initialize the fuel sign by finding and caching all display components
        /// </summary>
        private void InitializeFuelSign()
        {
            try
            {
                _signDisplays.Clear();

                // Look for GameObject 0-7 (fuel type positions)
                // GameObject (unnumbered) = position 0
                // GameObject (0) = position 1
                // GameObject (1) = position 2
                // etc.
                
                // First check for the unnumbered GameObject
                Transform unnumberedObject = transform.Find("GameObject");
                if (unnumberedObject != null)
                {
                    FuelSignDisplay display = CreateFuelSignDisplay(unnumberedObject, 0);
                    if (display != null)
                    {
                        _signDisplays[0] = display;
                        ModLogger.Debug($"FuelSign: Found fuel display for unnumbered GameObject (position 0)");
                    }
                }

                // Then check for numbered GameObjects (1-7)
                for (int i = 1; i < 8; i++)
                {
                    Transform fuelTypeObject = transform.Find($"GameObject ({i})");
                    if (fuelTypeObject != null)
                    {
                        FuelSignDisplay display = CreateFuelSignDisplay(fuelTypeObject, i);
                        if (display != null)
                        {
                            _signDisplays[i] = display;
                            ModLogger.Debug($"FuelSign: Found fuel display for position {i}");
                        }
                    }
                }

                if (_signDisplays.Count > 0)
                {
                    ModLogger.Debug($"FuelSign: Initialized with {_signDisplays.Count} fuel displays");
                    
                    // Update prices immediately after initialization
                    UpdateFuelPrices();
                }
                else
                {
                    ModLogger.Warning($"FuelSign: No fuel displays found for {gameObject.name}");
                }

                _hasInitialized = true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error initializing fuel sign displays", ex);
            }
        }

        /// <summary>
        /// Create a fuel sign display from a GameObject transform
        /// </summary>
        /// <param name="fuelTypeObject">The GameObject transform containing Name and Price children</param>
        /// <param name="position">Position index for this display</param>
        /// <returns>FuelSignDisplay object or null if creation failed</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private FuelSignDisplay? CreateFuelSignDisplay(Transform fuelTypeObject, int position)
        {
            try
            {
                // Find Name and Price children
                Transform nameTransform = fuelTypeObject.Find("Name");
                Transform priceTransform = fuelTypeObject.Find("Price");

                if (nameTransform == null || priceTransform == null)
                {
                    ModLogger.Debug($"FuelSign: Missing Name or Price child for position {position}");
                    return null;
                }

                // Get TMP_Text components
                TMP_Text nameText = nameTransform.GetComponent<TMP_Text>();
                TMP_Text priceText = priceTransform.GetComponent<TMP_Text>();

                if (nameText == null || priceText == null)
                {
                    ModLogger.Debug($"FuelSign: Missing TMP_Text components for position {position}");
                    return null;
                }

                return new FuelSignDisplay
                {
                    Position = position,
                    NameText = nameText,
                    PriceText = priceText,
                    FuelTypeId = GetFuelTypeForPosition(position)
                };
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error creating fuel sign display for position {position}", ex);
                return null;
            }
        }

        /// <summary>
        /// Get the fuel type for a specific position
        /// </summary>
        /// <param name="position">Position index (0-7)</param>
        /// <returns>FuelTypeId for the position</returns>
        private FuelTypeId GetFuelTypeForPosition(int position)
        {
            // Front side (0-3): Regular, Mid-Grade, Premium, Diesel
            // Back side (4-7): Regular, Mid-Grade, Premium, Diesel
            switch (position)
            {
                case 0:
                case 4:
                    return FuelTypeId.Regular;
                case 1:
                case 5:
                    return FuelTypeId.MidGrade;
                case 2:
                case 6:
                    return FuelTypeId.Premium;
                case 3:
                case 7:
                    return FuelTypeId.Diesel;
                default:
                    return FuelTypeId.Regular; // Default fallback
            }
        }

        /// <summary>
        /// Update all fuel prices on this sign
        /// </summary>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        public void UpdateFuelPrices()
        {
            if (!_hasInitialized)
            {
                ModLogger.Debug("FuelSign: Not initialized yet, skipping price update");
                return;
            }

            try
            {
                foreach (var kvp in _signDisplays)
                {
                    FuelSignDisplay display = kvp.Value;
                    UpdateFuelDisplay(display);
                }

                ModLogger.Debug($"FuelSign: Updated {_signDisplays.Count} fuel displays for {gameObject.name}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating fuel prices", ex);
            }
        }

        /// <summary>
        /// Update a specific fuel display with current price and name
        /// </summary>
        /// <param name="display">The fuel display to update</param>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private void UpdateFuelDisplay(FuelSignDisplay display)
        {
            try
            {
                if (display.NameText == null || display.PriceText == null)
                {
                    ModLogger.Debug($"FuelSign: Display components are null for position {display.Position}");
                    return;
                }

                // Get fuel type information
                string fuelName = GetFuelDisplayName(display.FuelTypeId);
                float fuelPrice = GetFuelPrice(display.FuelTypeId);

                // Update the text components
                display.NameText.text = fuelName;
                display.PriceText.text = $"${fuelPrice:F2}";

                ModLogger.Debug($"FuelSign: Updated position {display.Position} - {fuelName}: ${fuelPrice:F2}");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error updating fuel display for position {display.Position}", ex);
            }
        }

        /// <summary>
        /// Get the display name for a fuel type
        /// </summary>
        /// <param name="fuelTypeId">The fuel type ID</param>
        /// <returns>Display name for the fuel type</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private string GetFuelDisplayName(FuelTypeId fuelTypeId)
        {
            try
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
            catch (Exception ex)
            {
                ModLogger.Error($"Error getting fuel display name for {fuelTypeId}", ex);
                return fuelTypeId.ToString();
            }
        }

        /// <summary>
        /// Get the current price for a fuel type
        /// </summary>
        /// <param name="fuelTypeId">The fuel type ID</param>
        /// <returns>Current price per liter</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private float GetFuelPrice(FuelTypeId fuelTypeId)
        {
            try
            {
                // Get base price from Core
                float basePrice = Core.Instance?.BaseFuelPricePerLiter ?? Constants.Fuel.FUEL_PRICE_PER_LITER;

                // Apply dynamic pricing if enabled
                if (Core.Instance?.EnableDynamicPricing == true)
                {
                    basePrice = CalculateDynamicPrice(basePrice);
                }

                // Apply fuel type multiplier
                if (FuelTypeManager.Instance != null)
                {
                    var fuelType = FuelTypeManager.Instance.GetFuelType(fuelTypeId);
                    if (fuelType != null)
                    {
                        basePrice *= Mathf.Max(0.01f, fuelType.PriceMultiplier);
                    }
                }

                return basePrice;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error getting fuel price for {fuelTypeId}", ex);
                return Constants.Fuel.FUEL_PRICE_PER_LITER;
            }
        }

        /// <summary>
        /// Calculate dynamic price based on time and tier modifiers
        /// </summary>
        /// <param name="basePrice">Base price per liter</param>
        /// <returns>Adjusted price with dynamic modifiers</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private float CalculateDynamicPrice(float basePrice)
        {
            try
            {
                float timeModifier = 0f;

                // Calculate time-based modifier
                if (NetworkSingleton<TimeManager>.InstanceExists)
                {
                    int dayIndex = NetworkSingleton<TimeManager>.Instance.DayIndex;
                    int hashCode = ("Petrol" + dayIndex.ToString()).GetHashCode();
                    timeModifier = Mathf.Lerp(0f, 0.2f, Mathf.InverseLerp(-2.1474836E+09f, 2.1474836E+09f, (float)hashCode));
                }

                // Calculate tier multiplier
                float tierMultiplier = GetTierMultiplier();

                // Apply modifiers
                float adjustedPrice = Core.Instance.EnablePricingOnTier
                    ? basePrice + (basePrice * timeModifier) * tierMultiplier
                    : basePrice + (basePrice * timeModifier);

                // Apply curfew tax if enabled
                return ApplyCurfewTax(adjustedPrice);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error calculating dynamic price", ex);
                return basePrice;
            }
        }

        /// <summary>
        /// Get tier multiplier based on player rank
        /// </summary>
        /// <returns>Tier multiplier</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private float GetTierMultiplier()
        {
            try
            {
                if (!NetworkSingleton<LevelManager>.InstanceExists)
                {
                    return 1f;
                }

                switch (NetworkSingleton<LevelManager>.Instance.Rank)
                {
                    case ERank.Hoodlum:
                        return 1.05f;
                    case ERank.Peddler:
                        return 1.1f;
                    case ERank.Hustler:
                        return 1.15f;
                    case ERank.Bagman:
                        return 1.2f;
                    case ERank.Enforcer:
                        return 1.25f;
                    case ERank.Shot_Caller:
                        return 1.3f;
                    case ERank.Block_Boss:
                        return 1.4f;
                    case ERank.Underlord:
                        return 1.5f;
                    case ERank.Baron:
                        return 1.6f;
                    case ERank.Kingpin:
                        return 1.8f;
                    default:
                        return 1f;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error retrieving tier multiplier", ex);
                return 1f;
            }
        }

        /// <summary>
        /// Apply curfew tax to price if curfew is active
        /// </summary>
        /// <param name="price">Price to potentially tax</param>
        /// <returns>Price with curfew tax applied if applicable</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        private float ApplyCurfewTax(float price)
        {
            if (Core.Instance?.EnableCurfewFuelTax != true)
            {
                return price;
            }

            try
            {
#if MONO
                bool curfewActive = NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance.IsCurrentTimeWithinRange(CurfewManager.CURFEW_START_TIME, CurfewManager.CURFEW_END_TIME);
#else
                bool curfewActive = NetworkSingleton<Il2CppScheduleOne.GameTime.TimeManager>.Instance.IsCurrentTimeWithinRange(CurfewManager.CURFEW_START_TIME, CurfewManager.CURFEW_END_TIME);
#endif
                if (curfewActive)
                {
                    return price * 2f;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error applying curfew tax", ex);
            }

            return price;
        }

        /// <summary>
        /// Get statistics about this fuel sign
        /// </summary>
        /// <returns>Statistics about the fuel sign</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        public FuelSignStats GetStatistics()
        {
            return new FuelSignStats
            {
                SignName = gameObject.name,
                TotalDisplays = _signDisplays.Count,
                ActiveDisplays = _signDisplays.Count // All displays are considered active if they exist
            };
        }

        private void OnDestroy()
        {
            try
            {
                _signDisplays.Clear();
                ModLogger.Debug($"FuelSign destroyed for {gameObject.name}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error destroying FuelSign", ex);
            }
        }
    }

    /// <summary>
    /// Represents a single fuel display on a sign
    /// </summary>
    public class FuelSignDisplay
    {
        public int Position { get; set; }
        public TMP_Text? NameText { get; set; }
        public TMP_Text? PriceText { get; set; }
        public FuelTypeId FuelTypeId { get; set; }
    }

    /// <summary>
    /// Statistics about a fuel sign
    /// </summary>
    public class FuelSignStats
    {
        public string SignName { get; set; } = string.Empty;
        public int TotalDisplays { get; set; }
        public int ActiveDisplays { get; set; }
    }
}
