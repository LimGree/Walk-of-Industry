using UnityEngine;

public class Pipe : Conveyor
{
    protected override bool AcceptsItem(ItemData item)
    {
        return item != null && item.isFluid;
    }

    protected override bool ShowCargoVisual => false;
}
