using System;

[Serializable]
public sealed class CharacterAppearanceData
{
    public const int CurrentVersion = 1;

    public int appearanceVersion = CurrentVersion;
    public string bodyId = string.Empty;
    public string hairId = string.Empty;
    public string faceId = string.Empty;
    public string upperId = string.Empty;
    public string pantsId = string.Empty;
    public string shoesId = string.Empty;

    public CharacterAppearanceData()
    {
    }

    public CharacterAppearanceData(CharacterAppearanceData source)
    {
        CopyFrom(source);
    }

    public CharacterAppearanceData Clone()
    {
        return new CharacterAppearanceData(this);
    }

    public void CopyFrom(CharacterAppearanceData source)
    {
        if (source == null)
        {
            appearanceVersion = CurrentVersion;
            bodyId = string.Empty;
            hairId = string.Empty;
            faceId = string.Empty;
            upperId = string.Empty;
            pantsId = string.Empty;
            shoesId = string.Empty;
            return;
        }

        appearanceVersion = source.appearanceVersion <= 0 ? CurrentVersion : source.appearanceVersion;
        bodyId = NormalizeId(source.bodyId);
        hairId = NormalizeId(source.hairId);
        faceId = NormalizeId(source.faceId);
        upperId = NormalizeId(source.upperId);
        pantsId = NormalizeId(source.pantsId);
        shoesId = NormalizeId(source.shoesId);
    }

    public string GetId(CharacterAppearanceCategory category)
    {
        switch (category)
        {
            case CharacterAppearanceCategory.Body:
                return bodyId;
            case CharacterAppearanceCategory.Hair:
                return hairId;
            case CharacterAppearanceCategory.Face:
                return faceId;
            case CharacterAppearanceCategory.Upper:
                return upperId;
            case CharacterAppearanceCategory.Pants:
                return pantsId;
            case CharacterAppearanceCategory.Shoes:
                return shoesId;
            default:
                return string.Empty;
        }
    }

    public void SetId(CharacterAppearanceCategory category, string id)
    {
        string normalizedId = NormalizeId(id);

        switch (category)
        {
            case CharacterAppearanceCategory.Body:
                bodyId = normalizedId;
                break;
            case CharacterAppearanceCategory.Hair:
                hairId = normalizedId;
                break;
            case CharacterAppearanceCategory.Face:
                faceId = normalizedId;
                break;
            case CharacterAppearanceCategory.Upper:
                upperId = normalizedId;
                break;
            case CharacterAppearanceCategory.Pants:
                pantsId = normalizedId;
                break;
            case CharacterAppearanceCategory.Shoes:
                shoesId = normalizedId;
                break;
        }
    }

    public static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
