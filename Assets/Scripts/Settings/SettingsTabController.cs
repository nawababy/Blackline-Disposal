using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class SettingsTabController : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private TMP_Text[] tabTexts;

    [Header("Content Pages")]
    [SerializeField] private GameObject[] contentPages;

    [Header("Colors")]
    [SerializeField] private Color normalTextColor = Color.white;

    [SerializeField]
    private Color selectedTextColor =
        new Color(0.56f, 0.75f, 0.45f, 1f);

    [Header("Default Tab")]
    [SerializeField, Min(0)] private int defaultTabIndex = 0;

    [Tooltip("Wenn aktiviert, öffnet sich das Menü immer beim Standard-Tab.")]
    [SerializeField] private bool resetTabOnOpen = true;

    private UnityAction[] tabActions;
    private int selectedTabIndex = -1;
    private bool buttonsRegistered;

    public int SelectedTabIndex => selectedTabIndex;

    private void Awake()
    {
        RegisterButtons();
    }

    private void OnEnable()
    {
        if (!buttonsRegistered)
            RegisterButtons();

        if (resetTabOnOpen)
            SelectTab(defaultTabIndex);
        else if (selectedTabIndex < 0)
            SelectTab(defaultTabIndex);
        else
            SelectTab(selectedTabIndex);
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    private void RegisterButtons()
    {
        if (buttonsRegistered || tabButtons == null)
            return;

        tabActions = new UnityAction[tabButtons.Length];

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
                continue;

            int tabIndex = i;

            tabActions[i] = () => SelectTab(tabIndex);
            tabButtons[i].onClick.AddListener(tabActions[i]);
        }

        buttonsRegistered = true;
    }

    private void UnregisterButtons()
    {
        if (!buttonsRegistered ||
            tabButtons == null ||
            tabActions == null)
        {
            return;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null ||
                tabActions[i] == null)
            {
                continue;
            }

            tabButtons[i].onClick.RemoveListener(tabActions[i]);
        }

        buttonsRegistered = false;
    }

    public void SelectTab(int tabIndex)
    {
        int buttonCount =
            tabButtons != null ? tabButtons.Length : 0;

        int textCount =
            tabTexts != null ? tabTexts.Length : 0;

        int pageCount =
            contentPages != null ? contentPages.Length : 0;

        int tabCount = Mathf.Min(
            buttonCount,
            Mathf.Min(textCount, pageCount)
        );

        if (tabIndex < 0 || tabIndex >= tabCount)
        {
            Debug.LogWarning(
                "Ungültiger Settings-Tab-Index: " + tabIndex,
                gameObject
            );

            return;
        }

        selectedTabIndex = tabIndex;

        for (int i = 0; i < tabCount; i++)
        {
            bool isSelected = i == selectedTabIndex;

            if (tabTexts[i] != null)
            {
                tabTexts[i].color = isSelected
                    ? selectedTextColor
                    : normalTextColor;
            }

            if (contentPages[i] != null)
                contentPages[i].SetActive(isSelected);
        }
    }

    public void ResetToDefaultTab()
    {
        SelectTab(defaultTabIndex);
    }
}