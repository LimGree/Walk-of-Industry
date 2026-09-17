using UnityEngine;
using System;

[Serializable]
public class ItemStack
{
    public ItemData item;
    public int amount = 1;

    public ItemStack() { }

    public ItemStack(ItemData item, int amount = 1)
    {
        this.item = item;
        this.amount = amount;
    }

    public bool IsEmpty => item == null || amount <= 0;

    public ItemStack Clone()
    {
        return new ItemStack(item, amount);
    }
}