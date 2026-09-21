using UnityEngine;

public enum BeltShape
{
    Straight,
    Corner,
    Tee,
    Sides,
    Triple
}

[System.Flags]
public enum BeltInMask
{
    None = 0,
    Back = 1,
    Left = 2,
    Right = 4
}

/// <summary>
/// Таблица форм ленты. Выход всегда +Z локально (transform.forward).
/// Путь груза задан здесь, не точками на префабе.
/// </summary>
public static class BeltRules
{
    public const float MergeT = 0.5f;

    public static bool Has(BeltInMask mask, BeltInMask bit)
    {
        return (mask & bit) != 0;
    }

    public static BeltInMask MaskFromFlags(bool back, bool left, bool right)
    {
        BeltInMask mask = BeltInMask.None;
        if (back) mask |= BeltInMask.Back;
        if (left) mask |= BeltInMask.Left;
        if (right) mask |= BeltInMask.Right;
        return mask;
    }

    public static BeltShape Shape(BeltInMask mask)
    {
        bool back = Has(mask, BeltInMask.Back);
        bool left = Has(mask, BeltInMask.Left);
        bool right = Has(mask, BeltInMask.Right);
        if (back && left && right)
            return BeltShape.Triple;
        if (left && right)
            return BeltShape.Sides;
        if (back && (left || right))
            return BeltShape.Tee;
        if (left || right)
            return BeltShape.Corner;
        return BeltShape.Straight;
    }

    /// <summary>
    /// Доворот и зеркало меша, чтобы выход модели совпал с +Z.
    /// Бока / тройник в ассетах смотрят выходом на −X (extraYaw 90).
    /// T (Conveyor_Tee / conveer_4_5): правый рукав extraYaw 270, левый 90.
    /// Меш не зеркалим — второй рукав берёт teeMirrorMaterial (Рисунок4_5 Mirror).
    /// </summary>
    public static void GetVisual(BeltShape shape, BeltInMask mask, out float extraYaw, out bool mirrorX)
    {
        extraYaw = 0f;
        mirrorX = false;
        switch (shape)
        {
            case BeltShape.Corner:
                mirrorX = Has(mask, BeltInMask.Left) && !Has(mask, BeltInMask.Right);
                break;
            case BeltShape.Tee:
                extraYaw = Has(mask, BeltInMask.Left) && !Has(mask, BeltInMask.Right) ? 90f : 270f;
                break;
            case BeltShape.Sides:
            case BeltShape.Triple:
                extraYaw = 90f;
                break;
        }
    }

    public static Vector2Int BackNeighbor(Vector2Int exit)
    {
        return new Vector2Int(-exit.x, -exit.y);
    }

    public static Vector2Int LeftNeighbor(Vector2Int exit)
    {
        return BuildingLinker.Rotate90(exit, 1);
    }

    public static Vector2Int RightNeighbor(Vector2Int exit)
    {
        return BuildingLinker.Rotate90(exit, -1);
    }

    public static Vector2Int NeighborDelta(Vector2Int exit, BeltInMask side)
    {
        if (side == BeltInMask.Left)
            return LeftNeighbor(exit);
        if (side == BeltInMask.Right)
            return RightNeighbor(exit);
        return BackNeighbor(exit);
    }

    /// <summary>Направление движения груза при входе с этой стороны.</summary>
    public static Vector2Int Travel(Vector2Int exit, BeltInMask side)
    {
        if (side == BeltInMask.Left)
            return BuildingLinker.Rotate90(exit, -1);
        if (side == BeltInMask.Right)
            return BuildingLinker.Rotate90(exit, 1);
        return exit;
    }

    public static BeltInMask SideFromTravel(Vector2Int exit, Vector2Int travel)
    {
        if (travel == Travel(exit, BeltInMask.Left))
            return BeltInMask.Left;
        if (travel == Travel(exit, BeltInMask.Right))
            return BeltInMask.Right;
        if (travel == exit)
            return BeltInMask.Back;
        return BeltInMask.None;
    }

    public static BeltInMask SideFromNeighbor(Vector2Int exit, Vector2Int neighborDelta)
    {
        if (neighborDelta == LeftNeighbor(exit))
            return BeltInMask.Left;
        if (neighborDelta == RightNeighbor(exit))
            return BeltInMask.Right;
        if (neighborDelta == BackNeighbor(exit))
            return BeltInMask.Back;
        return BeltInMask.None;
    }

    /// <summary>
    /// Локальная точка пути. +Z = выход, +X = право, начало координат — центр клетки.
    /// Прямая: зад → перед. Поворот/слияние: край → центр (t=0.5) → перед.
    /// </summary>
    public static Vector3 PathLocal(BeltInMask entry, float t, float half, float height)
    {
        t = Mathf.Clamp01(t);
        Vector3 start = EntryLocal(entry, half, height);
        Vector3 end = new Vector3(0f, height, half);
        if (entry == BeltInMask.None || entry == BeltInMask.Back)
            return Vector3.Lerp(start, end, t);

        Vector3 mid = new Vector3(0f, height, 0f);
        if (t < MergeT)
            return Vector3.Lerp(start, mid, t / MergeT);
        return Vector3.Lerp(mid, end, (t - MergeT) / (1f - MergeT));
    }

    public static Vector3 PathWorld(Transform belt, BeltInMask entry, float t, float height)
    {
        float half = GridFootprint.CellSize * 0.5f;
        return belt.TransformPoint(PathLocal(entry, t, half, height));
    }

    static Vector3 EntryLocal(BeltInMask entry, float half, float height)
    {
        if (entry == BeltInMask.Left)
            return new Vector3(-half, height, 0f);
        if (entry == BeltInMask.Right)
            return new Vector3(half, height, 0f);
        return new Vector3(0f, height, -half);
    }
}
