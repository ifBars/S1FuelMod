
using S1FuelMod.Systems;
using S1FuelMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;



#if !MONO
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
#else
using ScheduleOne.DevUtilities;
using TMPro;
using ScheduleOne.UI;
#endif


namespace S1FuelMod.UI
{
    public class FuelGauge : IDisposable
    {
        private readonly VehicleFuelSystem _fuelSystem;
        private GameObject? _gaugeContainer;
        private RectTransform? _gaugeContainerRect;
        private RectTransform? _gaugeNeedleRect;
        private Image? _gaugeBackground;
        private TextMeshProUGUI? _gaugeText;
        private Canvas? _parentCanvas;
        private CanvasGroup? _gaugeTextGroup;
        private bool _isVisible;
        private float _lastUpdateTime;

        public bool IsVisible => _isVisible && _gaugeContainer != null && _gaugeContainer.activeInHierarchy;

        public FuelGauge(VehicleFuelSystem fuelSystem)
        {
            _fuelSystem = fuelSystem ?? throw new ArgumentNullException(nameof(fuelSystem));
            CreateGauge();
        }

        private void CreateGauge()
        {
            _parentCanvas = FindUICanvas();
            if (_parentCanvas == null)
            {
                ModLogger.Error("FuelGaugeUI: No suitable UI canvas found. Cannot create gauge.");
                return;
            }

            _gaugeContainer = new GameObject("FuelGauge");
            _gaugeContainer.transform.SetParent(_parentCanvas.transform, false);

            _gaugeContainerRect = _gaugeContainer.AddComponent<RectTransform>();
            _gaugeContainerRect.anchorMin = new Vector2(0.42f, 0.1f);
            _gaugeContainerRect.anchorMax = new Vector2(0.42f, 0.1f);
            _gaugeContainerRect.pivot = new Vector2(0f, 1f);
            _gaugeContainerRect.sizeDelta = new Vector2(200f, 75f);

            _gaugeContainerRect.anchoredPosition = Vector2.zero;

            SetupEventListeners();
            CreateGaugeBackground();
            CreateFillElements();
            CreateGaugeNeedle();
            _gaugeContainer.SetActive(true);
        }

        private void SetupEventListeners()
        {
            try
            {
#if MONO
                _fuelSystem.OnFuelLevelChanged.AddListener(OnFuelLevelChanged);
                _fuelSystem.OnFuelPercentageChanged.AddListener(OnFuelPercentageChanged);
                _fuelSystem.OnLowFuelWarning.AddListener(OnLowFuelWarning);
                _fuelSystem.OnCriticalFuelWarning.AddListener(OnCriticalFuelWarning);
                _fuelSystem.OnFuelEmpty.AddListener(OnFuelEmpty);
#else
                _fuelSystem.OnFuelLevelChanged.AddListener((UnityEngine.Events.UnityAction<float>)OnFuelLevelChanged);
                _fuelSystem.OnFuelPercentageChanged.AddListener((UnityEngine.Events.UnityAction<float>)OnFuelPercentageChanged);
                _fuelSystem.OnLowFuelWarning.AddListener((UnityEngine.Events.UnityAction<bool>)OnLowFuelWarning);
                _fuelSystem.OnCriticalFuelWarning.AddListener((UnityEngine.Events.UnityAction<bool>)OnCriticalFuelWarning);
                _fuelSystem.OnFuelEmpty.AddListener((UnityEngine.Events.UnityAction<bool>)OnFuelEmpty);
#endif
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error setting up event listeners", ex);
            }
        }

        private void CreateGaugeBackground()
        {
            try
            {
                GameObject? background = new GameObject("FuelGaugeBackground");
                background.transform.SetParent(_gaugeContainer!.transform, false);

                RectTransform bgRect = background.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                _gaugeBackground = background.AddComponent<Image>();
                _gaugeBackground.color = new Color(0.8f, 0.8f, 0.8f, 0.5f); // Semi-transparent light gray
                _gaugeBackground.type = Image.Type.Simple;

                Outline outline = background.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.8f); // Dark outline
                outline.effectDistance = new Vector2(1, 1);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating gauge background", ex);
            }
        }

