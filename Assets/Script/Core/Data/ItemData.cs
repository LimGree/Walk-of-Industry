using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Builderment/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;                    // ���������� ������������� (�������� "iron_ore")
    public string displayName;           // "�������� ����"
    [TextArea] public string description;

    [Header("Visuals")]
    public Sprite icon;                  // ������ ��� UI � ���������
    public GameObject worldPrefab;       // ������ ��������, ������� ���� �� ���������

    [Header("Settings")]
    public int maxStack = 100;
    public bool isFuel = false;
    public float fuelValue = 0f;

    [Header("Future")]
    [Tooltip("Жидкость: нефть, вода, кислота. Пока на ленты не влияет.")]
    public bool isFluid;

    [Header("Economy")]
    [Tooltip("Монеты за сдачу одного предмета в лабораторию.")]
    public int sellValue;
}