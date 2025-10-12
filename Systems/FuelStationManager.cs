using UnityEngine;
using S1FuelMod.Utils;

namespace S1FuelMod.Systems
{
    /// <summary>
    /// Manages fuel stations in the game world
    /// Automatically finds and sets up fuel stations on "Bowser (EMC Merge)" gameobjects
    /// </summary>
    public class FuelStationManager : IDisposable
    {
        private readonly List<FuelStation> _activeFuelStations = new List<FuelStation>();
        private readonly string FUEL_STATION_OBJECT_NAME = "Bowser  (EMC Merge)";
        private readonly string FUEL_STATION_OBJECT_NAME_ALT = "Bowser (EMC Merge)";

        private bool _hasInitialized = false;
        private float _lastStationCheckTime = 0f;
        private const float STATION_CHECK_INTERVAL = 15f; // Check for new stations every 15 seconds
        private const int EXPECTED_FUEL_STATIONS = 4; // Stop checking once we find this many stations
        private bool _shouldStopChecking = false;

        public FuelStationManager()
        {
            ModLogger.Debug("FuelStationManager: Initializing...");

            try
            {
                // Initial scan for fuel stations
                ScanForFuelStations();
                _hasInitialized = true;

                ModLogger.Debug($"FuelStationManager: Initialized with {_activeFuelStations.Count} fuel stations");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error initializing FuelStationManager", ex);
            }
        }

