using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ScheduleOne.UI;
using ScheduleOne.DevUtilities;
using S1FuelMod.Utils;
using S1FuelMod.Systems;

namespace S1FuelMod.UI
{
    /// <summary>
    /// UI component that displays fuel level for a vehicle
    /// </summary>
    public class FuelGaugeUI : IDisposable
    {
        private readonly VehicleFuelSystem _fuelSystem;
        private GameObject? _gaugeContainer;
        private RectTransform? _gaugeRect;
        private Image? _gaugeBackground;
        //private Image? _gaugeFill;
        private Slider? _gaugeSlider;
        private TextMeshProUGUI? _fuelText;
        private Image? _warningIcon;
        private Image? _gaugeSliderImage;

        private bool _isVisible = false;
        private float _lastUpdateTime = 0f;
        private Canvas? _parentCanvas;
        private CanvasGroup? _fuelTextParentGroup;

        public bool IsVisible => _isVisible && _gaugeContainer != null && _gaugeContainer.activeInHierarchy;

        public FuelGaugeUI(VehicleFuelSystem fuelSystem)
        {
            _fuelSystem = fuelSystem ?? throw new ArgumentNullException(nameof(fuelSystem));

            try
            {
                CreateGaugeUI();
                SetupEventListeners();
                UpdateDisplay();

                ModLogger.UIDebug($"FuelGaugeUI: Created for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating FuelGaugeUI", ex);
            }
        }