        private void CreateFillElements()
        {
            // Create E
            GameObject emptyTextObject = new GameObject("Empty");
            emptyTextObject.transform.SetParent(_gaugeContainer!.transform, false);
            CanvasGroup emptyTextGroup = emptyTextObject.AddComponent<CanvasGroup>();
            RectTransform emptyTextRect = emptyTextObject.AddComponent<RectTransform>();
            emptyTextRect.anchorMin = new Vector2(0.13f, 0.75f);
            emptyTextRect.anchorMax = new Vector2(0.23f, 0.85f);
            emptyTextRect.anchoredPosition = new Vector2(-5f, -2f);
            emptyTextRect.sizeDelta = new Vector2(40f, 20f);

            TextMeshProUGUI emptyText = emptyTextObject.AddComponent<TextMeshProUGUI>();
            emptyText.alignment = TextAlignmentOptions.Left;
            emptyText.fontSize = 18;
            emptyText.fontStyle = FontStyles.Bold;
            emptyText.color = Color.red;
            emptyText.text = "E";

            Shadow emptyShadow = emptyTextObject.AddComponent<Shadow>();
            emptyShadow.effectColor = Color.black;
            emptyShadow.effectDistance = new Vector2(1, -1);

            // Create F
            GameObject fullTextObject = new GameObject("Full");
            fullTextObject.transform.SetParent(_gaugeContainer!.transform, false);
            CanvasGroup fullTextGroup = fullTextObject.AddComponent<CanvasGroup>();
            RectTransform fullTextRect = fullTextObject.AddComponent<RectTransform>();
            fullTextRect.anchorMin = new Vector2(0.77f, 0.75f);
            fullTextRect.anchorMax = new Vector2(0.87f, 0.85f);
            fullTextRect.anchoredPosition = new Vector2(5f, 0f);
            fullTextRect.sizeDelta = new Vector2(40f, 20f);

            TextMeshProUGUI fullText = fullTextObject.AddComponent<TextMeshProUGUI>();
            fullText.alignment = TextAlignmentOptions.Right;
            fullText.fontSize = 18;
            fullText.fontStyle = FontStyles.Bold;
            fullText.color = Color.white;
            fullText.text = "F";

            Shadow fullShadow = fullTextObject.AddComponent<Shadow>();
            fullShadow.effectColor = Color.black;
            fullShadow.effectDistance = new Vector2(1, -1);

            // Rotate E 30 degrees to the left
            emptyTextRect.pivot = new Vector2(0.5f, 0.5f);
            emptyTextRect.localRotation = Quaternion.Euler(0, 0, 40f);

            // Rotate F 30 degrees to the right
            fullTextRect.pivot = new Vector2(0.5f, 0.5f);
            fullTextRect.localRotation = Quaternion.Euler(0, 0, -43f);

            // Create Gauge Ticks
            List<GameObject> ticks = new List<GameObject>();
            int tickCount = 0;
            while (ticks.Count < 5)
            {
                GameObject tickObject = new GameObject($"Fuel Gauge Tick {tickCount + 1}");
                tickObject.transform.SetParent(_gaugeContainer!.transform, false);
                RectTransform tickRect = tickObject.AddComponent<RectTransform>();
                CreateTick(tickCount, tickObject);
                ticks.Add(tickObject);
                tickCount++;
            }

            // Create fuel text readout
            GameObject fuelReadObject = new GameObject("Fuel Readout");
            fuelReadObject.transform.SetParent(_gaugeContainer!.transform, false);
            _gaugeTextGroup = fuelReadObject.AddComponent<CanvasGroup>();
            RectTransform fuelReadRect = fuelReadObject.AddComponent<RectTransform>();
            fuelReadRect.anchorMin = new Vector2(0f, 0.1f);
            fuelReadRect.anchorMax = new Vector2(0.3f, 0.25f);
            fuelReadRect.pivot = new Vector2(0f, 1f);
            fuelReadRect.anchoredPosition = Vector2.zero;
            fuelReadRect.sizeDelta = new Vector2(50f, 40f);

            _gaugeText = fuelReadObject.AddComponent<TextMeshProUGUI>();
            _gaugeText.alignment = TextAlignmentOptions.Left;
            _gaugeText.fontSize = 12;
            _gaugeText.fontStyle = FontStyles.Bold;
            _gaugeText.color = Color.white;
            _gaugeText.text = "50.0L (100%)";

            Shadow fuelTextShadow = fuelReadObject.AddComponent<Shadow>();
            fuelTextShadow.effectColor = Color.black;
            fuelTextShadow.effectDistance = new Vector2(1, -1);
        }

