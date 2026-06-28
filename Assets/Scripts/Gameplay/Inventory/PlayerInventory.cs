using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class PlayerInventory : MonoBehaviour
{
    private const int DefaultHotbarSize = 7;

    // ==================================================
    // HOTBAR
    // ==================================================

    [Header("Hotbar Slots")]
    [FormerlySerializedAs("hotbar")]
    [SerializeField]
    private HotbarItem[] hotbarItems =
        new HotbarItem[DefaultHotbarSize];

    [Header("Known Hotbar Item Definitions")]
    [Tooltip(
        "Katalog bekannter Hotbar-Items. Wird beim Laden genutzt, " +
        "um gespeicherte Item IDs wieder in vollständige Items aufzulösen."
    )]
    [SerializeField]
    private HotbarItem[] knownHotbarItems =
        new HotbarItem[0];

    private readonly Dictionary<string, HotbarItem> knownHotbarItemsById =
        new Dictionary<string, HotbarItem>(
            StringComparer.Ordinal
        );

    private bool knownHotbarItemLookupBuilt;

    [Header("Selected Slot")]
    [FormerlySerializedAs("selectedSlot")]
    [SerializeField, Min(0)]
    private int currentSelectedSlot;

    // ==================================================
    // PERSONAL CASH
    // ==================================================

    [Header("Personal Cash")]
    [FormerlySerializedAs("cash")]
    [SerializeField, Min(0)]
    private int currentCash;

    // ==================================================
    // PUBLIC VALUES
    // ==================================================

    public int cash => currentCash;

    public int selectedSlot
    {
        get => currentSelectedSlot;
        set => SetSelectedSlot(value);
    }

    public int SlotCount =>
        hotbarItems != null
            ? hotbarItems.Length
            : 0;

    public HotbarItem SelectedItem =>
        GetItem(currentSelectedSlot);

    /*
     * Gibt eine Kopie des Arrays zurück.
     * Andere Scripts können das interne Inventar dadurch
     * nicht unbemerkt verändern.
     */
    public HotbarItem[] hotbar
    {
        get
        {
            if (hotbarItems == null)
                return Array.Empty<HotbarItem>();

            return (HotbarItem[])hotbarItems.Clone();
        }
    }

    // ==================================================
    // EVENTS
    // ==================================================

    public event Action<int> CashChanged;
    public event Action<int> SelectedSlotChanged;

    /*
     * Übergibt:
     * 1. den geänderten Slot
     * 2. das neue Item in diesem Slot
     */
    public event Action<int, HotbarItem> HotbarItemChanged;

    /*
     * Wird ausgelöst, wenn sich irgendein Slot
     * der Hotbar verändert hat.
     */
    public event Action HotbarChanged;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        EnsureValidHotbar();
        BuildKnownHotbarItemLookup();
        ClampSelectedSlot();

        currentCash =
            Mathf.Max(0, currentCash);
    }

    private void OnValidate()
    {
        EnsureValidHotbar();
        BuildKnownHotbarItemLookup();

        currentCash =
            Mathf.Max(0, currentCash);

        ClampSelectedSlot();
    }

    // ==================================================
    // HOTBAR VALIDATION
    // ==================================================

    private void EnsureValidHotbar()
    {
        if (hotbarItems == null ||
            hotbarItems.Length == 0)
        {
            hotbarItems =
                new HotbarItem[DefaultHotbarSize];
        }
    }

    private void ClampSelectedSlot()
    {
        if (SlotCount <= 0)
        {
            currentSelectedSlot = 0;
            return;
        }

        currentSelectedSlot =
            Mathf.Clamp(
                currentSelectedSlot,
                0,
                SlotCount - 1
            );
    }

    private bool IsValidSlotIndex(
        int slotIndex
    )
    {
        return
            slotIndex >= 0 &&
            slotIndex < SlotCount;
    }

    // ==================================================
    // SELECTED SLOT
    // ==================================================

    public void SetSelectedSlot(
        int slotIndex
    )
    {
        if (SlotCount <= 0)
            return;

        int newSlot =
            Mathf.Clamp(
                slotIndex,
                0,
                SlotCount - 1
            );

        if (newSlot == currentSelectedSlot)
            return;

        currentSelectedSlot = newSlot;

        SelectedSlotChanged?.Invoke(
            currentSelectedSlot
        );
    }

    // ==================================================
    // HOTBAR ITEM ACCESS
    // ==================================================

    public HotbarItem GetItem(
        int slotIndex
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return null;

        return hotbarItems[slotIndex];
    }

    public bool SetItem(
        int slotIndex,
        HotbarItem item
    )
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogWarning(
                $"Ungültiger Hotbar-Slot: {slotIndex}",
                gameObject
            );

            return false;
        }

        if (ReferenceEquals(
                hotbarItems[slotIndex],
                item))
        {
            return false;
        }

        hotbarItems[slotIndex] = item;

        NotifyHotbarChanged(slotIndex);

        return true;
    }

    public bool ClearItem(
        int slotIndex
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        if (hotbarItems[slotIndex] == null)
            return false;

        hotbarItems[slotIndex] = null;

        NotifyHotbarChanged(slotIndex);

        return true;
    }

    public bool SwapItems(
        int firstSlotIndex,
        int secondSlotIndex
    )
    {
        if (!IsValidSlotIndex(firstSlotIndex) ||
            !IsValidSlotIndex(secondSlotIndex))
        {
            return false;
        }

        if (firstSlotIndex ==
            secondSlotIndex)
        {
            return false;
        }

        HotbarItem temporaryItem =
            hotbarItems[firstSlotIndex];

        hotbarItems[firstSlotIndex] =
            hotbarItems[secondSlotIndex];

        hotbarItems[secondSlotIndex] =
            temporaryItem;

        NotifyHotbarChanged(firstSlotIndex);
        NotifyHotbarChanged(secondSlotIndex);

        return true;
    }

    public bool TryAddItem(
        HotbarItem item,
        out int addedSlotIndex
    )
    {
        addedSlotIndex = -1;

        if (item == null ||
            !item.IsValid)
        {
            return false;
        }

        for (int i = 0;
             i < SlotCount;
             i++)
        {
            if (hotbarItems[i] != null)
                continue;

            hotbarItems[i] = item;
            addedSlotIndex = i;

            NotifyHotbarChanged(i);

            return true;
        }

        return false;
    }

    public int FindFirstEmptySlot()
    {
        for (int i = 0;
             i < SlotCount;
             i++)
        {
            if (hotbarItems[i] == null)
                return i;
        }

        return -1;
    }

    public bool HasFreeSlot()
    {
        return FindFirstEmptySlot() >= 0;
    }

    // ==================================================
    // HOTBAR LOAD SUPPORT
    // ==================================================

    public void ClearHotbarForLoad()
    {
        EnsureValidHotbar();

        for (int i = 0;
             i < SlotCount;
             i++)
        {
            hotbarItems[i] = null;
        }
    }

    public bool TryLoadKnownItemIntoSlot(
        int slotIndex,
        string itemId
    )
    {
        EnsureValidHotbar();

        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogWarning(
                $"Hotbar-Item konnte nicht geladen werden. " +
                $"Ungültiger Slot: {slotIndex}",
                gameObject
            );

            return false;
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning(
                $"Hotbar-Slot {slotIndex} konnte nicht geladen werden, " +
                "weil die Item ID leer ist.",
                gameObject
            );

            return false;
        }

        if (!TryGetKnownHotbarItem(
                itemId.Trim(),
                out HotbarItem item))
        {
            Debug.LogWarning(
                $"Hotbar-Slot {slotIndex} konnte nicht geladen werden. " +
                $"Unbekannte Item ID: {itemId}",
                gameObject
            );

            return false;
        }

        hotbarItems[slotIndex] = item;

        return true;
    }

    public void NotifyHotbarLoadCompleted()
    {
        ClampSelectedSlot();

        HotbarChanged?.Invoke();
    }

    private bool TryGetKnownHotbarItem(
        string itemId,
        out HotbarItem item
    )
    {
        if (!knownHotbarItemLookupBuilt)
            BuildKnownHotbarItemLookup();

        return knownHotbarItemsById.TryGetValue(
            itemId,
            out item
        );
    }

    private void BuildKnownHotbarItemLookup()
    {
        knownHotbarItemsById.Clear();
        knownHotbarItemLookupBuilt = true;

        if (knownHotbarItems == null ||
            knownHotbarItems.Length == 0)
        {
            return;
        }

        for (int i = 0;
             i < knownHotbarItems.Length;
             i++)
        {
            HotbarItem item =
                knownHotbarItems[i];

            if (item == null)
                continue;

            if (!item.IsValid)
            {
                Debug.LogWarning(
                    $"Known Hotbar Item an Index {i} ist ungültig " +
                    "und wird ignoriert.",
                    gameObject
                );

                continue;
            }

            string itemId =
                item.ItemId.Trim();

            if (knownHotbarItemsById.ContainsKey(
                    itemId))
            {
                Debug.LogWarning(
                    $"Doppelte Hotbar Item ID '{itemId}' " +
                    $"an Index {i}. Diese Definition wird ignoriert.",
                    gameObject
                );

                continue;
            }

            knownHotbarItemsById.Add(
                itemId,
                item
            );
        }
    }

    private void NotifyHotbarChanged(
        int slotIndex
    )
    {
        HotbarItem item =
            GetItem(slotIndex);

        HotbarItemChanged?.Invoke(
            slotIndex,
            item
        );

        HotbarChanged?.Invoke();
    }

    // ==================================================
    // PERSONAL CASH
    // ==================================================

    public void AddCash(
        float amount
    )
    {
        int roundedAmount =
            Mathf.RoundToInt(amount);

        if (roundedAmount <= 0)
            return;

        currentCash += roundedAmount;

        CashChanged?.Invoke(
            currentCash
        );
    }

    public bool TrySpendCash(
        int amount
    )
    {
        if (amount <= 0 ||
            currentCash < amount)
        {
            return false;
        }

        currentCash -= amount;

        CashChanged?.Invoke(
            currentCash
        );

        return true;
    }

    public bool CanAffordCash(
        int amount
    )
    {
        if (amount < 0)
            return false;

        return currentCash >= amount;
    }

    public void SetCash(
        int amount
    )
    {
        int newAmount =
            Mathf.Max(0, amount);

        if (newAmount == currentCash)
            return;

        currentCash = newAmount;

        CashChanged?.Invoke(
            currentCash
        );
    }

    public void ClearCash()
    {
        SetCash(0);
    }

    // ==================================================
    // SAVE / LOAD SUPPORT
    // ==================================================

    public int GetCashForSave()
    {
        return currentCash;
    }

    public void LoadCash(
        int savedCash
    )
    {
        SetCash(savedCash);
    }
}