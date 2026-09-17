using UnityEngine;

[CreateAssetMenu(fileName = "ResourceNodePrefabs", menuName = "Walk of Industry/Resource Node Prefabs")]
public class ResourceNodePrefabs : ScriptableObject
{
    public GameObject sand;
    public GameObject stone;
    public GameObject coal;
    public GameObject copper;
    public GameObject iron;
    public GameObject sulfur;
    public GameObject tree;

    public GameObject Get(string name)
    {
        switch (name)
        {
            case "sand": return sand;
            case "stone": return stone;
            case "stone_coal_ore": return coal;
            case "stone_cooper_ore": return copper;
            case "stone_iron_ore": return iron;
            case "sulfur": return sulfur;
            case "tree": return tree;
            default: return null;
        }
    }
}
