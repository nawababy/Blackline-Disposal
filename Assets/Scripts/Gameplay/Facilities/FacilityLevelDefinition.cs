using UnityEngine;

[System.Serializable]
public class FacilityLevelDefinition
{
    [Header("Level Information")]
    public string levelName = "Level 1";

    [Header("Processing")]
    [Min(0.1f)]
    public float processTime = 300f;

    [Min(0f)]
    public float valueMultiplier = 1f;

    [Header("Upgrade")]
    [Min(0)]
    public int upgradeCost;
}