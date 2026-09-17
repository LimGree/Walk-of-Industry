#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WorldRegenEditor
{
    [MenuItem("Walk of Industry/Regen Map (Keep Extractor Veins)")]
    public static void RegenMapKeepExtractorVeins()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Walk of Industry",
                "Запусти Play и зайди в мир. Команда работает только в активной игре.",
                "OK");
            return;
        }

        WorldBiomeMap map = WorldBiomeMap.Instance;
        WorldResourceScatterer scatter = WorldResourceScatterer.Instance;
        if (map == null || scatter == null)
        {
            EditorUtility.DisplayDialog(
                "Walk of Industry",
                "Нет WorldBiomeMap / WorldResourceScatterer на сцене.",
                "OK");
            return;
        }

        int oldSeed = map.seed;
        map.seed = Random.Range(1, 999999);
        map.Generate();
        scatter.ScatterPreservingExtractorVeins();
        Debug.Log("[Regen] Карта пересобрана. seed " + oldSeed + " → " + map.seed
            + ". Жилы под экстракторами оставлены.");
    }
}
#endif
