using UnityEngine;

[CreateAssetMenu(fileName = "GameHudIcons", menuName = "Walk of Industry/HUD Icons")]
public class GameHudIcons : ScriptableObject
{
    public Sprite coin;
    public Sprite ruby;

    static GameHudIcons cached;

    public static GameHudIcons Current
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<GameHudIcons>("GameHudIcons");
            return cached;
        }
    }

    public static Sprite Coin => Current != null ? Current.coin : null;
    public static Sprite Ruby => Current != null ? Current.ruby : null;
}
