using UnityEngine;

public sealed class HotbarController : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Control")]
    [SerializeField] private bool canControl = true;

    private void Update()
    {
        if (!canControl || inventory == null || inventory.SlotCount <= 0)
            return;

        HandleNumberKeys();
        HandleMouseScroll();
    }

    private void HandleNumberKeys()
    {
        int usableSlots = Mathf.Min(inventory.SlotCount, 9);

        for (int i = 0; i < usableSlots; i++)
        {
            KeyCode numberKey = KeyCode.Alpha1 + i;

            if (Input.GetKeyDown(numberKey))
            {
                inventory.SetSelectedSlot(i);
                return;
            }
        }
    }

    private void HandleMouseScroll()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(scrollInput, 0f))
            return;

        int direction = scrollInput > 0f ? -1 : 1;
        int nextSlot = inventory.selectedSlot + direction;

        if (nextSlot < 0)
            nextSlot = inventory.SlotCount - 1;
        else if (nextSlot >= inventory.SlotCount)
            nextSlot = 0;

        inventory.SetSelectedSlot(nextSlot);
    }

    public void SetControlEnabled(bool value)
    {
        canControl = value;
    }

    public void SetInventory(PlayerInventory newInventory)
    {
        inventory = newInventory;
    }
}