using UnityEngine;

public sealed class CharacterAppearanceCreationController : MonoBehaviour
{
    [SerializeField] private CharacterAppearanceDatabase database;
    [SerializeField] private CharacterAppearanceApplier applier;
    [SerializeField] private CharacterAppearanceData currentAppearance = new CharacterAppearanceData();
    [SerializeField] private bool applyOnEnable = true;

    public CharacterAppearanceData CurrentAppearance
    {
        get
        {
            EnsureAppearanceData();
            return currentAppearance;
        }
    }

    private void OnEnable()
    {
        if (!applyOnEnable)
        {
            return;
        }

        ApplyDefaultsIfNeeded();
        ApplyCurrentAppearance();
    }

    private void OnValidate()
    {
        EnsureAppearanceData();
    }

    public void SetDatabase(CharacterAppearanceDatabase newDatabase)
    {
        database = newDatabase;
        if (applier != null)
        {
            applier.SetDatabase(database);
        }
    }

    public void SetApplier(CharacterAppearanceApplier newApplier)
    {
        applier = newApplier;
        if (applier != null)
        {
            applier.SetDatabase(database);
        }
    }

    public void SetAppearance(CharacterAppearanceData appearanceData)
    {
        EnsureAppearanceData();
        currentAppearance.CopyFrom(appearanceData);
        ApplyDefaultsIfNeeded();
        ApplyCurrentAppearance();
    }

    public void ApplyCurrentAppearance()
    {
        EnsureAppearanceData();

        if (applier == null)
        {
            Debug.LogError("[CharacterAppearanceCreationController] Cannot refresh preview because no CharacterAppearanceApplier is assigned.", this);
            return;
        }

        if (database == null)
        {
            Debug.LogError("[CharacterAppearanceCreationController] Cannot refresh preview because no CharacterAppearanceDatabase is assigned.", this);
            return;
        }

        applier.SetDatabase(database);
        applier.ApplyAppearance(currentAppearance);
    }

    public void NextBody()
    {
        ChangeSelection(CharacterAppearanceCategory.Body, 1);
    }

    public void PreviousBody()
    {
        ChangeSelection(CharacterAppearanceCategory.Body, -1);
    }

    public void NextHair()
    {
        ChangeSelection(CharacterAppearanceCategory.Hair, 1);
    }

    public void PreviousHair()
    {
        ChangeSelection(CharacterAppearanceCategory.Hair, -1);
    }

    public void NextFace()
    {
        ChangeSelection(CharacterAppearanceCategory.Face, 1);
    }

    public void PreviousFace()
    {
        ChangeSelection(CharacterAppearanceCategory.Face, -1);
    }

    public void NextUpper()
    {
        ChangeSelection(CharacterAppearanceCategory.Upper, 1);
    }

    public void PreviousUpper()
    {
        ChangeSelection(CharacterAppearanceCategory.Upper, -1);
    }

    public void NextPants()
    {
        ChangeSelection(CharacterAppearanceCategory.Pants, 1);
    }

    public void PreviousPants()
    {
        ChangeSelection(CharacterAppearanceCategory.Pants, -1);
    }

    public void NextShoes()
    {
        ChangeSelection(CharacterAppearanceCategory.Shoes, 1);
    }

    public void PreviousShoes()
    {
        ChangeSelection(CharacterAppearanceCategory.Shoes, -1);
    }

    private void ChangeSelection(CharacterAppearanceCategory category, int direction)
    {
        EnsureAppearanceData();

        if (database == null)
        {
            Debug.LogError("[CharacterAppearanceCreationController] Cannot change " + category + " because no CharacterAppearanceDatabase is assigned.", this);
            return;
        }

        CharacterAppearanceDatabase.AppearanceDefinition nextDefinition;
        if (!database.TryGetNextDefinition(category, currentAppearance.GetId(category), direction, out nextDefinition))
        {
            Debug.LogWarning("[CharacterAppearanceCreationController] No compatible Adult Female entries found for category " + category + ".", this);
            return;
        }

        currentAppearance.SetId(category, nextDefinition.Id);
        ApplyDefaultsIfNeeded();
        ApplyCurrentAppearance();
    }

    private void ApplyDefaultsIfNeeded()
    {
        EnsureAppearanceData();

        if (database == null)
        {
            return;
        }

        ApplyDefaultIfNeeded(CharacterAppearanceCategory.Body);
        ApplyDefaultIfNeeded(CharacterAppearanceCategory.Hair);
        ApplyDefaultIfNeeded(CharacterAppearanceCategory.Face);
        ApplyDefaultIfNeeded(CharacterAppearanceCategory.Upper);
        ApplyDefaultIfNeeded(CharacterAppearanceCategory.Pants);
        ApplyDefaultIfNeeded(CharacterAppearanceCategory.Shoes);
    }

    private void ApplyDefaultIfNeeded(CharacterAppearanceCategory category)
    {
        CharacterAppearanceDatabase.AppearanceDefinition definition;
        if (database.TryGetDefinition(category, currentAppearance.GetId(category), out definition))
        {
            currentAppearance.SetId(category, definition.Id);
            return;
        }

        if (database.TryGetDefaultDefinition(category, out definition))
        {
            currentAppearance.SetId(category, definition.Id);
        }
    }

    private void EnsureAppearanceData()
    {
        if (currentAppearance == null)
        {
            currentAppearance = new CharacterAppearanceData();
        }
    }
}
