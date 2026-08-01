using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerInventoryHotbarTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance |
        BindingFlags.NonPublic;

    private GameObject inventoryGameObject;
    private PlayerInventory inventory;
    private FieldInfo hotbarItemsField;
    private MethodInfo ensureValidHotbarMethod;
    private FieldInfo itemIdField;
    private FieldInfo itemNameField;

    [SetUp]
    public void SetUp()
    {
        inventoryGameObject =
            new GameObject("PlayerInventoryHotbarTests");

        inventory =
            inventoryGameObject.AddComponent<PlayerInventory>();

        hotbarItemsField =
            typeof(PlayerInventory).GetField(
                "hotbarItems",
                PrivateInstance
            );

        ensureValidHotbarMethod =
            typeof(PlayerInventory).GetMethod(
                "EnsureValidHotbar",
                PrivateInstance
            );

        itemIdField =
            typeof(HotbarItem).GetField(
                "itemId",
                PrivateInstance
            );

        itemNameField =
            typeof(HotbarItem).GetField(
                "itemName",
                PrivateInstance
            );

        Assert.That(hotbarItemsField, Is.Not.Null);
        Assert.That(ensureValidHotbarMethod, Is.Not.Null);
        Assert.That(itemIdField, Is.Not.Null);
        Assert.That(itemNameField, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(inventoryGameObject);
    }

    [Test]
    public void EnsureValidHotbar_NullSlot_RemainsNull()
    {
        SetHotbar((HotbarItem)null);

        NormalizeHotbar();

        Assert.That(GetHotbar()[0], Is.Null);
    }

    [Test]
    public void EnsureValidHotbar_InvalidDefaultRecord_BecomesNull()
    {
        HotbarItem invalidItem =
            CreateItem("item.default", string.Empty);

        Assert.That(invalidItem.IsValid, Is.False);

        SetHotbar(invalidItem);
        NormalizeHotbar();

        Assert.That(GetHotbar()[0], Is.Null);
    }

    [Test]
    public void EnsureValidHotbar_EmptyItemId_BecomesNull()
    {
        HotbarItem invalidItem =
            CreateItem(string.Empty, "Valid Item Name");

        SetHotbar(invalidItem);
        NormalizeHotbar();

        Assert.That(GetHotbar()[0], Is.Null);
    }

    [Test]
    public void EnsureValidHotbar_ValidItem_RemainsUnchanged()
    {
        HotbarItem validItem =
            CreateItem("item.valid", "Valid Item");

        SetHotbar(validItem);
        NormalizeHotbar();

        Assert.That(GetHotbar()[0], Is.SameAs(validItem));
    }

    [Test]
    public void EnsureValidHotbar_MixedSlots_NormalizesOnlyInvalidItems()
    {
        HotbarItem firstValidItem =
            CreateItem("item.first", "First Item");

        HotbarItem invalidItem =
            CreateItem("item.default", string.Empty);

        HotbarItem secondValidItem =
            CreateItem("item.second", "Second Item");

        SetHotbar(
            firstValidItem,
            invalidItem,
            null,
            secondValidItem
        );

        NormalizeHotbar();

        HotbarItem[] normalizedHotbar =
            GetHotbar();

        Assert.That(normalizedHotbar[0], Is.SameAs(firstValidItem));
        Assert.That(normalizedHotbar[1], Is.Null);
        Assert.That(normalizedHotbar[2], Is.Null);
        Assert.That(normalizedHotbar[3], Is.SameAs(secondValidItem));
    }

    [Test]
    public void FindFirstEmptySlot_AfterNormalization_ReturnsNormalizedSlot()
    {
        SetHotbar(
            CreateItem("item.first", "First Item"),
            CreateItem("item.default", string.Empty),
            CreateItem("item.third", "Third Item")
        );

        NormalizeHotbar();

        Assert.That(inventory.FindFirstEmptySlot(), Is.EqualTo(1));
    }

    [Test]
    public void TryAddItem_AfterNormalization_UsesNormalizedSlot()
    {
        HotbarItem addedItem =
            CreateItem("item.added", "Added Item");

        SetHotbar(
            CreateItem("item.default", string.Empty)
        );

        NormalizeHotbar();

        bool wasAdded =
            inventory.TryAddItem(
                addedItem,
                out int addedSlotIndex
            );

        Assert.That(wasAdded, Is.True);
        Assert.That(addedSlotIndex, Is.Zero);
        Assert.That(GetHotbar()[0], Is.SameAs(addedItem));
    }

    [Test]
    public void EnsureValidHotbar_MultipleCalls_AreIdempotent()
    {
        HotbarItem validItem =
            CreateItem("item.valid", "Valid Item");

        SetHotbar(
            validItem,
            CreateItem("item.default", string.Empty),
            null
        );

        NormalizeHotbar();

        HotbarItem[] normalizedHotbar =
            GetHotbar();

        NormalizeHotbar();

        HotbarItem[] normalizedAgain =
            GetHotbar();

        Assert.That(normalizedAgain, Is.SameAs(normalizedHotbar));
        Assert.That(normalizedAgain.Length, Is.EqualTo(normalizedHotbar.Length));
        Assert.That(normalizedAgain[0], Is.SameAs(validItem));
        Assert.That(normalizedAgain[1], Is.Null);
        Assert.That(normalizedAgain[2], Is.Null);
    }

    [Test]
    public void EnsureValidHotbar_PreservesSlotCount()
    {
        SetHotbar(
            null,
            CreateItem("item.default", string.Empty),
            CreateItem("item.valid", "Valid Item"),
            null,
            null
        );

        NormalizeHotbar();

        Assert.That(GetHotbar().Length, Is.EqualTo(5));
        Assert.That(inventory.SlotCount, Is.EqualTo(5));
    }

    [Test]
    public void EnsureValidHotbar_DoesNotRaiseHotbarEvents()
    {
        int hotbarChangedCount = 0;
        int hotbarItemChangedCount = 0;

        inventory.HotbarChanged +=
            () => hotbarChangedCount++;

        inventory.HotbarItemChanged +=
            (_, _) => hotbarItemChangedCount++;

        SetHotbar(
            CreateItem("item.default", string.Empty)
        );

        NormalizeHotbar();

        Assert.That(hotbarChangedCount, Is.Zero);
        Assert.That(hotbarItemChangedCount, Is.Zero);
    }

    [Test]
    public void EnsureValidHotbar_DoesNotChangeSaveDataOrVersion()
    {
        SaveGameData saveData =
            SaveGameData.CreateNew(0, "Hotbar Test");

        int saveVersionBefore =
            saveData.saveVersion;

        string saveJsonBefore =
            JsonUtility.ToJson(saveData);

        SetHotbar(
            CreateItem("item.default", string.Empty)
        );

        NormalizeHotbar();

        Assert.That(
            saveData.saveVersion,
            Is.EqualTo(saveVersionBefore)
        );

        Assert.That(
            SaveGameData.CurrentSaveVersion,
            Is.EqualTo(saveVersionBefore)
        );

        Assert.That(
            JsonUtility.ToJson(saveData),
            Is.EqualTo(saveJsonBefore)
        );
    }

    private HotbarItem CreateItem(
        string itemId,
        string itemName
    )
    {
        HotbarItem item =
            new HotbarItem();

        itemIdField.SetValue(item, itemId);
        itemNameField.SetValue(item, itemName);

        return item;
    }

    private void SetHotbar(
        params HotbarItem[] items
    )
    {
        hotbarItemsField.SetValue(
            inventory,
            items
        );
    }

    private HotbarItem[] GetHotbar()
    {
        return (HotbarItem[])hotbarItemsField.GetValue(
            inventory
        );
    }

    private void NormalizeHotbar()
    {
        ensureValidHotbarMethod.Invoke(
            inventory,
            null
        );
    }
}
