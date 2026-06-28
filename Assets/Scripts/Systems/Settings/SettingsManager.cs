using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public sealed class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // ==================================================
    // PLAYERPREFS KEYS
    // ==================================================

    private const string SensitivityKey =
        "Settings_Sensitivity";

    private const string FieldOfViewKey =
        "Settings_FieldOfView";

    private const string MasterVolumeKey =
        "Settings_MasterVolume";

    private const string MusicVolumeKey =
        "Settings_MusicVolume";

    private const string QualityLevelKey =
        "Settings_QualityLevel";

    private const string FullScreenModeKey =
        "Settings_FullScreenMode";

    private const string ResolutionWidthKey =
        "Settings_ResolutionWidth";

    private const string ResolutionHeightKey =
        "Settings_ResolutionHeight";

    private const string VSyncKey =
        "Settings_VSync";

    private const string FpsLimitKey =
        "Settings_FpsLimit";

    // ==================================================
    // AUDIO
    // ==================================================

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Tooltip(
        "Muss exakt dem exponierten Master-Parameter entsprechen."
    )]
    [SerializeField]
    private string masterVolumeParameter =
        "MasterVolumeDb";

    [Tooltip(
        "Muss exakt dem exponierten Music-Parameter entsprechen."
    )]
    [SerializeField]
    private string musicVolumeParameter =
        "MusicVolumeDb";

    // ==================================================
    // DEFAULTS
    // ==================================================

    [Header("Default Gameplay Settings")]
    [SerializeField, Range(0.1f, 10f)]
    private float defaultSensitivity = 2f;

    [SerializeField, Range(60f, 100f)]
    private float defaultFieldOfView = 60f;

    [Header("Default Audio Settings")]
    [SerializeField, Range(0f, 1f)]
    private float defaultMasterVolume = 1f;

    [SerializeField, Range(0f, 1f)]
    private float defaultMusicVolume = 0.8f;

    [Header("Default Graphics Settings")]
    [Tooltip(
        "-1 verwendet das beim Spielstart aktive Quality-Level."
    )]
    [SerializeField]
    private int defaultQualityLevel = -1;

    [SerializeField]
    private UnityEngine.FullScreenMode defaultFullScreenMode =
        UnityEngine.FullScreenMode.FullScreenWindow;

    [SerializeField]
    private bool defaultVSync = true;

    [Tooltip("-1 bedeutet Unlimited.")]
    [SerializeField]
    private int defaultFpsLimit = -1;

    // ==================================================
    // CURRENT VALUES
    // ==================================================

    public float Sensitivity { get; private set; }

    public float FieldOfView { get; private set; }

    public float MasterVolume { get; private set; }

    public float MusicVolume { get; private set; }

    public int QualityLevel { get; private set; }

    public UnityEngine.FullScreenMode FullScreenMode
    {
        get;
        private set;
    }

    public int ResolutionWidth { get; private set; }

    public int ResolutionHeight { get; private set; }

    public bool VSyncEnabled { get; private set; }

    public int FpsLimit { get; private set; }

    // ==================================================
    // EVENTS
    // ==================================================

    public event Action<float> SensitivityChanged;

    public event Action<float> FieldOfViewChanged;

    public event Action<float> MasterVolumeChanged;

    public event Action<float> MusicVolumeChanged;

    public event Action<int> QualityLevelChanged;

    public event Action<UnityEngine.FullScreenMode>
        FullScreenModeChanged;

    public event Action<int, int> ResolutionChanged;

    public event Action<bool> VSyncChanged;

    public event Action<int> FpsLimitChanged;

    private int startupQualityLevel;

    private bool missingMixerWarningShown;
    private bool missingMasterParameterWarningShown;
    private bool missingMusicParameterWarningShown;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        startupQualityLevel =
            QualitySettings.GetQualityLevel();

        LoadSettings();
        ApplyNonAudioSettings();
    }

    private void Start()
    {
        ApplyAudioSettings();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        Instance = null;
    }

    private void OnApplicationQuit()
    {
        SaveSettings();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveSettings();
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode loadMode
    )
    {
        StartCoroutine(
            ReapplyAfterSceneLoad()
        );
    }

    private IEnumerator ReapplyAfterSceneLoad()
    {
        /*
         * Einen Frame warten, damit die neuen Objekte
         * der Szene bereits initialisiert sind.
         */
        yield return null;

        ApplyAllSettings();
        BroadcastAllSettings();
    }

    // ==================================================
    // LOAD
    // ==================================================

    private void LoadSettings()
    {
        Sensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                SensitivityKey,
                defaultSensitivity
            ),
            0.1f,
            10f
        );

        FieldOfView = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                FieldOfViewKey,
                defaultFieldOfView
            ),
            60f,
            100f
        );

        MasterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MasterVolumeKey,
                defaultMasterVolume
            )
        );

        MusicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MusicVolumeKey,
                defaultMusicVolume
            )
        );

        QualityLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(
                QualityLevelKey,
                GetDefaultQualityLevel()
            ),
            0,
            Mathf.Max(
                0,
                QualitySettings.names.Length - 1
            )
        );

        int savedFullscreenMode =
            PlayerPrefs.GetInt(
                FullScreenModeKey,
                (int)defaultFullScreenMode
            );

        FullScreenMode =
            IsValidFullScreenMode(savedFullscreenMode)
                ? (UnityEngine.FullScreenMode)savedFullscreenMode
                : defaultFullScreenMode;

        ResolutionWidth = Mathf.Max(
            640,
            PlayerPrefs.GetInt(
                ResolutionWidthKey,
                Screen.width
            )
        );

        ResolutionHeight = Mathf.Max(
            360,
            PlayerPrefs.GetInt(
                ResolutionHeightKey,
                Screen.height
            )
        );

        VSyncEnabled =
            PlayerPrefs.GetInt(
                VSyncKey,
                defaultVSync ? 1 : 0
            ) == 1;

        FpsLimit = NormalizeFpsLimit(
            PlayerPrefs.GetInt(
                FpsLimitKey,
                defaultFpsLimit
            )
        );
    }

    // ==================================================
    // SAVE
    // ==================================================

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(
            SensitivityKey,
            Sensitivity
        );

        PlayerPrefs.SetFloat(
            FieldOfViewKey,
            FieldOfView
        );

        PlayerPrefs.SetFloat(
            MasterVolumeKey,
            MasterVolume
        );

        PlayerPrefs.SetFloat(
            MusicVolumeKey,
            MusicVolume
        );

        PlayerPrefs.SetInt(
            QualityLevelKey,
            QualityLevel
        );

        PlayerPrefs.SetInt(
            FullScreenModeKey,
            (int)FullScreenMode
        );

        PlayerPrefs.SetInt(
            ResolutionWidthKey,
            ResolutionWidth
        );

        PlayerPrefs.SetInt(
            ResolutionHeightKey,
            ResolutionHeight
        );

        PlayerPrefs.SetInt(
            VSyncKey,
            VSyncEnabled ? 1 : 0
        );

        PlayerPrefs.SetInt(
            FpsLimitKey,
            FpsLimit
        );

        PlayerPrefs.Save();
    }

    // ==================================================
    // APPLY
    // ==================================================

    public void ReapplyAllSettings()
    {
        ApplyAllSettings();
        BroadcastAllSettings();
    }

    private void ApplyAllSettings()
    {
        ApplyAudioSettings();
        ApplyNonAudioSettings();
    }

    private void ApplyNonAudioSettings()
    {
        ApplyQualitySettings();
        ApplyResolutionSettings();
        ApplyFrameSettings();
    }

    private void ApplyAudioSettings()
    {
        if (audioMixer == null)
        {
            AudioListener.volume =
                MasterVolume;

            if (!missingMixerWarningShown)
            {
                Debug.LogWarning(
                    "Beim SettingsManager fehlt der AudioMixer. " +
                    "Master Volume wird über AudioListener geregelt. " +
                    "Music Volume kann ohne Mixer nicht separat wirken.",
                    gameObject
                );

                missingMixerWarningShown = true;
            }

            return;
        }

        AudioListener.volume = 1f;

        ApplyMixerVolume(
            masterVolumeParameter,
            MasterVolume,
            ref missingMasterParameterWarningShown
        );

        ApplyMixerVolume(
            musicVolumeParameter,
            MusicVolume,
            ref missingMusicParameterWarningShown
        );
    }

    private void ApplyMixerVolume(
        string parameterName,
        float linearVolume,
        ref bool warningShown
    )
    {
        if (audioMixer == null)
            return;

        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        float decibelValue =
            ConvertLinearVolumeToDecibels(
                linearVolume
            );

        bool parameterFound =
            audioMixer.SetFloat(
                parameterName,
                decibelValue
            );

        if (!parameterFound && !warningShown)
        {
            Debug.LogWarning(
                "AudioMixer-Parameter '" +
                parameterName +
                "' wurde nicht gefunden.",
                gameObject
            );

            warningShown = true;
        }
    }

    private float ConvertLinearVolumeToDecibels(
        float linearVolume
    )
    {
        if (linearVolume <= 0.0001f)
            return -80f;

        return Mathf.Log10(
            Mathf.Clamp01(linearVolume)
        ) * 20f;
    }

    private void ApplyQualitySettings()
    {
        if (QualitySettings.names.Length == 0)
            return;

        QualitySettings.SetQualityLevel(
            QualityLevel,
            true
        );
    }

    private void ApplyResolutionSettings()
    {
        Screen.SetResolution(
            ResolutionWidth,
            ResolutionHeight,
            FullScreenMode
        );
    }

    private void ApplyFrameSettings()
    {
        QualitySettings.vSyncCount =
            VSyncEnabled ? 1 : 0;

        Application.targetFrameRate =
            VSyncEnabled
                ? -1
                : FpsLimit;
    }

    // ==================================================
    // GAMEPLAY
    // ==================================================

    public void SetSensitivity(float value)
    {
        float newValue = Mathf.Clamp(
            value,
            0.1f,
            10f
        );

        if (Mathf.Approximately(
                Sensitivity,
                newValue))
        {
            return;
        }

        Sensitivity = newValue;

        SaveSettings();

        SensitivityChanged?.Invoke(
            Sensitivity
        );
    }

    public void SetFieldOfView(float value)
    {
        float newValue = Mathf.Clamp(
            value,
            60f,
            100f
        );

        if (Mathf.Approximately(
                FieldOfView,
                newValue))
        {
            return;
        }

        FieldOfView = newValue;

        SaveSettings();

        FieldOfViewChanged?.Invoke(
            FieldOfView
        );
    }

    // ==================================================
    // AUDIO
    // ==================================================

    public void SetMasterVolume(float value)
    {
        float newValue =
            Mathf.Clamp01(value);

        if (Mathf.Approximately(
                MasterVolume,
                newValue))
        {
            return;
        }

        MasterVolume = newValue;

        ApplyAudioSettings();
        SaveSettings();

        MasterVolumeChanged?.Invoke(
            MasterVolume
        );
    }

    public void SetMusicVolume(float value)
    {
        float newValue =
            Mathf.Clamp01(value);

        if (Mathf.Approximately(
                MusicVolume,
                newValue))
        {
            return;
        }

        MusicVolume = newValue;

        ApplyAudioSettings();
        SaveSettings();

        MusicVolumeChanged?.Invoke(
            MusicVolume
        );
    }

    // ==================================================
    // GRAPHICS
    // ==================================================

    public void SetQualityLevel(int qualityIndex)
    {
        if (QualitySettings.names.Length == 0)
            return;

        int newQualityLevel = Mathf.Clamp(
            qualityIndex,
            0,
            QualitySettings.names.Length - 1
        );

        if (QualityLevel == newQualityLevel)
            return;

        QualityLevel = newQualityLevel;

        ApplyQualitySettings();
        SaveSettings();

        QualityLevelChanged?.Invoke(
            QualityLevel
        );
    }

    public void SetFullscreen(bool enabled)
    {
        SetFullScreenMode(
            enabled
                ? UnityEngine.FullScreenMode.FullScreenWindow
                : UnityEngine.FullScreenMode.Windowed
        );
    }

    public void SetFullScreenMode(
        UnityEngine.FullScreenMode mode
    )
    {
        if (FullScreenMode == mode)
            return;

        FullScreenMode = mode;

        ApplyResolutionSettings();
        SaveSettings();

        FullScreenModeChanged?.Invoke(
            FullScreenMode
        );
    }

    public void SetResolution(
        int width,
        int height
    )
    {
        int validWidth =
            Mathf.Max(640, width);

        int validHeight =
            Mathf.Max(360, height);

        if (ResolutionWidth == validWidth &&
            ResolutionHeight == validHeight)
        {
            return;
        }

        ResolutionWidth = validWidth;
        ResolutionHeight = validHeight;

        ApplyResolutionSettings();
        SaveSettings();

        ResolutionChanged?.Invoke(
            ResolutionWidth,
            ResolutionHeight
        );
    }

    public void SetVSync(bool enabled)
    {
        if (VSyncEnabled == enabled)
            return;

        VSyncEnabled = enabled;

        ApplyFrameSettings();
        SaveSettings();

        VSyncChanged?.Invoke(
            VSyncEnabled
        );
    }

    public void SetFpsLimit(int fpsLimit)
    {
        int newValue =
            NormalizeFpsLimit(fpsLimit);

        if (FpsLimit == newValue)
            return;

        FpsLimit = newValue;

        ApplyFrameSettings();
        SaveSettings();

        FpsLimitChanged?.Invoke(
            FpsLimit
        );
    }

    // ==================================================
    // RESET
    // ==================================================

    public void ResetToDefaults()
    {
        Sensitivity = Mathf.Clamp(
            defaultSensitivity,
            0.1f,
            10f
        );

        FieldOfView = Mathf.Clamp(
            defaultFieldOfView,
            60f,
            100f
        );

        MasterVolume =
            Mathf.Clamp01(
                defaultMasterVolume
            );

        MusicVolume =
            Mathf.Clamp01(
                defaultMusicVolume
            );

        QualityLevel =
            GetDefaultQualityLevel();

        FullScreenMode =
            defaultFullScreenMode;

        ResolutionWidth =
            Screen.currentResolution.width;

        ResolutionHeight =
            Screen.currentResolution.height;

        VSyncEnabled =
            defaultVSync;

        FpsLimit =
            NormalizeFpsLimit(
                defaultFpsLimit
            );

        ApplyAllSettings();
        SaveSettings();
        BroadcastAllSettings();
    }

    // ==================================================
    // EVENTS
    // ==================================================

    private void BroadcastAllSettings()
    {
        SensitivityChanged?.Invoke(
            Sensitivity
        );

        FieldOfViewChanged?.Invoke(
            FieldOfView
        );

        MasterVolumeChanged?.Invoke(
            MasterVolume
        );

        MusicVolumeChanged?.Invoke(
            MusicVolume
        );

        QualityLevelChanged?.Invoke(
            QualityLevel
        );

        FullScreenModeChanged?.Invoke(
            FullScreenMode
        );

        ResolutionChanged?.Invoke(
            ResolutionWidth,
            ResolutionHeight
        );

        VSyncChanged?.Invoke(
            VSyncEnabled
        );

        FpsLimitChanged?.Invoke(
            FpsLimit
        );
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private int GetDefaultQualityLevel()
    {
        int qualityLevel =
            defaultQualityLevel >= 0
                ? defaultQualityLevel
                : startupQualityLevel;

        return Mathf.Clamp(
            qualityLevel,
            0,
            Mathf.Max(
                0,
                QualitySettings.names.Length - 1
            )
        );
    }

    private int NormalizeFpsLimit(int value)
    {
        if (value <= 0)
            return -1;

        return Mathf.Clamp(
            value,
            30,
            1000
        );
    }

    private bool IsValidFullScreenMode(
        int modeIndex
    )
    {
        return Enum.IsDefined(
            typeof(UnityEngine.FullScreenMode),
            modeIndex
        );
    }
}