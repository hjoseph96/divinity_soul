using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "ScriptableObjects/Weapon", order = 3)]
public class ScriptableWeapon : ScriptableItem
{
    public WeaponType Type;

    [FoldoutGroup("Descriptions"), MultiLineProperty(5)] 
    public string DescriptionBroken;

    [FoldoutGroup("Stats"), WeaponStats]
    public Dictionary<WeaponStat, EditorWeaponStat> WeaponStats = new Dictionary<WeaponStat, EditorWeaponStat>();

    [FoldoutGroup("Stats"), MinMaxSlider(1, 4)]
    public Vector2Int AttackRange = Vector2Int.one;

    [FoldoutGroup("Stats")] 
    public WeaponRank RequiredRank;

    [FoldoutGroup("Stats")]
    public int Weight, MaxDurability;

    [FoldoutGroup("Active Ability")]
    public bool IsUsable;

    [FoldoutGroup("Active Ability"), ShowIf("IsUsable")]
    public ScriptableAction Action;

    public override Item GetItem()
    {
        return new Weapon(this);
    }
}