public class FluidStorageTank : StorageContainer
{
    public override bool AcceptsCargo(ItemData item)
    {
        return item != null && item.isFluid;
    }
}
