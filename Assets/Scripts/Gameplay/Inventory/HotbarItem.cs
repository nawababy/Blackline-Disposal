using UnityEngine;

[System.Serializable]
public sealed class HotbarItem
{
    [Header("Item Information")]
    [SerializeField] private string itemId = "item.default";
    [SerializeField] private string itemName = "New Item";

    [Header("Visuals")]
    [SerializeField] private Sprite icon;

    [Header("Optional Prefab")]
    [SerializeField] private GameObject heldPrefab;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public Sprite Icon => icon;
    public GameObject HeldPrefab => heldPrefab;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(itemId) &&
        !string.IsNullOrWhiteSpace(itemName);
}