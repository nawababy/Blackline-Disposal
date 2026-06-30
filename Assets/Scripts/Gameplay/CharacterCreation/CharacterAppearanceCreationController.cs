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

        ResolveCurrentAppearance();
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
        ResolveCurrentAppearance();
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

    public void NextBodyType()
    {
        ChangeBodyType(1);
    }

    public void PreviousBodyType()
    {
        ChangeBodyType(-1);
    }

    public void NextSkin()
    {
        ChangeSkin(1);
    }

    public void PreviousSkin()
    {
        ChangeSkin(-1);
    }

    public void NextBody()
    {
        NextBodyType();
    }

    public void PreviousBody()
    {
        PreviousBodyType();
    }

    public void NextHair()
    {
        ChangePartSelection(CharacterAppearanceCategory.Hair, 1);
    }

    public void PreviousHair()
    {
        ChangePartSelection(CharacterAppearanceCategory.Hair, -1);
    }

    public void NextFace()
    {
        ChangePartSelection(CharacterAppearanceCategory.Face, 1);
    }

    public void PreviousFace()
    {
        ChangePartSelection(CharacterAppearanceCategory.Face, -1);
    }

    public void NextUpper()
    {
        ChangePartSelection(CharacterAppearanceCategory.Upper, 1);
    }

    public void PreviousUpper()
    {
        ChangePartSelection(CharacterAppearanceCategory.Upper, -1);
    }

    public void NextPants()
    {
        ChangePartSelection(CharacterAppearanceCategory.Pants, 1);
    }

    public void PreviousPants()
    {
        ChangePartSelection(CharacterAppearanceCategory.Pants, -1);
    }

    public void NextShoes()
    {
        ChangePartSelection(CharacterAppearanceCategory.Shoes, 1);
    }

    public void PreviousShoes()
    {
        ChangePartSelection(CharacterAppearanceCategory.Shoes, -1);
    }

    private void ChangeBodyType(int direction)
    {
        EnsureAppearanceData();
        if (!CanUseDatabase())
        {
            return;
        }

        ResolveCurrentAppearance();

        CharacterAppearanceDatabase.BodyTypeDefinition nextBodyType;
        if (!database.TryGetNextBodyType(currentAppearance.bodyTypeId, direction, out nextBodyType))
        {
            Debug.LogWarning("[CharacterAppearanceCreationController] No body types are configured.", this);
            return;
        }

        currentAppearance.bodyTypeId = nextBodyType.BodyTypeId;
        currentAppearance.skinId = nextBodyType.DefaultSkinId;
        ResolveCurrentAppearance();
        ApplyCurrentAppearance();
    }

    private void ChangeSkin(int direction)
    {
        EnsureAppearanceData();
        if (!CanUseDatabase())
        {
            return;
        }

        ResolveCurrentAppearance();

        CharacterAppearanceDatabase.BodyDefinition nextSkin;
        if (!database.TryGetNextSkin(currentAppearance.bodyTypeId, currentAppearance.skinId, direction, out nextSkin))
        {
            Debug.LogWarning("[CharacterAppearanceCreationController] No skin variants are configured for BodyType '" + currentAppearance.bodyTypeId + "'.", this);
            return;
        }

        currentAppearance.skinId = nextSkin.SkinId;
        ResolveCurrentAppearance();
        ApplyCurrentAppearance();
    }

    private void ChangePartSelection(CharacterAppearanceCategory category, int direction)
    {
        EnsureAppearanceData();
        if (!CanUseDatabase())
        {
            return;
        }

        ResolveCurrentAppearance();

        CharacterAppearanceDatabase.AppearanceDefinition nextDefinition;
        if (!database.TryGetNextDefinition(category, currentAppearance.bodyTypeId, currentAppearance.skinId, currentAppearance.GetId(category), direction, out nextDefinition))
        {
            Debug.LogWarning("[CharacterAppearanceCreationController] No compatible entries found for BodyType '" + currentAppearance.bodyTypeId + "' category " + category + ".", this);
            return;
        }

        currentAppearance.SetId(category, nextDefinition.Id);
        ResolveCurrentAppearance();
        ApplyCurrentAppearance();
    }

    private void ResolveCurrentAppearance()
    {
        EnsureAppearanceData();
        if (database == null)
        {
            return;
        }

        database.ResolveAppearance(currentAppearance, currentAppearance, this);
    }

    private bool CanUseDatabase()
    {
        if (database != null)
        {
            return true;
        }

        Debug.LogError("[CharacterAppearanceCreationController] Cannot change appearance because no CharacterAppearanceDatabase is assigned.", this);
        return false;
    }

    private void EnsureAppearanceData()
    {
        if (currentAppearance == null)
        {
            currentAppearance = new CharacterAppearanceData();
        }
    }
}
