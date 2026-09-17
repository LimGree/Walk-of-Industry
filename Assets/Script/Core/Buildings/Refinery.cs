using UnityEngine;

public class Refinery : CrafterBuilding
{
    protected override string WorkClip => "bld_refinery_loop";

    BuildingSocket pipeSocket;
    BuildingSocket beltSocket;

    void Awake()
    {
        CacheDefaultSockets();
    }

    public override void OnPlaced()
    {
        CacheDefaultSockets();
        ApplyPorts();
        base.OnPlaced();
    }

    public override void SetRecipe(RecipeData recipe)
    {
        base.SetRecipe(recipe);
        ApplyPorts();
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        ApplyPorts();
    }

    protected override bool AcceptsItem(ItemData item)
    {
        if (item == null)
            return false;
        if (currentRecipe == null)
            return item.isFluid;
        if (currentRecipe.inputs == null)
            return false;
        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            if (currentRecipe.inputs[i].item == item)
                return true;
        }
        return false;
    }

    void CacheDefaultSockets()
    {
        if (pipeSocket == null && inputSockets != null && inputSockets.Length > 0)
            pipeSocket = inputSockets[0];
        if (beltSocket == null && outputSockets != null && outputSockets.Length > 0)
            beltSocket = outputSockets[0];
    }

    void ApplyPorts()
    {
        CacheDefaultSockets();
        if (pipeSocket == null || beltSocket == null)
            return;

        pipeSocket.DisconnectAll();
        beltSocket.DisconnectAll();

        if (RecipeOutputsFluid(currentRecipe))
        {
            beltSocket.socketType = SocketType.Input;
            pipeSocket.socketType = SocketType.Output;
            inputSockets = new[] { beltSocket };
            outputSockets = new[] { pipeSocket };
        }
        else
        {
            pipeSocket.socketType = SocketType.Input;
            beltSocket.socketType = SocketType.Output;
            inputSockets = new[] { pipeSocket };
            outputSockets = new[] { beltSocket };
        }

        BuildingLinker.RelinkAround(this);
    }

    static bool RecipeOutputsFluid(RecipeData recipe)
    {
        if (recipe == null || recipe.outputs == null)
            return false;
        for (int i = 0; i < recipe.outputs.Count; i++)
        {
            ItemData item = recipe.outputs[i].item;
            if (item != null && item.isFluid)
                return true;
        }
        return false;
    }
}
