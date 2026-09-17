#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BuildingFootprintSceneOverlay
{
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawBuilding(BuildingBase building, GizmoType gizmoType)
    {
        if (building == null || !building.drawFootprintGizmo)
            return;

        Vector2Int size = building.data != null ? building.data.size : Vector2Int.one;
        size = GridFootprint.GetRotatedSize(size, building.transform.eulerAngles.y);

        GridFootprint.DrawFootprintGizmo(building.transform.position, size, new Color(0.15f, 0.9f, 1f, 1f));
        GridFootprint.DrawCellQuadsGizmo(
            building.transform.position,
            size,
            new Color(0.3f, 1f, 0.45f, 0.7f));
    }
}

[CustomEditor(typeof(BuildingBase), true)]
public class BuildingBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BuildingBase b = (BuildingBase)target;
        if (b.data != null)
        {
            Vector2Int size = GridFootprint.GetRotatedSize(b.data.size, b.transform.eulerAngles.y);
            float cell = GridFootprint.CellSize;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Footprint (center pivot)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cells", $"{size.x} × {size.y}");
            EditorGUILayout.LabelField("World size", $"{size.x * cell:0.###} × {size.y * cell:0.###}");
        }
    }
}
#endif
