using System;
using UnityEngine;

public enum InputBindingId
{
    Jump,
    Crouch,
    Sprint,
    Interact,
    Pickup,
    Drop,
    Pause
}

[CreateAssetMenu(menuName = "Blackline Disposal/Input Settings")]
public sealed class InputSettings : ScriptableObject
{
    [Header("Movement")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode crouchKey = KeyCode.C;
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode pickupKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.G;

    [Header("Menu")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Toggle / Hold")]
    public bool crouchToggle = true;
    public bool pickupHold = true;

    public event Action Changed;

    private const string JumpKey = "Controls_Jump";
    private const string CrouchKey = "Controls_Crouch";
    private const string SprintKey = "Controls_Sprint";
    private const string InteractKey = "Controls_Interact";
    private const string PickupKey = "Controls_Pickup";
    private const string DropKey = "Controls_Drop";
    private const string PauseKey = "Controls_Pause";

    private const string CrouchToggleKey = "Controls_CrouchToggle";
    private const string PickupHoldKey = "Controls_PickupHold";

    private bool hasLoaded;

    private void OnEnable()
    {
        hasLoaded = false;

        if (Application.isPlaying)
            EnsureLoaded();
    }

    public void EnsureLoaded()
    {
        if (!hasLoaded)
            Load();
    }

    public KeyCode GetKey(InputBindingId binding)
    {
        EnsureLoaded();

        switch (binding)
        {
            case InputBindingId.Jump:
                return jumpKey;

            case InputBindingId.Crouch:
                return crouchKey;

            case InputBindingId.Sprint:
                return sprintKey;

            case InputBindingId.Interact:
                return interactKey;

            case InputBindingId.Pickup:
                return pickupKey;

            case InputBindingId.Drop:
                return dropKey;

            case InputBindingId.Pause:
                return pauseKey;

            default:
                return KeyCode.None;
        }
    }

    public void SetKey(
        InputBindingId binding,
        KeyCode newKey
    )
    {
        EnsureLoaded();

        if (newKey == KeyCode.None)
            return;

        if (GetKey(binding) == newKey)
            return;

        switch (binding)
        {
            case InputBindingId.Jump:
                jumpKey = newKey;
                break;

            case InputBindingId.Crouch:
                crouchKey = newKey;
                break;

            case InputBindingId.Sprint:
                sprintKey = newKey;
                break;

            case InputBindingId.Interact:
                interactKey = newKey;
                break;

            case InputBindingId.Pickup:
                pickupKey = newKey;
                break;

            case InputBindingId.Drop:
                dropKey = newKey;
                break;

            case InputBindingId.Pause:
                pauseKey = newKey;
                break;
        }

        Save();
        Changed?.Invoke();
    }

    public void SetCrouchToggle(bool enabled)
    {
        EnsureLoaded();

        if (crouchToggle == enabled)
            return;

        crouchToggle = enabled;

        Save();
        Changed?.Invoke();
    }

    public void SetPickupHold(bool enabled)
    {
        EnsureLoaded();

        if (pickupHold == enabled)
            return;

        pickupHold = enabled;

        Save();
        Changed?.Invoke();
    }

    public void Load()
    {
        jumpKey = ReadKey(
            JumpKey,
            KeyCode.Space
        );

        crouchKey = ReadKey(
            CrouchKey,
            KeyCode.C
        );

        sprintKey = ReadKey(
            SprintKey,
            KeyCode.LeftShift
        );

        interactKey = ReadKey(
            InteractKey,
            KeyCode.E
        );

        pickupKey = ReadKey(
            PickupKey,
            KeyCode.E
        );

        dropKey = ReadKey(
            DropKey,
            KeyCode.G
        );

        pauseKey = ReadKey(
            PauseKey,
            KeyCode.Escape
        );

        crouchToggle = PlayerPrefs.GetInt(
            CrouchToggleKey,
            1
        ) == 1;

        pickupHold = PlayerPrefs.GetInt(
            PickupHoldKey,
            1
        ) == 1;

        hasLoaded = true;

        Changed?.Invoke();
    }

    public void Save()
    {
        PlayerPrefs.SetInt(
            JumpKey,
            (int)jumpKey
        );

        PlayerPrefs.SetInt(
            CrouchKey,
            (int)crouchKey
        );

        PlayerPrefs.SetInt(
            SprintKey,
            (int)sprintKey
        );

        PlayerPrefs.SetInt(
            InteractKey,
            (int)interactKey
        );

        PlayerPrefs.SetInt(
            PickupKey,
            (int)pickupKey
        );

        PlayerPrefs.SetInt(
            DropKey,
            (int)dropKey
        );

        PlayerPrefs.SetInt(
            PauseKey,
            (int)pauseKey
        );

        PlayerPrefs.SetInt(
            CrouchToggleKey,
            crouchToggle ? 1 : 0
        );

        PlayerPrefs.SetInt(
            PickupHoldKey,
            pickupHold ? 1 : 0
        );

        PlayerPrefs.Save();
    }

    public void ResetToDefaults()
    {
        jumpKey = KeyCode.Space;
        crouchKey = KeyCode.C;
        sprintKey = KeyCode.LeftShift;

        interactKey = KeyCode.E;
        pickupKey = KeyCode.E;
        dropKey = KeyCode.G;

        pauseKey = KeyCode.Escape;

        crouchToggle = true;
        pickupHold = true;

        hasLoaded = true;

        Save();
        Changed?.Invoke();
    }

    private KeyCode ReadKey(
        string playerPrefsKey,
        KeyCode defaultKey
    )
    {
        int savedValue = PlayerPrefs.GetInt(
            playerPrefsKey,
            (int)defaultKey
        );

        if (!Enum.IsDefined(
                typeof(KeyCode),
                savedValue))
        {
            return defaultKey;
        }

        return (KeyCode)savedValue;
    }
}