using UnityEngine;
using S1FuelMod.Utils;
#if !MONO
using Il2CppInterop.Runtime.Injection;

// using Il2CppSystem.Collections.Generic;
#else
using TMPro;
#endif

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Manages fuel price signs at gas stations
    /// Automatically finds and updates fuel price displays on gas station signs
    /// </summary>
#if !MONO
    [MelonLoader.RegisterTypeInIl2Cpp]
#endif
    public class FuelSignManager : MonoBehaviour
    {
        private static FuelSignManager? _instance;
        public static FuelSignManager? Instance => _instance;

        private readonly List<FuelSign> _activeFuelSigns = new List<FuelSign>();
        
        // Paths to fuel signs in the scene hierarchy
        // Based on actual game hierarchy: Map/Hyland Point/Region_Downtown/Gas Station/gas station/Sign (X)
        private readonly string[] FUEL_SIGN_PATHS = new string[]
        {
            "Map/Hyland Point/Region_Downtown/Gas Station/gas station/Sign",
            "Map/Hyland Point/Region_Downtown/Gas Station/gas station/Sign (1)",
            "Map/Hyland Point/Region_Westville/Slums Gas Station/gas station/Sign"
        };

        private bool _hasInitialized = false;
        private float _lastSignCheckTime = 0f;
        private const float SIGN_CHECK_INTERVAL = 5f; // Check for new signs every 5 seconds (reduced from 10s)
        private const int EXPECTED_FUEL_SIGNS = 3; // Expected number of signs
        private bool _shouldStopChecking = false;
        private int _initializationAttempts = 0;
        private const int MAX_INITIALIZATION_ATTEMPTS = 6; // Try for 30 seconds (6 * 5s intervals)
        private const float INITIAL_DELAY = 3f; // Wait 3 seconds before first initialization attempt
        private float _startTime = 0f;
        private bool _delayedInitStarted = false;

        // Fuel type mapping for sign positions
        // GameObject 0-3: Front side (Regular, Mid-Grade, Premium, Diesel)
        // GameObject 4-7: Back side (Regular, Mid-Grade, Premium, Diesel)
        private readonly FuelTypeId[] FRONT_SIDE_FUEL_TYPES = new FuelTypeId[]
        {
            FuelTypeId.Regular,
            FuelTypeId.MidGrade,
            FuelTypeId.Premium,
            FuelTypeId.Diesel
        };

        private readonly FuelTypeId[] BACK_SIDE_FUEL_TYPES = new FuelTypeId[]
        {
            FuelTypeId.Regular,
            FuelTypeId.MidGrade,
            FuelTypeId.Premium,
            FuelTypeId.Diesel
        };

#if !MONO
        /// <summary>
        /// IL2CPP constructor required for RegisterTypeInIl2Cpp
        /// </summary>
        public FuelSignManager(IntPtr ptr) : base(ptr) { }

        /// <summary>
        /// Mono-side constructor for instantiation from managed code
        /// </summary>
        public FuelSignManager() : base(ClassInjector.DerivedConstructorPointer<FuelSignManager>())
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
                
                ModLogger.Debug("FuelSignManager initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelSignManager.Awake", ex);
            }
        }

        private void Start()
        {
            // Record start time to delay first initialization
            _startTime = Time.time;
            ModLogger.Debug($"FuelSignManager.Start() called, will attempt initialization after {INITIAL_DELAY}s delay");
        }

        private void Update()
        {
            try
            {
                // Wait for initial delay before first initialization attempt
                if (!_delayedInitStarted && Time.time - _startTime >= INITIAL_DELAY)
                {
                    _delayedInitStarted = true;
                    _lastSignCheckTime = Time.time;
                    ModLogger.Debug("Initial delay complete, starting fuel sign initialization");
                    InitializeFuelSigns();
                }

                // Only proceed with update logic after delayed init has started
                if (!_delayedInitStarted)
                {
                    return;
                }

                // Clean up destroyed fuel signs every frame (lightweight operation)
                CleanupDestroyedSigns();
                
                // Check for new fuel signs periodically (expensive operation)
                // Only continue checking if we haven't found all expected signs yet
                if (!_shouldStopChecking && Time.time - _lastSignCheckTime > SIGN_CHECK_INTERVAL)
                {
                    CheckForNewFuelSigns();
                    _lastSignCheckTime = Time.time;
                }

                // Note: Removed periodic price updates since FuelStation.UpdateFuelTypePriceCache()
                // already calls UpdateAllFuelSigns() when prices change, making this redundant
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelSignManager.Update", ex);
            }
        }

        /// <summary>
        /// Initialize fuel signs by scanning for them in the scene
        /// </summary>
        private void InitializeFuelSigns()
        {
            try
            {
                _initializationAttempts++;
                int signsFound = 0;
                int signsSetup = 0;

                foreach (string signPath in FUEL_SIGN_PATHS)
                {
                    GameObject signObject = FindGameObjectByPath(signPath);
                    if (signObject != null)
                    {
                        signsFound++;
                        ModLogger.Debug($"FuelSignManager: Found sign at path: {signPath}");
                        
                        if (SetupFuelSign(signObject))
                        {
                            signsSetup++;
                        }
                    }
                    else
                    {
                        ModLogger.Debug($"FuelSignManager: Could not find sign at path: {signPath}");
                    }
                }

                if (signsFound > 0)
                {
                    ModLogger.Debug($"FuelSignManager: Attempt {_initializationAttempts} - Found {signsFound} fuel sign objects, setup {signsSetup} new fuel signs");
                }
                else
                {
                    ModLogger.Debug($"FuelSignManager: Attempt {_initializationAttempts} - No fuel sign objects found in scene");
                }

                // Check if we've found the expected number of signs and can stop checking
                if (_activeFuelSigns.Count >= EXPECTED_FUEL_SIGNS && !_shouldStopChecking)
                {
                    _shouldStopChecking = true;
                    ModLogger.Debug($"FuelSignManager: Found expected number of fuel signs ({EXPECTED_FUEL_SIGNS}), stopping periodic checks for performance");
                }
                // Stop checking after maximum attempts to avoid infinite scanning
                else if (_initializationAttempts >= MAX_INITIALIZATION_ATTEMPTS && !_shouldStopChecking)
                {
                    _shouldStopChecking = true;
                    ModLogger.Debug($"FuelSignManager: Reached maximum initialization attempts ({MAX_INITIALIZATION_ATTEMPTS}), stopping periodic checks. Found {_activeFuelSigns.Count}/{EXPECTED_FUEL_SIGNS} signs.");
                }

                _hasInitialized = true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error initializing fuel signs", ex);
            }
        }

        /// <summary>
        /// Check for new fuel signs that may have loaded in after initialization
        /// </summary>
        private void CheckForNewFuelSigns()
        {
            try
            {
                _initializationAttempts++;
                int newSignsFound = 0;

                foreach (string signPath in FUEL_SIGN_PATHS)
                {
                    GameObject signObject = FindGameObjectByPath(signPath);
                    if (signObject != null)
                    {
                        // Check if this sign already has a FuelSign component
                        FuelSign existingSign = signObject.GetComponent<FuelSign>();
                        if (existingSign == null)
                        {
                            // New fuel sign found, set it up
                            if (SetupFuelSign(signObject))
                            {
                                newSignsFound++;
                            }
                        }
                        else if (!_activeFuelSigns.Contains(existingSign))
                        {
                            // Existing sign not tracked, add to tracking
                            _activeFuelSigns.Add(existingSign);
                            ModLogger.Debug($"FuelSignManager: Added existing fuel sign to tracking: {signObject.name}");
                        }
                    }
                }

                if (newSignsFound > 0)
                {
                    ModLogger.Debug($"FuelSignManager: Attempt {_initializationAttempts} - Found and setup {newSignsFound} new fuel signs");
                }

                // Check if we've found the expected number of signs and can stop checking
                if (_activeFuelSigns.Count >= EXPECTED_FUEL_SIGNS && !_shouldStopChecking)
                {
                    _shouldStopChecking = true;
                    ModLogger.Debug($"FuelSignManager: Found expected number of fuel signs ({EXPECTED_FUEL_SIGNS}), stopping periodic checks for performance");
                }
                // Stop checking after maximum attempts to avoid infinite scanning
                else if (_initializationAttempts >= MAX_INITIALIZATION_ATTEMPTS && !_shouldStopChecking)
                {
                    _shouldStopChecking = true;
                    ModLogger.Debug($"FuelSignManager: Reached maximum initialization attempts ({MAX_INITIALIZATION_ATTEMPTS}), stopping periodic checks. Found {_activeFuelSigns.Count}/{EXPECTED_FUEL_SIGNS} signs.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error checking for new fuel signs", ex);
            }
        }

        /// <summary>
        /// Setup a fuel sign component on a sign GameObject
        /// </summary>
        /// <param name="signObject">The sign GameObject</param>
        /// <returns>True if setup was successful</returns>
        private bool SetupFuelSign(GameObject signObject)
        {
            try
            {
                // Check if the sign already has a fuel sign component
                FuelSign existingSign = signObject.GetComponent<FuelSign>();
                if (existingSign != null)
                {
                    // Already has fuel sign, make sure it's tracked
                    if (!_activeFuelSigns.Contains(existingSign))
                    {
                        _activeFuelSigns.Add(existingSign);
                        ModLogger.Debug($"FuelSignManager: Added existing fuel sign to tracking: {signObject.name}");
                    }
                    return true;
                }

                // Add FuelSign component to the sign object
                FuelSign fuelSign = signObject.AddComponent<FuelSign>();
                if (fuelSign != null)
                {
                    _activeFuelSigns.Add(fuelSign);
                    ModLogger.Debug($"FuelSignManager: Setup fuel sign on {signObject.name} at position {signObject.transform.position}");
                    return true;
                }
                else
                {
                    ModLogger.Warning($"FuelSignManager: Failed to add FuelSign component to {signObject.name}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error setting up fuel sign for {signObject.name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Find a GameObject by its full path in the hierarchy
        /// </summary>
        /// <param name="path">Full path to the GameObject</param>
        /// <returns>GameObject if found, null otherwise</returns>
        private GameObject? FindGameObjectByPath(string path)
        {
            try
            {
                // Split the path into individual names
                string[] pathParts = path.Split('/');
                if (pathParts.Length == 0)
                {
                    return null;
                }

                // Start from the root and traverse down
                Transform current = null;
                foreach (string part in pathParts)
                {
                    if (current == null)
                    {
                        // Find the root object
                        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                        foreach (GameObject rootObj in rootObjects)
                        {
                            if (rootObj.name == part)
                            {
                                current = rootObj.transform;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Find child with matching name
                        Transform child = current.Find(part);
                        if (child != null)
                        {
                            current = child;
                        }
                        else
                        {
                            // Try to find by searching all children
                            for (int i = 0; i < current.childCount; i++)
                            {
                                Transform childTransform = current.GetChild(i);
                                if (childTransform.name == part)
                                {
                                    current = childTransform;
                                    break;
                                }
                            }
                        }
                    }

                    if (current == null)
                    {
                        return null;
                    }
                }

                return current?.gameObject;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error finding GameObject by path '{path}'", ex);
                return null;
            }
        }

        /// <summary>
        /// Clean up destroyed fuel signs from tracking
        /// </summary>
        private void CleanupDestroyedSigns()
        {
            try
            {
                for (int i = _activeFuelSigns.Count - 1; i >= 0; i--)
                {
                    if (_activeFuelSigns[i] == null || _activeFuelSigns[i].gameObject == null)
                    {
                        _activeFuelSigns.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error cleaning up destroyed fuel signs", ex);
            }
        }

        /// <summary>
        /// Update all fuel signs with current prices
        /// </summary>
        public void UpdateAllFuelSigns()
        {
            try
            {
                int updatedCount = 0;
                foreach (FuelSign fuelSign in _activeFuelSigns)
                {
                    if (fuelSign != null && fuelSign.gameObject != null)
                    {
                        fuelSign.UpdateFuelPrices();
                        updatedCount++;
                    }
                }
                
                if (updatedCount > 0)
                {
                    ModLogger.Debug($"FuelSignManager: Updated {updatedCount} fuel signs with current prices");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating fuel signs", ex);
            }
        }

        /// <summary>
        /// Force immediate update of all fuel signs (used when prices change)
        /// </summary>
        public void ForceUpdateAllSigns()
        {
            try
            {
                ModLogger.Debug("FuelSignManager: Forcing immediate update of all fuel signs");
                UpdateAllFuelSigns();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error forcing fuel sign updates", ex);
            }
        }

        /// <summary>
        /// Get fuel type for a specific position on the sign
        /// </summary>
        /// <param name="position">Position index (0-7)</param>
        /// <returns>FuelTypeId for the position</returns>
        public FuelTypeId GetFuelTypeForPosition(int position)
        {
            if (position >= 0 && position < 4)
            {
                return FRONT_SIDE_FUEL_TYPES[position];
            }
            else if (position >= 4 && position < 8)
            {
                return BACK_SIDE_FUEL_TYPES[position - 4];
            }
            
            return FuelTypeId.Regular; // Default fallback
        }

        /// <summary>
        /// Get all active fuel signs
        /// </summary>
        /// <returns>Read-only collection of active fuel signs</returns>
        #if !MONO
        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        #endif
        public IReadOnlyList<FuelSign> GetActiveFuelSigns()
        {
            return _activeFuelSigns.AsReadOnly();
        }

        /// <summary>
        /// Force a rescan for fuel signs
        /// </summary>
        public void ForceScan()
        {
            ModLogger.Debug("FuelSignManager: Forcing fuel sign scan...");

            // Reset attempt counter and re-enable checking
            _initializationAttempts = 0;
            _shouldStopChecking = false;
            
            InitializeFuelSigns();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}