        /// <summary>
        /// Create the fuel gauge UI elements
        /// </summary>
        private void CreateGaugeUI()
        {
            try
            {
                // Find the main UI canvas
                _parentCanvas = FindUICanvas();
                if (_parentCanvas == null)
                {
                    ModLogger.Error("FuelGaugeUI: Could not find UI canvas");
                    return;
                }

                // Create main container
                _gaugeContainer = new GameObject("FuelGauge");
                _gaugeContainer.transform.SetParent(_parentCanvas.transform, false);

                // Setup RectTransform
                _gaugeRect = _gaugeContainer.AddComponent<RectTransform>();
                _gaugeRect.anchorMin = new Vector2(0.02f, 0.95f); // Top-left area
                _gaugeRect.anchorMax = new Vector2(0.02f, 0.95f);
                _gaugeRect.pivot = new Vector2(0f, 1f);
                _gaugeRect.sizeDelta = new Vector2(Constants.UI.GAUGE_WIDTH, Constants.UI.GAUGE_HEIGHT + 25f); // Extra space for text
                _gaugeRect.anchoredPosition = Vector2.zero;

                // Create background
                CreateGaugeBackground();

                // Create fill bar
                CreateGaugeFill();

                // Create text display
                CreateFuelText();

                // Create warning icon
                CreateWarningIcon();

                // Initially hide the gauge
                _gaugeContainer.SetActive(false);

                ModLogger.UIDebug("FuelGaugeUI: UI elements created successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating gauge UI elements", ex);
            }
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

        /// <summary>
        /// Create the gauge background
        /// </summary>
        private void CreateGaugeBackground()
        {
            try
            {
                GameObject background = new GameObject("Background");
                background.transform.SetParent(_gaugeContainer!.transform, false);

                RectTransform bgRect = background.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = new Vector2(1f, 0.8f); // Leave space for text
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                _gaugeBackground = background.AddComponent<Image>();
                _gaugeBackground.color = Constants.UI.Colors.GAUGE_BACKGROUND;
                _gaugeBackground.type = Image.Type.Simple;

                // Add border
                Outline outline = background.AddComponent<Outline>();
                outline.effectColor = Constants.UI.Colors.GAUGE_BORDER;
                outline.effectDistance = new Vector2(1, 1);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating gauge background", ex);
            }
        }

        /// <summary>
        /// Create the gauge fill bar
        /// </summary>
        private void CreateGaugeFill()
        {
            try
            {
                GameObject fill = new GameObject("Fill");
                fill.transform.SetParent(_gaugeContainer!.transform, false);

                RectTransform fillRect = fill.AddComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0.05f, 0.1f);
                fillRect.anchorMax = new Vector2(0.95f, 0.7f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                //_gaugeFill = fill.AddComponent<Image>();
                //_gaugeFill.color = Constants.UI.Colors.GAUGE_BACKGROUND;
                //_gaugeFill.type = Image.Type.Filled;
                //_gaugeFill.fillMethod = Image.FillMethod.Horizontal;
                //_gaugeFill.fillOrigin = 0; // Start from left
                //_gaugeFill.fillAmount = 1.0f; // Start with full gauge

                _gaugeSlider = fill.AddComponent<Slider>();
                _gaugeSlider.direction = Slider.Direction.RightToLeft;
                _gaugeSlider.minValue = 0f;
                _gaugeSlider.maxValue = 100f;

                GameObject sliderFill = new GameObject("SliderFill");
                sliderFill.transform.SetParent(fill.transform, false);
                RectTransform sliderFillRect = sliderFill.AddComponent<RectTransform>();
                _gaugeSliderImage = sliderFill.AddComponent<Image>();
                _gaugeSliderImage.color = Constants.UI.Colors.FUEL_NORMAL;
                sliderFillRect.anchorMin = Vector2.zero;
                sliderFillRect.anchorMax = Vector2.one;
                sliderFillRect.offsetMin = Vector2.zero;
                sliderFillRect.offsetMax = Vector2.zero;
                _gaugeSlider.fillRect = sliderFillRect;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating gauge fill", ex);
            }
        }

        /// <summary>
        /// Create the fuel text display
        /// </summary>
        private void CreateFuelText()
        {
            try
            {
                GameObject textObj = new GameObject("FuelText");
                textObj.transform.SetParent(_gaugeContainer!.transform, false);
                _fuelTextParentGroup = textObj.AddComponent<CanvasGroup>();
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0f, 0.8f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                textRect.anchoredPosition = new Vector2(0f, -50f);
                textRect.sizeDelta = new Vector2(400f, 20f);

                _fuelText = textObj.AddComponent<TextMeshProUGUI>();
                _fuelText.alignment = TextAlignmentOptions.Left;
                _fuelText.fontSize = 12;
                _fuelText.fontStyle = FontStyles.Bold;
                _fuelText.color = Color.white;
                _fuelText.text = "50.0L (100%)"; // Initial text

                // Add shadow for better readability
                Shadow shadow = textObj.AddComponent<Shadow>();
                shadow.effectColor = Color.black;
                shadow.effectDistance = new Vector2(1, -1);

                //GameObject textObj = new GameObject("FuelText");
                //textObj.transform.SetParent(_gaugeContainer!.transform, false);

                //RectTransform textRect = textObj.AddComponent<RectTransform>();
                //textRect.anchorMin = new Vector2(0f, 0.8f);
                //textRect.anchorMax = new Vector2(1f, 1f);
                //textRect.offsetMin = Vector2.zero;
                //textRect.offsetMax = Vector2.zero;

                //_fuelText = textObj.AddComponent<TextMeshProUGUI>();
                ////_fuelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                //_fuelText.fontSize = 12;
                //_fuelText.fontStyle = FontStyles.Bold;
                //_fuelText.color = Color.white;
                //_fuelText.alignment = TextAlignmentOptions.Midline;
                //_fuelText.text = "50.0L (100%)";

                //// Add shadow for better readability
                //Shadow shadow = textObj.AddComponent<Shadow>();
                //shadow.effectColor = Color.black;
                //shadow.effectDistance = new Vector2(1, -1);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating fuel text", ex);
            }
        }

        /// <summary>
        /// Create the warning icon
        /// </summary>
        private void CreateWarningIcon()
        {
            try
            {
                GameObject warningObj = new GameObject("WarningIcon");
                warningObj.transform.SetParent(_gaugeContainer!.transform, false);

                RectTransform warningRect = warningObj.AddComponent<RectTransform>();
                warningRect.anchorMin = new Vector2(0.85f, 0.15f);
                warningRect.anchorMax = new Vector2(0.95f, 0.65f);
                warningRect.offsetMin = Vector2.zero;
                warningRect.offsetMax = Vector2.zero;

                _warningIcon = warningObj.AddComponent<Image>();
                _warningIcon.color = Constants.UI.Colors.FUEL_CRITICAL;

                // Create a simple warning triangle (using a filled image for now)
                // In a full implementation, you'd load a proper warning icon texture
                _warningIcon.type = Image.Type.Simple;

                // Initially hide the warning icon
                warningObj.SetActive(false);
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error creating warning icon", ex);
            }
        } 

        /// <summary>
        /// Setup event listeners for fuel system events
        /// </summary>
        private void SetupEventListeners()
        {
            try
            {
                if (_fuelSystem != null)
                {
                    ModLogger.UIDebug($"FuelGaugeUI: Setting up event listeners for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
                    
                    _fuelSystem.OnFuelLevelChanged.AddListener(OnFuelLevelChanged);
                    _fuelSystem.OnFuelPercentageChanged.AddListener(OnFuelPercentageChanged);
                    _fuelSystem.OnLowFuelWarning.AddListener(OnLowFuelWarning);
                    _fuelSystem.OnCriticalFuelWarning.AddListener(OnCriticalFuelWarning);
                    _fuelSystem.OnFuelEmpty.AddListener(OnFuelEmpty);
                    
                    ModLogger.UIDebug($"FuelGaugeUI: Event listeners set up successfully for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
                }
                else
                {
                    ModLogger.Warning("FuelGaugeUI: Cannot setup event listeners - fuel system is null");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error setting up event listeners", ex);
            }
        }

        /// <summary>
        /// Update the gauge display
        /// </summary>
        public void Update()
        {
            try
            {
                if (!_isVisible || _gaugeContainer == null) return;

                // Throttle updates to improve performance
                if (Time.time - _lastUpdateTime < Constants.UI.GAUGE_UPDATE_INTERVAL) return;

                UpdateDisplay();
                _lastUpdateTime = Time.time;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating fuel gauge display", ex);
            }
        }

        /// <summary>
        /// Update the visual display of the gauge
        /// </summary>
        private void UpdateDisplay()
        {
            try
            {
                if (_fuelSystem == null) 
                {
                    ModLogger.UIDebug("FuelGaugeUI: UpdateDisplay called but fuel system is null");
                    return;
                }

                float fuelLevel = _fuelSystem.CurrentFuelLevel;
                float maxCapacity = _fuelSystem.MaxFuelCapacity;
                float percentage = _fuelSystem.FuelPercentage;

                // Update fill amount
                //if (_gaugeFill != null)
                //{
                //    float fillAmount = percentage / 100f;
                //    _gaugeFill.fillAmount = fillAmount;
                //    _gaugeSlider.value = percentage; // Update slider value
                //}
                if (_gaugeSlider != null && _gaugeSliderImage != null)
                {
                    // Update slider fill amount
                    _gaugeSlider.value = percentage;
                }
                ModLogger.UIDebug($"FuelGaugeUI: Percentage shown: {percentage}");

                // Update text
                if (_fuelText != null)
                {
                    string newText = $"{fuelLevel:F1}L ({percentage:F1}%)";
                    _fuelText.text = newText;
                }

                // Update colors based on fuel level
                UpdateGaugeColors();

                // Update warning icon
                UpdateWarningIcon();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating gauge display", ex);
            }
        }

        /// <summary>
        /// Update gauge colors based on fuel level
        /// </summary>
        private void UpdateGaugeColors()
        {
            try
            {
                if (_fuelText == null) return;

                Color fillColor;
                Color textColor = Color.white;

                if (_fuelSystem.IsCriticalFuel)
                {
                    fillColor = Constants.UI.Colors.FUEL_CRITICAL;
                    textColor = Constants.UI.Colors.FUEL_CRITICAL;
                }
                else if (_fuelSystem.IsLowFuel)
                {
                    fillColor = Constants.UI.Colors.FUEL_LOW;
                    textColor = Constants.UI.Colors.FUEL_LOW;
                }
                else
                {
                    fillColor = Constants.UI.Colors.FUEL_NORMAL;
                }

                _gaugeSliderImage.color = Color.Lerp(_gaugeSliderImage.color, fillColor, Time.deltaTime * 5f);
                _fuelText.color = textColor;
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating gauge colors", ex);
            }
        }

        /// <summary>
        /// Update warning icon visibility
        /// </summary>
        private void UpdateWarningIcon()
        {
            try
            {
                if (_warningIcon == null) return;

                bool showWarning = _fuelSystem.IsLowFuel || _fuelSystem.IsCriticalFuel || _fuelSystem.IsOutOfFuel;
                _warningIcon.gameObject.SetActive(showWarning);

                if (showWarning)
                {
                    // Animate warning icon for critical situations
                    if (_fuelSystem.IsCriticalFuel || _fuelSystem.IsOutOfFuel)
                    {
                        float alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f); // Pulsing effect
                        Color color = _warningIcon.color;
                        color.a = alpha;
                        _warningIcon.color = color;
                    }
                    else
                    {
                        // Steady warning for low fuel
                        Color color = _warningIcon.color;
                        color.a = 1f;
                        _warningIcon.color = color;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error updating warning icon", ex);
            }
        }

        /// <summary>
        /// Show the fuel gauge
        /// </summary>
        public void Show()
        {
            try
            {
                if (_gaugeContainer != null)
                {
                    _gaugeContainer.SetActive(true);
                    if (_gaugeSliderImage != null)
                    {
                        Color newColor = _gaugeSliderImage.color;
                        newColor.a = 1f; // Ensure full opacity
                        _gaugeSliderImage.color = newColor; // Reset color
                    }
                    _isVisible = true;
                    UpdateDisplay();
                    ModLogger.UIDebug($"FuelGaugeUI: Shown for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error showing fuel gauge", ex);
            }
        }

        /// <summary>
        /// Hide the fuel gauge
        /// </summary>
        public void Hide()
        {
            try
            {
                if (_gaugeContainer != null)
                {
                    if ( _gaugeSliderImage != null )
                    {
                        Color newColor = _gaugeSliderImage.color;
                        newColor.a = 0f; // Fade out
                        _gaugeSliderImage.color = newColor; // Apply fade
                    }
                    _gaugeContainer.SetActive(false);
                    _isVisible = false;
                    ModLogger.UIDebug($"FuelGaugeUI: Hidden for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error hiding fuel gauge", ex);
            }
        }

        // Event handlers
        private void OnFuelLevelChanged(float fuelLevel)
        {
            // Immediately update display when fuel level changes
            if (_isVisible)
            {
                UpdateDisplay();
            }
        }

        private void OnFuelPercentageChanged(float percentage)
        {
            // Immediately update display when fuel percentage changes
            if (_isVisible)
            {
                UpdateDisplay();
            }
        }

        private void OnLowFuelWarning(bool isActive)
        {
            ModLogger.UIDebug($"FuelGaugeUI: Low fuel warning {(isActive ? "activated" : "deactivated")}");
        }

        private void OnCriticalFuelWarning(bool isActive)
        {
            ModLogger.UIDebug($"FuelGaugeUI: Critical fuel warning {(isActive ? "activated" : "deactivated")}");
        }

        private void OnFuelEmpty(bool isEmpty)
        {
            ModLogger.UIDebug($"FuelGaugeUI: Fuel empty state {(isEmpty ? "activated" : "deactivated")}");
        }

        /// <summary>
        /// Dispose of the fuel gauge UI
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Remove event listeners
                if (_fuelSystem != null)
                {
                    _fuelSystem.OnFuelLevelChanged.RemoveListener(OnFuelLevelChanged);
                    _fuelSystem.OnFuelPercentageChanged.RemoveListener(OnFuelPercentageChanged);
                    _fuelSystem.OnLowFuelWarning.RemoveListener(OnLowFuelWarning);
                    _fuelSystem.OnCriticalFuelWarning.RemoveListener(OnCriticalFuelWarning);
                    _fuelSystem.OnFuelEmpty.RemoveListener(OnFuelEmpty);
                }

                // Destroy UI objects
                if (_gaugeContainer != null)
                {
                    UnityEngine.Object.Destroy(_gaugeContainer);
                    _gaugeContainer = null;
                }

                _gaugeRect = null;
                _gaugeBackground = null;
                _gaugeSliderImage = null;
                _fuelText = null;
                _warningIcon = null;
                _isVisible = false;

                ModLogger.UIDebug($"FuelGaugeUI: Disposed for vehicle {_fuelSystem.VehicleGUID.Substring(0, 8)}...");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error disposing FuelGaugeUI", ex);
            }
        }
    }
}
