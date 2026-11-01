using UnityEngine;

public enum ItemClass : byte
{
    Small,
    Medium,
    Big
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Office Clash/Item Data")]
public class ItemData : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public ItemClass itemClass = ItemClass.Small;

    [Header("Damage")]
    public float baseDamage = 10f;

    //Below this impact speed the hit does no knockdown
    public float knockdownSpeed = 6f;

    //Small items only knock down when thrown at full charge
    public bool knockdownOnlyAtFullCharge = false;

    [Header("Spawning")]
    //Higher means it appears more often, 0 never spawns
    [Min(0f)] public float spawnWeight = 1f;

    [Header("Throwing")]
    public float throwSpeedMultiplier = 1f;

    public bool CanGoInPocket => itemClass != ItemClass.Big;

    public int HandsRequired => itemClass == ItemClass.Big ? 2 : 1;
}
