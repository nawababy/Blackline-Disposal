using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsMenuUI : MonoBehaviour
{
    [Header("Optional Panel Root")]
    [Tooltip(
        "Das gesamte OptionsPanel. Kann leer bleiben, " +
        "wenn das Script direkt darauf liegt."
    )]
    [SerializeField] private GameObject panelRoot;

    // ==================================================
    // GAMEPLAY
    // ==================================================

    [Header("Gameplay")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText;

    [SerializeField] private Slider fieldOfViewSlider;
    [SerializeField] private TMP_Text fieldOfViewValueText;

    // ==================================================
    // AUDIO
    // ==================================================

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;

    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text musicVolumeValueText;

    // ==================================================
    // GRAPHICS
    // ==================================================

    [Header("Graphics")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vSyncToggle;

    [SerializeField] private TMP_Dropdown fpsLimitDropdown;

    [Tooltip("Wird später über Localization übersetzt.")]
    [SerializeField] private string unlimitedFpsText = "Unlimited";

    // ==================================================
    // FIXED FPS OPTIONS
    // ==================================================

    private readonly int[] fpsOptions =
    {
        -1,
        240,
        180,
        60
    };

    // ==================================================
    // FIXED RESOLUTIONS
    // ==================================================

    private readonly Vector2Int[] resolutionOptions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1920, 1080),
        new Vector2Int(3840, 2160)
    };

    private readonly List<Vector2Int> availableResolutions =
        new List<Vector2Int>();

    private bool listenersRegistered;

    private SettingsManager Manager =>
        SettingsManager.Instance;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
    }

    private void OnEnable()
    {
        if (!CheckSettingsManager())
            return;

        BuildDropdownOptions();
        NormalizeRestrictedSettings();
        RegisterListeners();
        RefreshUI();
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }

    // ==================================================
    // INITIALIZATION
    // ==================================================

    private bool CheckSettingsManager()
    {
        if (Manager != null)
            return true;

        Debug.LogWarning(
            "SettingsMenuUI konnte keinen SettingsManager finden.",
            gameObject
        );

        return false;
    }

    private void BuildDropdownOptions()
    {
        BuildQualityOptions();
        BuildResolutionOptions();
        BuildFpsOptions();
    }

    private void BuildQualityOptions()
    {
        if (qualityDropdown == null)
            return;

        qualityDropdown.ClearOptions();

        List<string> qualityNames =
            new List<string>(QualitySettings.names);

        qualityDropdown.AddOptions(qualityNames);
        qualityDropdown.RefreshShownValue();
    }

    private void BuildResolutionOptions()
    {
        if (resolutionDropdown == null)
            return;

        availableResolutions.Clear();
        resolutionDropdown.ClearOptions();

        List<string> resolutionNames =
            new List<string>();

        foreach (Vector2Int resolution in resolutionOptions)
        {
            availableResolutions.Add(resolution);

            resolutionNames.Add(
                resolution.x + " × " + resolution.y
            );
        }

        resolutionDropdown.AddOptions(resolutionNames);
        resolutionDropdown.RefreshShownValue();
    }

    private void BuildFpsOptions()
    {
        if (fpsLimitDropdown == null)
            return;

        fpsLimitDropdown.ClearOptions();

        List<string> fpsNames =
            new List<string>();

        foreach (int fpsOption in fpsOptions)
        {
            string optionName =
                fpsOption <= 0
                    ? unlimitedFpsText
                    : fpsOption + " FPS";

            fpsNames.Add(optionName);
        }

        fpsLimitDropdown.AddOptions(fpsNames);
        fpsLimitDropdown.RefreshShownValue();
    }

    // ==================================================
    // SETTINGS NORMALIZATION
    // ==================================================

    private void NormalizeRestrictedSettings()
    {
        if (Manager == null)
            return;

        /*
         * Alte FPS-Werte wie 120, 144 oder 165
         * werden auf Unlimited gesetzt.
         *
         * SetFpsLimit speichert selbstständig.
         */
        if (!ContainsFpsOption(Manager.FpsLimit))
        {
            Manager.SetFpsLimit(-1);
        }

        Vector2Int savedResolution =
            new Vector2Int(
                Manager.ResolutionWidth,
                Manager.ResolutionHeight
            );

        /*
         * Alte Auflösungen werden auf die nächstgelegene
         * feste Auflösung gesetzt.
         *
         * SetResolution speichert selbstständig.
         */
        if (!availableResolutions.Contains(savedResolution))
        {
            Vector2Int closestResolution =
                GetClosestAvailableResolution(
                    savedResolution
                );

            Manager.SetResolution(
                closestResolution.x,
                closestResolution.y
            );
        }
    }

    private bool ContainsFpsOption(int fpsValue)
    {
        foreach (int fpsOption in fpsOptions)
        {
            if (fpsOption == fpsValue)
                return true;
        }

        return false;
    }

    private Vector2Int GetClosestAvailableResolution(
        Vector2Int requestedResolution
    )
    {
        if (availableResolutions.Count == 0)
            return requestedResolution;

        Vector2Int closestResolution =
            availableResolutions[0];

        long closestDistance =
            long.MaxValue;

        foreach (Vector2Int resolution in availableResolutions)
        {
            long widthDifference =
                resolution.x - requestedResolution.x;

            long heightDifference =
                resolution.y - requestedResolution.y;

            long distance =
                widthDifference * widthDifference +
                heightDifference * heightDifference;

            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestResolution = resolution;
        }

        return closestResolution;
    }

    // ==================================================
    // UI REFRESH
    // ==================================================

    public void RefreshUI()
    {
        if (Manager == null)
            return;

        RefreshGameplayUI();
        RefreshAudioUI();
        RefreshGraphicsUI();
    }

    private void RefreshGameplayUI()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 10f;
            sensitivitySlider.wholeNumbers = false;

            sensitivitySlider.SetValueWithoutNotify(
                Manager.Sensitivity
            );
        }

        if (fieldOfViewSlider != null)
        {
            fieldOfViewSlider.minValue = 60f;
            fieldOfViewSlider.maxValue = 100f;
            fieldOfViewSlider.wholeNumbers = true;

            fieldOfViewSlider.SetValueWithoutNotify(
                Manager.FieldOfView
            );
        }

        UpdateSensitivityText(
            Manager.Sensitivity
        );

        UpdateFieldOfViewText(
            Manager.FieldOfView
        );
    }

    private void RefreshAudioUI()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.wholeNumbers = false;

            masterVolumeSlider.SetValueWithoutNotify(
                Manager.MasterVolume
            );
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.wholeNumbers = false;

            musicVolumeSlider.SetValueWithoutNotify(
                Manager.MusicVolume
            );
        }

        UpdateMasterVolumeText(
            Manager.MasterVolume
        );

        UpdateMusicVolumeText(
            Manager.MusicVolume
        );
    }

    private void RefreshGraphicsUI()
    {
        if (qualityDropdown != null &&
            qualityDropdown.options.Count > 0)
        {
            int qualityIndex =
                Mathf.Clamp(
                    Manager.QualityLevel,
                    0,
                    qualityDropdown.options.Count - 1
                );

            qualityDropdown.SetValueWithoutNotify(
                qualityIndex
            );

            qualityDropdown.RefreshShownValue();
        }

        RefreshResolutionDropdown();

        if (fullscreenToggle != null)
        {
            bool isFullscreen =
                Manager.FullScreenMode !=
                FullScreenMode.Windowed;

            fullscreenToggle.SetIsOnWithoutNotify(
                isFullscreen
            );
        }

        if (vSyncToggle != null)
        {
            vSyncToggle.SetIsOnWithoutNotify(
                Manager.VSyncEnabled
            );
        }

        RefreshFpsDropdown();

        if (fpsLimitDropdown != null)
        {
            fpsLimitDropdown.interactable =
                !Manager.VSyncEnabled;
        }
    }

    private void RefreshResolutionDropdown()
    {
        if (resolutionDropdown == null ||
            availableResolutions.Count == 0)
        {
            return;
        }

        Vector2Int savedResolution =
            new Vector2Int(
                Manager.ResolutionWidth,
                Manager.ResolutionHeight
            );

        int selectedIndex = 0;

        for (int i = 0;
             i < availableResolutions.Count;
             i++)
        {
            if (availableResolutions[i] != savedResolution)
                continue;

            selectedIndex = i;
            break;
        }

        resolutionDropdown.SetValueWithoutNotify(
            selectedIndex
        );

        resolutionDropdown.RefreshShownValue();
    }

    private void RefreshFpsDropdown()
    {
        if (fpsLimitDropdown == null ||
            fpsOptions.Length == 0)
        {
            return;
        }

        int selectedIndex = 0;

        for (int i = 0;
             i < fpsOptions.Length;
             i++)
        {
            if (fpsOptions[i] != Manager.FpsLimit)
                continue;

            selectedIndex = i;
            break;
        }

        fpsLimitDropdown.SetValueWithoutNotify(
            selectedIndex
        );

        fpsLimitDropdown.RefreshShownValue();
    }

    // ==================================================
    // LISTENERS
    // ==================================================

    private void RegisterListeners()
    {
        RemoveListeners();

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(
                OnSensitivityChanged
            );
        }

        if (fieldOfViewSlider != null)
        {
            fieldOfViewSlider.onValueChanged.AddListener(
                OnFieldOfViewChanged
            );
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(
                OnMasterVolumeChanged
            );
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(
                OnMusicVolumeChanged
            );
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged.AddListener(
                OnQualityChanged
            );
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(
                OnResolutionChanged
            );
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.AddListener(
                OnFullscreenChanged
            );
        }

        if (vSyncToggle != null)
        {
            vSyncToggle.onValueChanged.AddListener(
                OnVSyncChanged
            );
        }

        if (fpsLimitDropdown != null)
        {
            fpsLimitDropdown.onValueChanged.AddListener(
                OnFpsLimitChanged
            );
        }

        listenersRegistered = true;
    }

    private void RemoveListeners()
    {
        if (!listenersRegistered)
            return;

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveListener(
                OnSensitivityChanged
            );
        }

        if (fieldOfViewSlider != null)
        {
            fieldOfViewSlider.onValueChanged.RemoveListener(
                OnFieldOfViewChanged
            );
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(
                OnMasterVolumeChanged
            );
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(
                OnMusicVolumeChanged
            );
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged.RemoveListener(
                OnQualityChanged
            );
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(
                OnResolutionChanged
            );
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(
                OnFullscreenChanged
            );
        }

        if (vSyncToggle != null)
        {
            vSyncToggle.onValueChanged.RemoveListener(
                OnVSyncChanged
            );
        }

        if (fpsLimitDropdown != null)
        {
            fpsLimitDropdown.onValueChanged.RemoveListener(
                OnFpsLimitChanged
            );
        }

        listenersRegistered = false;
    }

    // ==================================================
    // GAMEPLAY CALLBACKS
    // ==================================================

    private void OnSensitivityChanged(float value)
    {
        if (Manager == null)
            return;

        Manager.SetSensitivity(value);
        UpdateSensitivityText(value);
    }

    private void OnFieldOfViewChanged(float value)
    {
        if (Manager == null)
            return;

        Manager.SetFieldOfView(value);
        UpdateFieldOfViewText(value);
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText == null)
            return;

        sensitivityValueText.text =
            value.ToString("0.0");
    }

    private void UpdateFieldOfViewText(float value)
    {
        if (fieldOfViewValueText == null)
            return;

        fieldOfViewValueText.text =
            Mathf.RoundToInt(value) + "°";
    }

    // ==================================================
    // AUDIO CALLBACKS
    // ==================================================

    private void OnMasterVolumeChanged(float value)
    {
        if (Manager == null)
            return;

        Manager.SetMasterVolume(value);
        UpdateMasterVolumeText(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (Manager == null)
            return;

        Manager.SetMusicVolume(value);
        UpdateMusicVolumeText(value);
    }

    private void UpdateMasterVolumeText(float value)
    {
        if (masterVolumeValueText == null)
            return;

        masterVolumeValueText.text =
            Mathf.RoundToInt(value * 100f) + "%";
    }

    private void UpdateMusicVolumeText(float value)
    {
        if (musicVolumeValueText == null)
            return;

        musicVolumeValueText.text =
            Mathf.RoundToInt(value * 100f) + "%";
    }

    // ==================================================
    // GRAPHICS CALLBACKS
    // ==================================================

    private void OnQualityChanged(int qualityIndex)
    {
        if (Manager == null)
            return;

        Manager.SetQualityLevel(qualityIndex);
    }

    private void OnResolutionChanged(int resolutionIndex)
    {
        if (Manager == null)
            return;

        if (resolutionIndex < 0 ||
            resolutionIndex >= availableResolutions.Count)
        {
            return;
        }

        Vector2Int resolution =
            availableResolutions[resolutionIndex];

        Manager.SetResolution(
            resolution.x,
            resolution.y
        );
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        if (Manager == null)
            return;

        Manager.SetFullscreen(isFullscreen);
    }

    private void OnVSyncChanged(bool enabled)
    {
        if (Manager == null)
            return;

        Manager.SetVSync(enabled);

        if (fpsLimitDropdown != null)
        {
            fpsLimitDropdown.interactable =
                !enabled;
        }
    }

    private void OnFpsLimitChanged(int optionIndex)
    {
        if (Manager == null)
            return;

        if (optionIndex < 0 ||
            optionIndex >= fpsOptions.Length)
        {
            return;
        }

        Manager.SetFpsLimit(
            fpsOptions[optionIndex]
        );
    }

    // ==================================================
    // BUTTONS
    // ==================================================

    public void OnResetButton()
    {
        if (Manager == null)
            return;

        Manager.ResetToDefaults();

        BuildDropdownOptions();
        NormalizeRestrictedSettings();
        RefreshUI();
    }

    public void OnCloseButton()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void OpenMenu()
    {
        if (!CheckSettingsManager())
            return;

        if (panelRoot == null)
            return;

        bool wasAlreadyOpen =
            panelRoot.activeSelf;

        panelRoot.SetActive(true);

        /*
         * Wenn das Menü bereits aktiv war,
         * wird OnEnable nicht erneut aufgerufen.
         */
        if (wasAlreadyOpen)
        {
            BuildDropdownOptions();
            NormalizeRestrictedSettings();
            RefreshUI();
        }
    }
}