        /// <summary>
        /// Update the fuel station manager
        /// </summary>
        public void Update()
        {
            try
            {
                // Clean up destroyed fuel stations every frame (lightweight operation)
                CleanupDestroyedStations();
                
                // Check for new fuel stations periodically (expensive operation)
                // Stop checking once we've found the expected number of stations
                if (!_shouldStopChecking && Time.time - _lastStationCheckTime > STATION_CHECK_INTERVAL)
                {
                    CheckForNewFuelStations();
                    _lastStationCheckTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in FuelStationManager.Update", ex);
            }
        }

        /// <summary>
        /// Scan for and setup fuel stations in the scene
        /// </summary>
        private void ScanForFuelStations()
        {
            try
            {
                int stationsFound = 0;
                int stationsSetup = 0;

                // Find all GameObjects with the target name (try both variations)
                GameObject[] bowserObjects = FindGameObjectsWithName(FUEL_STATION_OBJECT_NAME);
                GameObject[] bowserObjectsAlt = FindGameObjectsWithName(FUEL_STATION_OBJECT_NAME_ALT);

                // Combine both arrays
                List<GameObject> allBowserObjects = new List<GameObject>();
                allBowserObjects.AddRange(bowserObjects);
                allBowserObjects.AddRange(bowserObjectsAlt);

                foreach (GameObject bowserObject in allBowserObjects)
                {
                    if (bowserObject != null)
                    {
                        stationsFound++;

                        if (SetupFuelStation(bowserObject))
                        {
                            stationsSetup++;
                        }
                    }
                }

                if (stationsFound > 0)
                {
                    ModLogger.Debug($"FuelStationManager: Found {stationsFound} Bowser objects, setup {stationsSetup} new fuel stations");
                }
                else
                {
                    ModLogger.Debug("FuelStationManager: No Bowser objects found in scene");
                }

                // Check if we've found the expected number of stations and can stop checking
                if (_activeFuelStations.Count >= EXPECTED_FUEL_STATIONS && !_shouldStopChecking)
                {
                    _shouldStopChecking = true;
                    ModLogger.Debug($"FuelStationManager: Found expected number of fuel stations ({EXPECTED_FUEL_STATIONS}), stopping periodic checks for performance");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error scanning for fuel stations", ex);
            }
        }

        /// <summary>
        /// Check for new fuel stations that may have loaded in after initialization
        /// </summary>
        private void CheckForNewFuelStations()
        {
            try
            {
                int newStationsFound = 0;

                // Find all GameObjects with the target name (try both variations)
                GameObject[] bowserObjects = FindGameObjectsWithName(FUEL_STATION_OBJECT_NAME);
                GameObject[] bowserObjectsAlt = FindGameObjectsWithName(FUEL_STATION_OBJECT_NAME_ALT);

                // Combine both arrays
                List<GameObject> allBowserObjects = new List<GameObject>();
                allBowserObjects.AddRange(bowserObjects);
                allBowserObjects.AddRange(bowserObjectsAlt);

                foreach (GameObject bowserObject in allBowserObjects)
                {
                    if (bowserObject != null)
                    {
                        // Get the parent object where the FuelStation component should be
                        GameObject parentObject = bowserObject.transform.parent?.gameObject;
                        if (parentObject != null)
                        {
                            // Check if this parent already has a fuel station component
                            FuelStation existingStation = parentObject.GetComponent<FuelStation>();
                            if (existingStation == null)
                            {
                                // New fuel station found, set it up
                                if (SetupFuelStation(bowserObject))
                                {
                                    newStationsFound++;
                                }
                            }
                            else if (!_activeFuelStations.Contains(existingStation))
                            {
                                // Existing station not tracked, add to tracking
                                _activeFuelStations.Add(existingStation);
                                ModLogger.Debug($"FuelStationManager: Added existing fuel station to tracking: {parentObject.name}");
                            }
                        }
                    }
                }

                if (newStationsFound > 0)
                {
                    ModLogger.Debug($"FuelStationManager: Found and setup {newStationsFound} new fuel stations");
                }

                // Check if we've found the expected number of stations and can stop checking
                if (_activeFuelStations.Count >= EXPECTED_FUEL_STATIONS && !_shouldStopChecking)
                {
                    _shouldStopChecking = true;
                    ModLogger.Debug($"FuelStationManager: Found expected number of fuel stations ({EXPECTED_FUEL_STATIONS}), stopping periodic checks for performance");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error checking for new fuel stations", ex);
            }
        }

        /// <summary>
        /// Setup a fuel station on the parent of a bowser object
        /// </summary>
        /// <param name="bowserObject">The bowser GameObject (child object)</param>
        /// <returns>True if setup was successful or already exists</returns>
        private bool SetupFuelStation(GameObject bowserObject)
        {
            try
            {
                // Get the parent object - this is where we want to add the FuelStation component
                GameObject parentObject = bowserObject.transform.parent?.gameObject;
                if (parentObject == null)
                {
                    ModLogger.Warning($"FuelStationManager: Bowser object {bowserObject.name} has no parent - skipping");
                    return false;
                }

                // Check if the parent already has a fuel station component
                FuelStation existingStation = parentObject.GetComponent<FuelStation>();
                if (existingStation != null)
                {
                    // Already has fuel station, make sure it's tracked
                    if (!_activeFuelStations.Contains(existingStation))
                    {
                        _activeFuelStations.Add(existingStation);
                        ModLogger.Debug($"FuelStationManager: Added existing fuel station to tracking: {parentObject.name} (parent of {bowserObject.name})");
                    }
                    return true;
                }

                // Add FuelStation component to the parent object
                FuelStation fuelStation = parentObject.AddComponent<FuelStation>();
                if (fuelStation != null)
                {
                    _activeFuelStations.Add(fuelStation);

                    ModLogger.Debug($"FuelStationManager: Setup fuel station on {parentObject.name} (parent of {bowserObject.name}) at position {parentObject.transform.position}");

                    // Configure the fuel station based on the parent and bowser object setup
                    ConfigureFuelStation(fuelStation, parentObject, bowserObject);

                    return true;
                }
                else
                {
                    ModLogger.Warning($"FuelStationManager: Failed to add FuelStation component to {parentObject.name}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error setting up fuel station for bowser {bowserObject.name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Configure a fuel station based on its parent object and bowser child
        /// </summary>
        /// <param name="fuelStation">The fuel station to configure</param>
        /// <param name="parentObject">The parent object where the FuelStation component is attached</param>
        /// <param name="bowserObject">The bowser child object</param>
        private void ConfigureFuelStation(FuelStation fuelStation, GameObject parentObject, GameObject bowserObject)
        {
            try
            {
                // Try to find an audio source on the parent or its children (including the bowser)
                AudioSource audioSource = parentObject.GetComponentInChildren<AudioSource>();
                if (audioSource != null)
                {
                    ModLogger.Debug($"FuelStationManager: Found existing audio source on fuel station {parentObject.name}");
                }

                ModLogger.Debug($"FuelStationManager: Configured fuel station {parentObject.name} with bowser child {bowserObject.name}");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error configuring fuel station for {parentObject.name}", ex);
            }
        }

        /// <summary>
        /// Find all GameObjects with a specific name in the scene
        /// </summary>
        /// <param name="name">Name to search for</param>
        /// <returns>Array of GameObjects with matching names</returns>
        private GameObject[] FindGameObjectsWithName(string name)
        {
            List<GameObject> found = new List<GameObject>();

            // Get all GameObjects in the scene
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

            foreach (GameObject obj in allObjects)
            {
                if (obj.name == name)
                {
                    found.Add(obj);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Clean up destroyed fuel stations from tracking
        /// </summary>
        private void CleanupDestroyedStations()
        {
            try
            {
                for (int i = _activeFuelStations.Count - 1; i >= 0; i--)
                {
                    if (_activeFuelStations[i] == null || _activeFuelStations[i].gameObject == null)
                    {
                        _activeFuelStations.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error cleaning up destroyed fuel stations", ex);
            }
        }

        /// <summary>
        /// Get all active fuel stations
        /// </summary>
        /// <returns>Read-only collection of active fuel stations</returns>
        public IReadOnlyList<FuelStation> GetActiveFuelStations()
        {
            return _activeFuelStations.AsReadOnly();
        }

        /// <summary>
        /// Get fuel station statistics
        /// </summary>
        /// <returns>Statistics about fuel stations</returns>
        public FuelStationStats GetStatistics()
        {
            var stats = new FuelStationStats
            {
                TotalStations = _activeFuelStations.Count,
                ActiveStations = 0,
                InactiveStations = 0
            };

            try
            {
                foreach (var station in _activeFuelStations)
                {
                    if (station != null && station.gameObject != null && station.enabled)
                    {
                        stats.ActiveStations++;
                    }
                    else
                    {
                        stats.InactiveStations++;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error calculating fuel station statistics", ex);
            }

            return stats;
        }

        /// <summary>
        /// Force a rescan for fuel stations
        /// </summary>
        public void ForceScan()
        {
            ModLogger.Debug("FuelStationManager: Forcing fuel station scan...");

            // Temporarily re-enable checking in case fuel stations were destroyed and new ones added
            _shouldStopChecking = false;
            
            ScanForFuelStations();
        }

        /// <summary>
        /// Dispose of the fuel station manager
        /// </summary>
        public void Dispose()
        {
            try
            {
                ModLogger.Debug("FuelStationManager: Disposing...");

                _activeFuelStations.Clear();

                ModLogger.Debug("FuelStationManager: Disposed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error disposing FuelStationManager", ex);
            }
        }
    }

    /// <summary>
    /// Statistics about fuel stations
    /// </summary>
    public class FuelStationStats
    {
        public int TotalStations { get; set; }
        public int ActiveStations { get; set; }
        public int InactiveStations { get; set; }
    }
}