        private void CreateGaugeNeedle()
        {
            GameObject needleObject = new GameObject("FuelGaugeNeedle");
            needleObject.transform.SetParent(_gaugeContainer!.transform, false);
            _gaugeNeedleRect = needleObject.AddComponent<RectTransform>();
            _gaugeNeedleRect.anchorMin = new Vector2(0.5f, 0.01f);
            _gaugeNeedleRect.anchorMax = new Vector2(0.5f, 0.01f);
            _gaugeNeedleRect.sizeDelta = new Vector2(1f, 70f);
            _gaugeNeedleRect.pivot = new Vector2(0.5f, 0f);
            _gaugeNeedleRect.anchoredPosition = new Vector2(0f, 0f);
            Image needleImage = needleObject.AddComponent<Image>();
            needleImage.color = Color.red;
            needleImage.type = Image.Type.Simple;
            // Add shadow for better visibility
            //Outline outline = needleObject.AddComponent<Outline>();
            //outline.effectColor = new Color(0, 0, 0, 0.8f); // Dark outline
            //outline.effectDistance = new Vector2(1, 1);
            //Shadow shadow = needleObject.AddComponent<Shadow>();
            //shadow.effectColor = Color.black;
            //shadow.effectDistance = new Vector2(1, -1);
            //needleObject.SetActive(true);
        }

        private void CreateTick(int index, GameObject tickObject)
        {
            RectTransform tickRect = tickObject.GetComponent<RectTransform>();
            
            switch (index)
            {
                case 0:
                    tickRect.anchorMin = new Vector2(0.5f, 0.75f);
                    tickRect.anchorMax = new Vector2(0.5f, 0.75f);
                    tickRect.anchoredPosition = new Vector2(-77f, -25f);
                    tickRect.sizeDelta = new Vector2(3f, 15f);
                    tickRect.pivot = new Vector2(0.5f, 0.5f);
                    tickRect.localRotation = Quaternion.Euler(0, 0, 55f);
                    break;
                case 1:
                    tickRect.anchorMin = new Vector2(0.5f, 0.75f);
                    tickRect.anchorMax = new Vector2(0.5f, 0.75f);
                    tickRect.anchoredPosition = new Vector2(-40f, -5f);
                    tickRect.sizeDelta = new Vector2(3f, 10f);
                    tickRect.pivot = new Vector2(0.5f, 0.5f);
                    tickRect.localRotation = Quaternion.Euler(0, 0, 30f);
                    break;
                case 2:
                    tickRect.anchorMin = new Vector2(0.5f, 0.75f);
                    tickRect.anchorMax = new Vector2(0.5f, 0.75f);
                    tickRect.anchoredPosition = new Vector2(0f, 0f);
                    tickRect.sizeDelta = new Vector2(3f, 15f);
                    tickRect.pivot = new Vector2(0.5f, 0.5f);
                    tickRect.localRotation = Quaternion.Euler(0, 0, 0f);
                    break;
                case 3:
                    tickRect.anchorMin = new Vector2(0.5f, 0.75f);
                    tickRect.anchorMax = new Vector2(0.5f, 0.75f);
                    tickRect.anchoredPosition = new Vector2(40f, -5f);
                    tickRect.sizeDelta = new Vector2(3f, 10f);
                    tickRect.pivot = new Vector2(0.5f, 0.5f);
                    tickRect.localRotation = Quaternion.Euler(0, 0, -30f);
                    break;
                case 4:
                    tickRect.anchorMin = new Vector2(0.5f, 0.75f);
                    tickRect.anchorMax = new Vector2(0.5f, 0.75f);
                    tickRect.anchoredPosition = new Vector2(77f, -25f);
                    tickRect.sizeDelta = new Vector2(3f, 15f);
                    tickRect.pivot = new Vector2(0.5f, 0.5f);
                    tickRect.localRotation = Quaternion.Euler(0, 0, -55f);
                    break;
                default:
                    ModLogger.Error($"FuelGaugeUI: Invalid tick index {index}. Must be between 0 and 4.");
                    return;
            }
            Image tickImage = tickObject.AddComponent<Image>();
            tickImage.type = Image.Type.Simple;
            tickImage.color = new Color(0.15f, 0.15f, 0.15f, (index == 0 || index == 2 || index == 4) ? 1f : 0.75f); // Full opacity for middle tick, semi-transparent for others
            tickObject.SetActive(true);
        }

