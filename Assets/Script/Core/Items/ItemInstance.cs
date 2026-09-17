using UnityEngine;

[System.Serializable]
public class ItemInstance
{
    public ItemData data;
    public int amount = 1;

    public ItemInstance(ItemData data, int amount = 1)
    {
        this.data = data;
        this.amount = amount;
    }

    public bool IsValid => data != null && amount > 0;
}