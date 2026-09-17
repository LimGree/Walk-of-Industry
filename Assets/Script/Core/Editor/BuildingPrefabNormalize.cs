#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildingPrefabNormalize
{
    const string InPrefabPath = "Assets/prefabs/IO/IoArrow_In.prefab";
    const string OutPrefabPath = "Assets/prefabs/IO/IoArrow_Out.prefab";
    const string ObjPath = "Assets/models/Builders/io_arrow.obj";
    const string MeshAssetPath = "Assets/models/Builders/io_arrow_mesh.asset";
    const string InMatPath = "Assets/Materials/IoArrow_In.mat";
    const string OutMatPath = "Assets/Materials/IoArrow_Out.mat";

    [InitializeOnLoadMethod]
    static void BootstrapIoArrows()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (ArrowPrefabReady(InPrefabPath) && ArrowPrefabReady(OutPrefabPath))
                return;
            CreateIoArrowPrefabs();
        };
    }

    [MenuItem("Walk of Industry/Create IO Arrow Prefabs")]
    public static void CreateIoArrowPrefabs()
    {
        Mesh mesh = LoadOrCreateArrowMesh();
        Material inMat = AssetDatabase.LoadAssetAtPath<Material>(InMatPath);
        Material outMat = AssetDatabase.LoadAssetAtPath<Material>(OutMatPath);
        if (mesh == null)
        {
            Debug.LogError("IO arrow mesh missing. Expected " + ObjPath);
            return;
        }

        if (inMat == null || outMat == null)
        {
            Debug.LogError("IoArrow materials missing: " + InMatPath + " / " + OutMatPath);
            return;
        }

        DirectoryEnsure("Assets/prefabs/IO");
        WriteArrowPrefab(InPrefabPath, "IoArrow_In", mesh, inMat);
        WriteArrowPrefab(OutPrefabPath, "IoArrow_Out", mesh, outMat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("IO arrows: " + InPrefabPath + " and " + OutPrefabPath
            + ". Drag onto InputSocket/StartPoint (In) or OutputSocket/EndPoint (Out). Local rot 0, +Z = tip.");
    }

    static bool ArrowPrefabReady(string path)
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
            return false;
        MeshFilter filter = go.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null;
    }

    static void DirectoryEnsure(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string name = System.IO.Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }

    static Mesh LoadOrCreateArrowMesh()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ObjPath);
        if (model != null)
        {
            MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return filter.sharedMesh;
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
        if (mesh != null)
            return mesh;

        mesh = Object.Instantiate(BuiltInArrowMesh());
        mesh.name = "io_arrow";
        AssetDatabase.CreateAsset(mesh, MeshAssetPath);
        return mesh;
    }

    static Mesh BuiltInArrowMesh()
    {
        var verts = new Vector3[]
        {
            new Vector3(-0.09f, 0f, -0.04f),
            new Vector3(0.09f, 0f, -0.04f),
            new Vector3(0.09f, 0f, 0.20f),
            new Vector3(0.24f, 0f, 0.16f),
            new Vector3(0f, 0f, 0.56f),
            new Vector3(-0.24f, 0f, 0.16f),
            new Vector3(-0.09f, 0f, 0.20f),
            new Vector3(-0.09f, 0.09f, -0.04f),
            new Vector3(0.09f, 0.09f, -0.04f),
            new Vector3(0.09f, 0.09f, 0.20f),
            new Vector3(0.24f, 0.09f, 0.16f),
            new Vector3(0f, 0.09f, 0.56f),
            new Vector3(-0.24f, 0.09f, 0.16f),
            new Vector3(-0.09f, 0.09f, 0.20f)
        };
        var tris = new int[]
        {
            7, 9, 8, 7, 13, 9, 9, 11, 10, 13, 11, 9, 13, 12, 11,
            0, 1, 2, 0, 2, 6, 2, 3, 4, 6, 2, 4, 6, 4, 5,
            0, 7, 8, 0, 8, 1,
            1, 8, 9, 1, 9, 2,
            2, 9, 10, 2, 10, 3,
            3, 10, 11, 3, 11, 4,
            4, 11, 12, 4, 12, 5,
            5, 12, 13, 5, 13, 6,
            6, 13, 7, 6, 7, 0
        };
        var mesh = new Mesh { name = "io_arrow" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void WriteArrowPrefab(string path, string objectName, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject(objectName);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer rend = go.AddComponent<MeshRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (go.GetComponent<SocketArrow>() == null)
            go.AddComponent<SocketArrow>();
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    [MenuItem("Walk of Industry/Normalize Building Prefabs")]
    public static void NormalizeAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:BuildingData", new[] { "Assets/ScriptableObjects/Builders" });
        int n = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data == null || data.prefab == null)
                continue;
            if (NormalizePrefab(data))
                n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BuildingPrefabNormalize] Updated " + n + " prefabs. +Z = output, -Z = input. Arrows are not auto-added.");
    }

    static bool NormalizePrefab(BuildingData data)
    {
        string path = AssetDatabase.GetAssetPath(data.prefab);
        if (string.IsNullOrEmpty(path))
            return false;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BuildingBase building = root.GetComponent<BuildingBase>();
            if (building == null)
                return false;

            if (root.transform.localRotation != Quaternion.identity)
                root.transform.localRotation = Quaternion.identity;

            if (building is Conveyor)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }

            if (building.data == null)
                building.data = data;

            BuildingPrefabLayout.ApplyPrimarySockets(building);
            EnsureVisualTree(root.transform, building);
            EnsureGhost(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EnsureVisualTree(Transform root, BuildingBase building)
    {
        Transform visual = root.Find(BuildingPrefabLayout.Visual);
        if (visual == null)
        {
            GameObject go = new GameObject(BuildingPrefabLayout.Visual);
            visual = go.transform;
            visual.SetParent(root, false);
        }

        Transform level1 = BuildingPrefabLayout.FindLevel1(root);
        if (level1 == null)
        {
            GameObject go = new GameObject(BuildingPrefabLayout.Level1);
            level1 = go.transform;
            level1.SetParent(visual, false);
            ReparentModels(root, visual, level1);
        }
        else if (level1.parent != visual)
            level1.SetParent(visual, true);

        Transform level2 = BuildingPrefabLayout.FindLevel2(root);
        if (level2 != null && level2.parent != visual)
            level2.SetParent(visual, true);
    }

    static void ReparentModels(Transform root, Transform visual, Transform level1)
    {
        List<Transform> move = new List<Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == visual)
                continue;
            if (child.GetComponent<BuildingSocket>() != null)
                continue;
            if (child.GetComponentInChildren<BuildingSocket>() != null && child.GetComponent<Renderer>() == null)
                continue;
            if (child.name == BuildingPrefabLayout.Ghost || child.name == BuildingPrefabLayout.Sockets)
                continue;
            if (child.GetComponent<SocketArrow>() != null)
                continue;
            if (child.GetComponent<MeshFilter>() != null || child.GetComponentInChildren<MeshFilter>(true) != null
                || child.GetComponent<SkinnedMeshRenderer>() != null)
                move.Add(child);
        }

        for (int i = 0; i < move.Count; i++)
            move[i].SetParent(level1, true);
    }

    static void EnsureGhost(Transform root)
    {
        Transform ghost = BuildingPrefabLayout.FindGhost(root);
        Transform level1 = BuildingPrefabLayout.FindLevel1(root);
        if (ghost != null || level1 == null)
            return;

        GameObject clone = Object.Instantiate(level1.gameObject, root);
        clone.name = BuildingPrefabLayout.Ghost;
        clone.transform.localPosition = level1.localPosition;
        clone.transform.localRotation = level1.localRotation;
        clone.transform.localScale = level1.localScale;
        clone.SetActive(false);
    }
}
#endif