        /// <summary>
        /// Find the main UI canvas in the scene
        /// </summary>
        /// <returns>The UI canvas or null if not found</returns>
        private Canvas? FindUICanvas()
        {
            try
            {
                // Check if we're in the correct scene first
                string currentSceneName = SceneManager.GetActiveScene().name;
                ModLogger.UIDebug($"FuelGaugeUI: Current scene: {currentSceneName}");

                // Don't create UI in lobby/menu scenes
                if (currentSceneName.Contains(Constants.Game.MENU_SCENE))
                {
                    ModLogger.UIDebug($"FuelGaugeUI: Skipping UI creation in scene: {currentSceneName}");
                    return null;
                }

                // Try to find HUD Singleton instance first (best approach)
                if (Singleton<HUD>.InstanceExists)
                {
                    Canvas hudCanvas = Singleton<HUD>.Instance.canvas;
                    if (hudCanvas != null)
                    {
                        ModLogger.UIDebug($"FuelGaugeUI: Found HUD Singleton canvas in scene: {currentSceneName}");
                        return hudCanvas;
                    }
                }

                // Fallback: Try to find HUD GameObject
                GameObject hudObject = GameObject.Find("HUD");
                if (hudObject != null)
                {
                    Canvas hudCanvas = hudObject.GetComponent<Canvas>();
                    if (hudCanvas != null)
                    {
                        ModLogger.UIDebug($"FuelGaugeUI: Found HUD GameObject canvas in scene: {currentSceneName}");
                        return hudCanvas;
                    }
                }

                // Last resort: Find any overlay canvas in the main scene, but be selective
                Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in canvases)
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                        !canvas.name.Contains(Constants.Game.MENU_SCENE))
                    {
                        ModLogger.UIDebug($"FuelGaugeUI: Found suitable overlay canvas: {canvas.name} in scene: {currentSceneName}");
                        return canvas;
                    }
                }

                ModLogger.Warning($"FuelGaugeUI: No suitable UI canvas found in scene: {currentSceneName}");
                return null;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error finding UI canvas", ex);
                return null;
            }
        }

        public void Update()
        {
            try
            {
                if (_isVisible || _gaugeContainer == null) return;

                if (Time.time - _lastUpdateTime < Constants.UI.GAUGE_UPDATE_INTERVAL) return;

                UpdateDisplay();
                _lastUpdateTime = Time.time;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error in updating the fuel gauge", ex);
            }
        }

        private void UpdateDisplay()
        {
            try
            {
                if (_fuelSystem == null)
                {
                    ModLogger.UIDebug("FuelGauge: UpdateDisplay called but fuel system is null");
                }
                
                float fuelLevel = _fuelSystem.CurrentFuelLevel;
                float maxCapacity = _fuelSystem.MaxFuelCapacity;
                float percentage = _fuelSystem.FuelPercentage;

                if (_gaugeNeedleRect != null)
                {
                    RotateNeedle();
                }

                // Update text
                if (_gaugeText != null)
                {
                    string newText = $"{fuelLevel:F1}L ({percentage:F1}%)";
                    _gaugeText.text = newText;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating gauge display", ex);
            }
        }

        public void RotateNeedle()
        {
            float currentFuelLevel = _fuelSystem.CurrentFuelLevel;
            float maxCapacity = _fuelSystem.MaxFuelCapacity;

            float percent = currentFuelLevel / maxCapacity;
            float angle = Mathf.Lerp(70f, -70f, percent);

            _gaugeNeedleRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// 
        ///  Event Handlers
        /// 
        private void OnFuelLevelChanged(float fuelLevel)
        {
            if (_isVisible)
                UpdateDisplay();
        }

        private void OnFuelPercentageChanged(float percentage)
        {
            if (_isVisible)
                UpdateDisplay();
        }

        public void Show()
        {
            try
            {
                if (_gaugeContainer != null)
                {
                    _gaugeContainer.SetActive(true);
                    _isVisible = true;

                    UpdateDisplay();
                    ModLogger.UIDebug($"FuelGauge: Shown for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing fuel gauge", ex);
            }
        }

        public void Hide()
        {
            try
            {
                if (_gaugeContainer != null)
                {
                    _gaugeContainer.SetActive(false);
                    _isVisible = false;
                    ModLogger.UIDebug($"FuelGauge: Hidden for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error hiding fuel gauge", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_fuelSystem != null)
                {
#if !MONO
                    _fuelSystem.OnFuelLevelChanged.RemoveListener(new System.Action<float>(OnFuelLevelChanged));
                    _fuelSystem.OnFuelPercentageChanged.RemoveListener(new System.Action<float>(OnFuelPercentageChanged));
                    _fuelSystem.OnLowFuelWarning.RemoveListener(new System.Action<bool>(OnLowFuelWarning));
                    _fuelSystem.OnCriticalFuelWarning.RemoveListener(new System.Action<bool>(OnCriticalFuelWarning));
                    _fuelSystem.OnFuelEmpty.RemoveListener(new System.Action<bool>(OnFuelEmpty));
#else
                    _fuelSystem.OnFuelLevelChanged.RemoveListener(OnFuelLevelChanged);
                    _fuelSystem.OnFuelPercentageChanged.RemoveListener(OnFuelPercentageChanged);
                    _fuelSystem.OnLowFuelWarning.RemoveListener(OnLowFuelWarning);
                    _fuelSystem.OnCriticalFuelWarning.RemoveListener(OnCriticalFuelWarning);
                    _fuelSystem.OnFuelEmpty.RemoveListener(OnFuelEmpty);
#endif
                }

                if (_gaugeContainer != null)
                {
                    UnityEngine.Object.Destroy(_gaugeContainer);
                    _gaugeContainer = null;
                }

                _gaugeNeedleRect = null;
                _gaugeText = null;
                _isVisible = false;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error disposing FuelGauge", ex);
            }
        }

        private void OnLowFuelWarning(bool isActive)
        {
            ModLogger.UIDebug($"FuelGauge: Low fuel warning {(isActive ? "activated" : "deactivated")}");
        }

        private void OnCriticalFuelWarning(bool isActive)
        {
            ModLogger.UIDebug($"FuelGauge: Critical fuel warning {(isActive ? "activated" : "deactivated")}");
        }

        private void OnFuelEmpty(bool isEmpty)
        {
            ModLogger.UIDebug($"FuelGauge: Fuel empty state {(isEmpty ? "activated" : "deactivated")}");
        }
    }
}