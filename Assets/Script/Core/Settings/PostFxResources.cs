using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Ссылка на PostProcessResources из пакета com.unity.postprocessing.
/// Лежит в Resources, чтобы PostFx мог повесить PostProcessLayer из кода и в билде.
/// </summary>
public class PostFxResources : ScriptableObject
{
    public PostProcessResources resources;
}
