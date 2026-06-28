using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class HotbarUI : MonoBehaviour
{
    // ==================================================
    // HOTBAR SLOTS
    // ==================================================

    [Header("Slot Backgrounds 1–7")]
    [FormerlySerializedAs("itemSlots")]
    [SerializeField]
    private Image[] slotBackgrounds =
        new Image[7];

    [Header("Item Icons 1–7")]
    [SerializeField]
    private Image[] itemIcons =
        new Image[7];

    [Header("Highlight Settings")]
    [SerializeField]
    private Vector2 normalSize =
        new Vector2(64f, 64f);

    [SerializeField]
    private Vector2 selectedSize =
        new Vector2(75f, 75f);

    // ==================================================
    // DATA REFERENCES
    // ==================================================

    [Header("Data References")]
    [SerializeField]
    private PlayerInventory inventory;

    [Tooltip(
        "Kann leer bleiben. Das SharedBankAccount wird " +
        "automatisch über die Singleton-Instanz gefunden."
    )]
    [SerializeField]
    private SharedBankAccount sharedBankAccount;

    // ==================================================
    // MONEY TEXTS
    // ==================================================

    [Header("Money Values")]
    [SerializeField]
    private TMP_Text cashTextTMP;

    [SerializeField]
    private TMP_Text bankTextTMP;

    [Header("Money Gain Popups")]
    [SerializeField]
    private TMP_Text cashGainTextTMP;

    [SerializeField]
    private TMP_Text bankGainTextTMP;

    // ==================================================
    // POPUP ANIMATION
    // ==================================================

    [Header("Popup Animation")]
    [SerializeField, Min(0.1f)]
    private float popupDuration = 1.5f;

    [SerializeField, Min(0f)]
    private float popupMoveDistance = 25f;

    [SerializeField, Min(0.01f)]
    private float popAnimationDuration = 0.18f;

    [SerializeField]
    private float popupStartScale = 0.7f;

    [SerializeField]
    private float popupPeakScale = 1.15f;

    [Header("Legacy UI Text – Optional")]
    [SerializeField]
    private Text cashText;

    [SerializeField]
    private Text bankText;

    // ==================================================
    // INTERNAL STATE
    // ==================================================

    private CultureInfo currencyCulture;

    private int lastCash;
    private int lastBank;

    private bool cashInitialized;
    private bool bankInitialized;

    private bool allowMoneyGainPopups;
    private bool waitingForInitialSaveLoad;

    private bool inventorySubscribed;
    private bool bankSubscribed;
    private bool saveManagerSubscribed;

    private Coroutine referenceRoutine;
    private Coroutine cashPopupRoutine;
    private Coroutine bankPopupRoutine;

    private Vector2 cashPopupStartPosition;
    private Vector2 bankPopupStartPosition;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        currencyCulture =
            CultureInfo.GetCultureInfo("en-US");

        PreparePopup(
            cashGainTextTMP,
            out cashPopupStartPosition
        );

        PreparePopup(
            bankGainTextTMP,
            out bankPopupStartPosition
        );
    }

    private void OnEnable()
    {
        /*
         * Beim Betreten der GameScene beginnen PlayerInventory
         * und UI zunächst bei 0. Der SaveManager setzt danach
         * den gespeicherten Wert.
         *
         * Solange dieser Ladevorgang nicht abgeschlossen ist,
         * dürfen keine Einnahme-Popups erscheinen.
         */
        allowMoneyGainPopups = false;
        waitingForInitialSaveLoad =
            ShouldWaitForInitialSaveLoad();

        ResolveReferences();

        SubscribeToSaveManager();
        SubscribeToData();

        RefreshAll();
        ClearMoneyPopups();

        referenceRoutine =
            StartCoroutine(
                ResolveReferencesRoutine()
            );
    }

    private void OnDisable()
    {
        if (referenceRoutine != null)
        {
            StopCoroutine(referenceRoutine);
            referenceRoutine = null;
        }

        StopPopupCoroutines();
        UnsubscribeFromData();
        UnsubscribeFromSaveManager();
    }

    // ==================================================
    // INITIAL SAVE LOAD
    // ==================================================

    private bool ShouldWaitForInitialSaveLoad()
    {
        if (SaveManager.Instance == null ||
            GameManager.Instance == null)
        {
            return false;
        }

        int currentSlot =
            GameManager.Instance.CurrentSlot;

        if (currentSlot < 0)
            return false;

        return SaveManager.Instance.HasSave(
            currentSlot
        );
    }

    private void SubscribeToSaveManager()
    {
        if (SaveManager.Instance == null ||
            saveManagerSubscribed)
        {
            return;
        }

        SaveManager.Instance.GameLoaded +=
            OnGameLoaded;

        saveManagerSubscribed = true;
    }

    private void UnsubscribeFromSaveManager()
    {
        if (SaveManager.Instance != null &&
            saveManagerSubscribed)
        {
            SaveManager.Instance.GameLoaded -=
                OnGameLoaded;
        }

        saveManagerSubscribed = false;
    }

    private void OnGameLoaded(
        int slotIndex
    )
    {
        /*
         * CashChanged und BalanceChanged wurden beim Laden
         * bereits ausgelöst. Da allowMoneyGainPopups bisher
         * false war, wurden keine Popups gestartet.
         *
         * Jetzt übernehmen wir die geladenen Werte als neue
         * Ausgangswerte für zukünftige echte Einnahmen.
         */
        StopPopupCoroutines();
        ClearMoneyPopups();

        ResolveReferences();
        RefreshAll();

        waitingForInitialSaveLoad = false;
        allowMoneyGainPopups = true;
    }

    // ==================================================
    // REFERENCE RESOLUTION
    // ==================================================

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory =
                FindFirstObjectByType<PlayerInventory>();
        }

        if (sharedBankAccount == null)
        {
            sharedBankAccount =
                SharedBankAccount.Instance;
        }

        if (sharedBankAccount == null)
        {
            sharedBankAccount =
                FindFirstObjectByType<SharedBankAccount>();
        }
    }

    private IEnumerator ResolveReferencesRoutine()
    {
        const int maximumWaitFrames = 120;

        for (int frame = 0;
             frame < maximumWaitFrames;
             frame++)
        {
            bool inventoryWasMissing =
                inventory == null;

            bool bankWasMissing =
                sharedBankAccount == null;

            ResolveReferences();
            SubscribeToSaveManager();

            if (inventoryWasMissing &&
                inventory != null)
            {
                SubscribeToInventory();
                RefreshInventoryUI();
            }

            if (bankWasMissing &&
                sharedBankAccount != null)
            {
                SubscribeToBank();
                RefreshBankUI();
            }

            if (inventory != null &&
                sharedBankAccount != null)
            {
                break;
            }

            yield return null;
        }

        /*
         * Falls kein Spielstand geladen werden muss,
         * dürfen Popups nach der Initialisierung starten.
         */
        if (!waitingForInitialSaveLoad)
        {
            yield return null;

            RefreshAll();
            allowMoneyGainPopups = true;
            referenceRoutine = null;

            yield break;
        }

        /*
         * Sicherheits-Timeout:
         * Sollte das GameLoaded-Event wegen eines Fehlers
         * niemals eintreffen, bleibt das UI nicht dauerhaft
         * ohne Einnahme-Popups.
         */
        float timeout = 5f;

        while (waitingForInitialSaveLoad &&
               timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (waitingForInitialSaveLoad)
        {
            waitingForInitialSaveLoad = false;

            ResolveReferences();
            RefreshAll();

            ClearMoneyPopups();

            allowMoneyGainPopups = true;
        }

        referenceRoutine = null;
    }

    // ==================================================
    // SUBSCRIPTIONS
    // ==================================================

    private void SubscribeToData()
    {
        SubscribeToInventory();
        SubscribeToBank();
    }

    private void SubscribeToInventory()
    {
        if (inventory == null ||
            inventorySubscribed)
        {
            return;
        }

        inventory.CashChanged +=
            UpdateCash;

        inventory.SelectedSlotChanged +=
            UpdateSelectedSlot;

        inventorySubscribed = true;
    }

    private void SubscribeToBank()
    {
        if (sharedBankAccount == null ||
            bankSubscribed)
        {
            return;
        }

        sharedBankAccount.BalanceChanged +=
            UpdateBank;

        bankSubscribed = true;
    }

    private void UnsubscribeFromData()
    {
        if (inventory != null &&
            inventorySubscribed)
        {
            inventory.CashChanged -=
                UpdateCash;

            inventory.SelectedSlotChanged -=
                UpdateSelectedSlot;
        }

        if (sharedBankAccount != null &&
            bankSubscribed)
        {
            sharedBankAccount.BalanceChanged -=
                UpdateBank;
        }

        inventorySubscribed = false;
        bankSubscribed = false;
    }

    // ==================================================
    // COMPLETE REFRESH
    // ==================================================

    private void RefreshAll()
    {
        RefreshInventoryUI();
        RefreshBankUI();
    }

    private void RefreshInventoryUI()
    {
        if (inventory == null)
        {
            lastCash = 0;
            cashInitialized = false;

            SetCashText(0);
            RefreshItemIcons();

            return;
        }

        lastCash =
            inventory.cash;

        cashInitialized = true;

        SetCashText(
            inventory.cash
        );

        UpdateSelectedSlot(
            inventory.selectedSlot
        );

        RefreshItemIcons();
    }

    private void RefreshBankUI()
    {
        if (sharedBankAccount == null)
        {
            lastBank = 0;
            bankInitialized = false;

            SetBankText(0);

            return;
        }

        int currentBankBalance =
            sharedBankAccount.GetBalanceForSave();

        lastBank =
            currentBankBalance;

        bankInitialized = true;

        SetBankText(
            currentBankBalance
        );
    }

    // ==================================================
    // HOTBAR DISPLAY
    // ==================================================

    private void UpdateSelectedSlot(
        int selectedSlot
    )
    {
        if (slotBackgrounds == null)
            return;

        float safeNormalWidth =
            Mathf.Max(
                normalSize.x,
                0.01f
            );

        float safeNormalHeight =
            Mathf.Max(
                normalSize.y,
                0.01f
            );

        Vector3 selectedScale =
            new Vector3(
                selectedSize.x / safeNormalWidth,
                selectedSize.y / safeNormalHeight,
                1f
            );

        for (int i = 0;
             i < slotBackgrounds.Length;
             i++)
        {
            if (slotBackgrounds[i] == null)
                continue;

            slotBackgrounds[i]
                .transform.localScale =
                    i == selectedSlot
                        ? selectedScale
                        : Vector3.one;
        }
    }

    private void RefreshItemIcons()
    {
        if (itemIcons == null)
            return;

        for (int i = 0;
             i < itemIcons.Length;
             i++)
        {
            Image iconImage =
                itemIcons[i];

            if (iconImage == null)
                continue;

            HotbarItem item =
                inventory != null &&
                i < inventory.SlotCount
                    ? inventory.GetItem(i)
                    : null;

            bool hasValidIcon =
                item != null &&
                item.IsValid &&
                item.Icon != null;

            iconImage.sprite =
                hasValidIcon
                    ? item.Icon
                    : null;

            iconImage.enabled =
                hasValidIcon;
        }
    }

    // ==================================================
    // MONEY EVENTS
    // ==================================================

    private void UpdateCash(
        int newAmount
    )
    {
        /*
         * Während des Speicherladens wird der neue Wert
         * nur als Ausgangswert übernommen.
         */
        if (!allowMoneyGainPopups)
        {
            lastCash = newAmount;
            cashInitialized = true;

            SetCashText(newAmount);

            return;
        }

        if (cashInitialized)
        {
            int difference =
                newAmount - lastCash;

            if (difference > 0)
                ShowCashGain(difference);
        }

        lastCash = newAmount;
        cashInitialized = true;

        SetCashText(newAmount);
    }

    private void UpdateBank(
        int newAmount
    )
    {
        /*
         * Auch der geladene Bankwert ist keine neue Einnahme.
         */
        if (!allowMoneyGainPopups)
        {
            lastBank = newAmount;
            bankInitialized = true;

            SetBankText(newAmount);

            return;
        }

        if (bankInitialized)
        {
            int difference =
                newAmount - lastBank;

            if (difference > 0)
                ShowBankGain(difference);
        }

        lastBank = newAmount;
        bankInitialized = true;

        SetBankText(newAmount);
    }

    // ==================================================
    // MONEY TEXT
    // ==================================================

    private void SetCashText(
        int amount
    )
    {
        string formattedAmount =
            amount.ToString(
                "C0",
                currencyCulture
            );

        if (cashTextTMP != null)
            cashTextTMP.text = formattedAmount;

        if (cashText != null)
            cashText.text = formattedAmount;
    }

    private void SetBankText(
        int amount
    )
    {
        string formattedAmount =
            amount.ToString(
                "C0",
                currencyCulture
            );

        if (bankTextTMP != null)
            bankTextTMP.text = formattedAmount;

        if (bankText != null)
            bankText.text = formattedAmount;
    }

    // ==================================================
    // POPUP SETUP
    // ==================================================

    private void PreparePopup(
        TMP_Text popupText,
        out Vector2 startPosition
    )
    {
        startPosition =
            Vector2.zero;

        if (popupText == null)
            return;

        startPosition =
            popupText.rectTransform
                .anchoredPosition;

        popupText.text =
            string.Empty;

        popupText.rectTransform.localScale =
            Vector3.one;
    }

    private void StopPopupCoroutines()
    {
        if (cashPopupRoutine != null)
        {
            StopCoroutine(cashPopupRoutine);
            cashPopupRoutine = null;
        }

        if (bankPopupRoutine != null)
        {
            StopCoroutine(bankPopupRoutine);
            bankPopupRoutine = null;
        }
    }

    private void ClearMoneyPopups()
    {
        ResetPopup(
            cashGainTextTMP,
            cashPopupStartPosition
        );

        ResetPopup(
            bankGainTextTMP,
            bankPopupStartPosition
        );
    }

    private void ResetPopup(
        TMP_Text popupText,
        Vector2 startPosition
    )
    {
        if (popupText == null)
            return;

        popupText.text =
            string.Empty;

        popupText.rectTransform
            .anchoredPosition =
                startPosition;

        popupText.rectTransform
            .localScale =
                Vector3.one;

        Color popupColor =
            popupText.color;

        popupColor.a = 1f;

        popupText.color =
            popupColor;
    }

    // ==================================================
    // POPUPS
    // ==================================================

    private void ShowCashGain(
        int amount
    )
    {
        if (cashGainTextTMP == null)
            return;

        if (cashPopupRoutine != null)
            StopCoroutine(cashPopupRoutine);

        Color popupColor =
            cashTextTMP != null
                ? cashTextTMP.color
                : cashGainTextTMP.color;

        cashPopupRoutine =
            StartCoroutine(
                AnimateGainPopup(
                    cashGainTextTMP,
                    cashPopupStartPosition,
                    popupColor,
                    amount,
                    true
                )
            );
    }

    private void ShowBankGain(
        int amount
    )
    {
        if (bankGainTextTMP == null)
            return;

        if (bankPopupRoutine != null)
            StopCoroutine(bankPopupRoutine);

        Color popupColor =
            bankTextTMP != null
                ? bankTextTMP.color
                : bankGainTextTMP.color;

        bankPopupRoutine =
            StartCoroutine(
                AnimateGainPopup(
                    bankGainTextTMP,
                    bankPopupStartPosition,
                    popupColor,
                    amount,
                    false
                )
            );
    }

    private IEnumerator AnimateGainPopup(
        TMP_Text popupText,
        Vector2 startPosition,
        Color popupColor,
        int amount,
        bool isCashPopup
    )
    {
        RectTransform popupTransform =
            popupText.rectTransform;

        popupText.text =
            $"+${amount:N0}";

        popupTransform.anchoredPosition =
            startPosition;

        popupTransform.localScale =
            Vector3.one *
            popupStartScale;

        popupColor.a = 1f;
        popupText.color = popupColor;

        float timer = 0f;

        while (timer < popupDuration)
        {
            timer += Time.unscaledDeltaTime;

            float totalProgress =
                Mathf.Clamp01(
                    timer / popupDuration
                );

            float moveProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    totalProgress
                );

            popupTransform.anchoredPosition =
                startPosition +
                Vector2.up *
                popupMoveDistance *
                moveProgress;

            if (timer < popAnimationDuration)
            {
                float popProgress =
                    Mathf.Clamp01(
                        timer /
                        popAnimationDuration
                    );

                float currentScale;

                if (popProgress < 0.5f)
                {
                    currentScale =
                        Mathf.Lerp(
                            popupStartScale,
                            popupPeakScale,
                            popProgress * 2f
                        );
                }
                else
                {
                    currentScale =
                        Mathf.Lerp(
                            popupPeakScale,
                            1f,
                            (popProgress - 0.5f) * 2f
                        );
                }

                popupTransform.localScale =
                    Vector3.one *
                    currentScale;
            }
            else
            {
                popupTransform.localScale =
                    Vector3.one;
            }

            const float fadeStart = 0.3f;

            float alpha = 1f;

            if (totalProgress > fadeStart)
            {
                alpha =
                    1f -
                    Mathf.InverseLerp(
                        fadeStart,
                        1f,
                        totalProgress
                    );
            }

            Color currentColor =
                popupColor;

            currentColor.a =
                alpha;

            popupText.color =
                currentColor;

            yield return null;
        }

        ResetPopup(
            popupText,
            startPosition
        );

        if (isCashPopup)
            cashPopupRoutine = null;
        else
            bankPopupRoutine = null;
    }

    // ==================================================
    // EXTERNAL SETTERS
    // ==================================================

    public void SetInventory(
        PlayerInventory newInventory
    )
    {
        if (inventory == newInventory)
            return;

        UnsubscribeFromData();

        inventory =
            newInventory;

        ResolveReferences();

        cashInitialized = false;
        bankInitialized = false;
        allowMoneyGainPopups = false;

        SubscribeToData();
        RefreshAll();

        if (referenceRoutine != null)
            StopCoroutine(referenceRoutine);

        waitingForInitialSaveLoad =
            ShouldWaitForInitialSaveLoad();

        referenceRoutine =
            StartCoroutine(
                ResolveReferencesRoutine()
            );
    }

    public void SetSharedBankAccount(
        SharedBankAccount newBankAccount
    )
    {
        if (sharedBankAccount == newBankAccount)
            return;

        UnsubscribeFromData();

        sharedBankAccount =
            newBankAccount;

        ResolveReferences();

        bankInitialized = false;
        allowMoneyGainPopups = false;

        SubscribeToData();
        RefreshAll();

        if (referenceRoutine != null)
            StopCoroutine(referenceRoutine);

        waitingForInitialSaveLoad =
            ShouldWaitForInitialSaveLoad();

        referenceRoutine =
            StartCoroutine(
                ResolveReferencesRoutine()
            );
    }
}