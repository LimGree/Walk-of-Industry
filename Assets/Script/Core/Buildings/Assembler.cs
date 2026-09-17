using UnityEngine;

public class Assembler : CrafterBuilding
{
    [Header("Upgrade")]
    public float upgradedCraftSpeed = 2f;

    GameObject level1Visual;
    GameObject level2Visual;

    public bool CanUpgrade => level < 2;
    public override bool CanUpgradeBuilding =>
        CanUpgrade
        && ResearchSystem.Instance != null
        && ResearchSystem.Instance.IsResearchIdUnlocked("research_assembler_2");
    public override float CraftSpeed => level >= 2 ? Mathf.Max(1f, upgradedCraftSpeed) : 1f;

    void Awake()
    {
        EnsureCollider();
        BindVisuals();
        ApplyLevel();
    }

    public override void OnPlaced()
    {
        base.OnPlaced();
        EnsureCollider();
        BindVisuals();
        ApplyLevel();
    }

    public bool TryUpgrade()
    {
        return TryUpgradeBuilding();
    }

    public override bool TryUpgradeBuilding()
    {
        if (!CanUpgradeBuilding)
            return false;

        level = 2;
        ApplyLevel();
        return true;
    }

    public override int ReadLevel()
    {
        return level;
    }

    public override void ApplyLevel(int savedLevel)
    {
        if (savedLevel < 2)
            return;
        level = 2;
        ApplyLevel();
    }

    void ApplyLevel()
    {
        BuildingVisuals.ApplyLevel(this, level);
        if (level1Visual != null)
            level1Visual.SetActive(level < 2);
        if (level2Visual != null)
            level2Visual.SetActive(level >= 2);
    }

    void BindVisuals()
    {
        Transform l1 = BuildingPrefabLayout.FindLevel1(transform);
        Transform l2 = BuildingPrefabLayout.FindLevel2(transform);
        if (level1Visual == null && l1 != null)
            level1Visual = l1.gameObject;
        if (level2Visual == null && l2 != null)
            level2Visual = l2.gameObject;
        if (level1Visual == null)
            level1Visual = FindNamedChild("Assembler_Level_1");
        if (level2Visual == null)
            level2Visual = FindNamedChild("Assembler_level_2");

        DisableChildColliders(level1Visual);
        DisableChildColliders(level2Visual);
    }

    GameObject FindNamedChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i] != transform && children[i].name == childName)
                return children[i].gameObject;
        }

        return null;
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        float cell = GridFootprint.CellSize * 0.84f;
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            cell / Mathf.Max(0.01f, lossy.x),
            0.9f / Mathf.Max(0.01f, lossy.y),
            cell / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.45f, 0f);
        box.enabled = true;
    }

    static void DisableChildColliders(GameObject root)
    {
        if (root == null)
            return;

        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }
}
