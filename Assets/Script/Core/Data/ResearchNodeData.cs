using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Research", menuName = "Builderment/Research Node")]
public class ResearchNodeData : ScriptableObject
{
    public string Title => DataLocale.ResearchName(id, displayName);
    public string Info => DataLocale.ResearchDesc(id, description);

    [Header("Basic Info")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Requirements")]
    public List<ItemStack> requiredItems = new List<ItemStack>();   // ��� ����� ����� � Research Lab
    public List<ResearchNodeData> requiredResearches;               // ����� ������������ ������ ���� ������� �� �����

    [Header("Rewards")]
    public List<BuildingData> unlockedBuildings;    // ����� ������ ���������
    public List<RecipeData> unlockedRecipes;        // ����� ������� ���������
    [Tooltip("Рубины за завершение. 0 = авто по объёму сдачи.")]
    public int rubyReward;
}