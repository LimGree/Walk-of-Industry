using UnityEngine;

public class HotbarSlotView : MonoBehaviour
{
    public int index;

    public void Bind(InventoryUI owner, int slot)
    {
        index = slot;
    }
}
