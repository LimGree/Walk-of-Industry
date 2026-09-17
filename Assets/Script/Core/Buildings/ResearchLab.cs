using UnityEngine;

public class ResearchLab : BuildingBase, IInteractable
{
    public bool IsWorldLab { get; private set; }

    public ResearchNodeData currentResearch
    {
        get
        {
            return ResearchSystem.Instance != null
                ? ResearchSystem.Instance.CurrentResearch
                : null;
        }
    }

    public override void OnPlaced()
    {
        IsWorldLab = true;
        base.OnPlaced();
    }

    public override void OnRemoved()
    {
        IsWorldLab = false;
        base.OnRemoved();
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (ResearchSystem.Instance == null)
            return false;

        return ResearchSystem.Instance.TrySubmitItem(item);
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
        else
            Debug.LogError("MachineUI.Instance == null!");
    }

    public void SetResearch(ResearchNodeData research)
    {
        if (ResearchSystem.Instance == null)
        {
            Debug.LogError("[ResearchLab] ResearchSystem.Instance == null");
            return;
        }

        ResearchSystem.Instance.SetCurrentResearch(research);
    }

    public float GetProgress01()
    {
        return ResearchSystem.Instance != null
            ? ResearchSystem.Instance.GetCurrentProgress01()
            : 0f;
    }

    public float GetProgress()
    {
        return GetProgress01();
    }
}
