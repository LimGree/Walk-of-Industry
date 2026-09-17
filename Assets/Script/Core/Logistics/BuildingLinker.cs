using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Связи по сетке. Ленты: форма по маске входов (кто выходом кормит клетку).
/// Станки: сокеты. Направление сокета = world forward.
/// </summary>
public static class BuildingLinker
{
    public const int WorklistMaxIterations = 64;

    static readonly Vector2Int[] Cardinals =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    static readonly Vector2Int[] Moore =
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        new Vector2Int(-1, 0),                         new Vector2Int(1, 0),
        new Vector2Int(-1, 1),  new Vector2Int(0, 1),  new Vector2Int(1, 1)
    };

    static readonly List<Vector2Int> CellBuffer = new List<Vector2Int>(16);
    static readonly List<BuildingBase> NeighborBuffer = new List<BuildingBase>(16);
    static readonly List<BuildingBase> AllBuffer = new List<BuildingBase>(256);
    static readonly List<GameObject> OccupantBuffer = new List<GameObject>(256);
    static readonly List<Vector2Int> SeedBuffer = new List<Vector2Int>(32);
    static readonly HashSet<Vector2Int> SeedSet = new HashSet<Vector2Int>();

    public static bool SuppressRelink;

    public static Vector2Int ToCardinal(Vector3 worldDir)
    {
        if (Mathf.Abs(worldDir.x) >= Mathf.Abs(worldDir.z))
            return new Vector2Int(worldDir.x >= 0f ? 1 : -1, 0);
        return new Vector2Int(0, worldDir.z >= 0f ? 1 : -1);
    }

    public static Vector3 CardinalToWorld(Vector2Int dir)
    {
        return new Vector3(dir.x, 0f, dir.y);
    }

    public static Vector2Int Rotate90(Vector2Int dir, int sign)
    {
        if (sign >= 0)
            return new Vector2Int(-dir.y, dir.x);
        return new Vector2Int(dir.y, -dir.x);
    }

    public static Vector2Int WorldToCell(Vector3 world)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.WorldToCell(world);
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }

    public static Vector2Int SocketWorldCardinal(BuildingSocket socket)
    {
        if (socket == null)
            return Vector2Int.zero;
        return ToCardinal(socket.GetOutward());
    }

    public static BuildingBase GetBuildingAt(Vector2Int cell)
    {
        GameObject obj = GridOccupancy.GetAt(cell);
        return obj != null ? obj.GetComponent<BuildingBase>() : null;
    }

    public static Vector2Int GetSocketFrontCell(BuildingSocket socket)
    {
        if (socket == null)
            return Vector2Int.zero;

        float cell = GridFootprint.CellSize;
        Vector3 outward = socket.GetOutward();
        Vector3 probe = socket.transform.position + outward * (cell * 0.55f);
        return WorldToCell(probe);
    }

    public static bool FeedsInto(BuildingBase candidate, Vector2Int targetCell)
    {
        if (candidate == null)
            return false;

        Conveyor belt = candidate as Conveyor;
        if (belt != null)
            return OccupiesCell(candidate, targetCell - belt.ExitDir);

        if (candidate.outputSockets == null)
            return false;

        for (int i = 0; i < candidate.outputSockets.Length; i++)
        {
            BuildingSocket output = candidate.outputSockets[i];
            if (output == null)
                continue;
            Vector2Int dir = SocketWorldCardinal(output);
            if (dir.x == 0 && dir.y == 0)
                continue;
            if (OccupiesCell(candidate, targetCell - dir))
                return true;
        }

        return false;
    }

    public static bool AcceptsFromCell(BuildingBase target, Vector2Int fromCell)
    {
        if (target == null)
            return false;

        Conveyor belt = target as Conveyor;
        if (belt != null)
            return belt.AcceptsFromCell(fromCell);

        if (target.inputSockets == null)
            return false;

        for (int i = 0; i < target.inputSockets.Length; i++)
        {
            BuildingSocket input = target.inputSockets[i];
            if (input == null)
                continue;
            Vector2Int inward = SocketWorldCardinal(input);
            if (inward.x == 0 && inward.y == 0)
                continue;
            if (OccupiesCell(target, fromCell + inward))
                return true;
        }

        return false;
    }

    public static bool HasOutputToward(BuildingBase building, Vector2Int targetCell)
    {
        return FeedsInto(building, targetCell);
    }

    public static void RelinkAround(BuildingBase building)
    {
        if (SuppressRelink || building == null)
            return;
        SeedSet.Clear();
        SeedBuffer.Clear();
        SeedFromBuilding(building, includeSelf: true);
        RunWorklist(SeedBuffer);
    }

    public static void RefreshRemoved(IList<Vector2Int> aroundCells)
    {
        if (SuppressRelink || aroundCells == null || aroundCells.Count == 0)
            return;
        SeedSet.Clear();
        SeedBuffer.Clear();
        for (int i = 0; i < aroundCells.Count; i++)
            AddMoore(aroundCells[i]);
        RunWorklist(SeedBuffer);
    }

    public static void CollectPlaced(List<BuildingBase> results)
    {
        if (results == null)
            return;
        results.Clear();
        GridOccupancy.CollectOccupants(OccupantBuffer);
        for (int i = 0; i < OccupantBuffer.Count; i++)
        {
            GameObject go = OccupantBuffer[i];
            if (go == null)
                continue;
            BuildingBase building = go.GetComponent<BuildingBase>();
            if (building != null)
                results.Add(building);
        }
    }

    public static void RelinkAll()
    {
        CollectPlaced(AllBuffer);
        var belts = new List<Conveyor>(AllBuffer.Count);
        var snapshot = new List<Conveyor.Incoming>(AllBuffer.Count);
        for (int i = 0; i < AllBuffer.Count; i++)
        {
            Conveyor belt = AllBuffer[i] as Conveyor;
            if (belt == null)
                continue;
            belts.Add(belt);
            snapshot.Add(belt.ComputeIncoming());
        }

        for (int i = 0; i < belts.Count; i++)
            belts[i].ApplyIncoming(snapshot[i]);

        for (int i = 0; i < AllBuffer.Count; i++)
            RelinkOutputs(AllBuffer[i]);
    }

    public static void Relink(List<BuildingBase> buildings)
    {
        if (SuppressRelink || buildings == null)
            return;
        SeedSet.Clear();
        SeedBuffer.Clear();
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null)
                SeedFromBuilding(buildings[i], includeSelf: true);
        }
        RunWorklist(SeedBuffer);
    }

    public static void CollectNeighbors(BuildingBase building, List<BuildingBase> results)
    {
        if (results == null)
            return;
        results.Clear();
        if (building == null)
            return;

        SeedSet.Clear();
        SeedBuffer.Clear();
        SeedFromBuilding(building, includeSelf: false);
        for (int i = 0; i < SeedBuffer.Count; i++)
        {
            BuildingBase other = GetBuildingAt(SeedBuffer[i]);
            if (other == null || other == building)
                continue;
            if (!results.Contains(other))
                results.Add(other);
        }
    }

    public static void CollectFootprintCells(BuildingBase building, List<Vector2Int> results)
    {
        if (results == null)
            return;
        results.Clear();
        if (building == null)
            return;
        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, results);
    }

    static void SeedFromBuilding(BuildingBase building, bool includeSelf)
    {
        if (building == null)
            return;
        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
        for (int i = 0; i < CellBuffer.Count; i++)
        {
            if (includeSelf)
                AddSeed(CellBuffer[i]);
            AddMoore(CellBuffer[i]);
        }
    }

    static void AddMoore(Vector2Int cell)
    {
        for (int i = 0; i < Moore.Length; i++)
            AddSeed(cell + Moore[i]);
    }

    static void AddSeed(Vector2Int cell)
    {
        if (SeedSet.Add(cell))
            SeedBuffer.Add(cell);
    }

    struct ShapeChange
    {
        public Conveyor belt;
        public Conveyor.Incoming incoming;
        public bool changed;
    }

    static void RunWorklist(List<Vector2Int> seeds)
    {
        if (seeds == null || seeds.Count == 0)
            return;

        var queue = new List<Vector2Int>(seeds);
        var visited = new HashSet<Vector2Int>();
        var touched = new HashSet<Vector2Int>();
        var buffer = new List<ShapeChange>(16);
        int iterations = 0;

        while (queue.Count > 0 && iterations < WorklistMaxIterations)
        {
            iterations++;
            buffer.Clear();
            int round = queue.Count;
            for (int i = 0; i < round; i++)
            {
                Vector2Int cell = queue[i];
                if (visited.Contains(cell))
                    continue;
                touched.Add(cell);
                Conveyor belt = GetBuildingAt(cell) as Conveyor;
                if (belt == null)
                    continue;
                Conveyor.Incoming incoming = belt.ComputeIncoming();
                buffer.Add(new ShapeChange
                {
                    belt = belt,
                    incoming = incoming,
                    changed = incoming.mask != belt.InMask
                });
            }

            queue.RemoveRange(0, round);

            for (int i = 0; i < buffer.Count; i++)
            {
                ShapeChange change = buffer[i];
                if (change.belt == null)
                    continue;
                Vector2Int cell = change.belt.Cell;
                visited.Add(cell);
                if (!change.changed)
                    continue;
                change.belt.ApplyIncoming(change.incoming);
                for (int m = 0; m < Moore.Length; m++)
                {
                    Vector2Int n = cell + Moore[m];
                    if (!visited.Contains(n))
                        queue.Add(n);
                }
            }
        }

        var seenBuildings = new HashSet<int>();
        foreach (Vector2Int cell in touched)
        {
            BuildingBase building = GetBuildingAt(cell);
            if (building == null)
                continue;
            int id = building.GetInstanceID();
            if (!seenBuildings.Add(id))
                continue;
            RelinkOutputs(building);
        }
    }

    static void RelinkOutputs(BuildingBase building)
    {
        if (building == null || building is Conveyor || building.outputSockets == null)
            return;

        for (int i = 0; i < building.outputSockets.Length; i++)
        {
            BuildingSocket output = building.outputSockets[i];
            if (output == null)
                continue;

            output.DisconnectSocket();

            BuildingBase target = FindOutputTarget(building, output);
            if (target == null || target is Conveyor)
                continue;

            BuildingSocket input = FindAcceptingInput(target, building, output);
            if (input != null)
                output.ConnectSocket(input);
        }
    }

    static BuildingBase FindOutputTarget(BuildingBase building, BuildingSocket output)
    {
        BuildingBase front = GetBuildingAt(GetSocketFrontCell(output));
        if (front != null && front != building && !(front is RoboticArm))
            return front;

        Vector2Int outward = SocketWorldCardinal(output);
        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
        for (int i = 0; i < CellBuffer.Count; i++)
        {
            BuildingBase other = GetBuildingAt(CellBuffer[i] + outward);
            if (other != null && other != building && !(other is RoboticArm))
                return other;
        }

        return null;
    }

    static BuildingSocket FindAcceptingInput(BuildingBase target, BuildingBase from, BuildingSocket fromOutput)
    {
        if (target is Conveyor)
            return null;

        Splitter splitter = target as Splitter;
        if (splitter != null)
        {
            if (!splitter.IsFedBy(from))
                return null;
            return splitter.InputSocket;
        }

        if (target.inputSockets == null)
            return null;

        Vector2Int fromCell = from is Conveyor conv ? conv.Cell : WorldToCell(from.transform.position);
        for (int i = 0; i < target.inputSockets.Length; i++)
        {
            BuildingSocket input = target.inputSockets[i];
            if (input == null)
                continue;
            if (input.connectedSocket != null && input.connectedSocket != fromOutput)
                continue;
            Vector2Int inward = SocketWorldCardinal(input);
            if (inward.x == 0 && inward.y == 0)
                continue;
            if (OccupiesCell(target, fromCell + inward))
                return input;
        }

        return null;
    }

    public static bool HasInputFrom(BuildingBase target, BuildingBase source)
    {
        if (target == null || source == null || target == source)
            return false;

        Conveyor belt = target as Conveyor;
        if (belt != null)
            return belt.IsFedBy(source);

        Splitter splitter = target as Splitter;
        if (splitter != null)
            return splitter.IsFedBy(source);

        Vector2Int fromCell = source is Conveyor conv ? conv.Cell : WorldToCell(source.transform.position);
        return AcceptsFromCell(target, fromCell);
    }

    public static bool OccupiesCell(BuildingBase building, Vector2Int cell)
    {
        if (building == null)
            return false;

        var cells = new List<Vector2Int>(8);
        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, cells);
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == cell)
                return true;
        }

        return false;
    }

    public static bool IsAdjacentTo(BuildingBase building, Vector2Int cell)
    {
        if (building == null)
            return false;

        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
        for (int i = 0; i < CellBuffer.Count; i++)
        {
            Vector2Int delta = cell - CellBuffer[i];
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                return true;
        }

        return false;
    }

    public static float YawFromExitDir(Vector2Int exitDir)
    {
        if (exitDir.x == 0 && exitDir.y == 0)
            exitDir = new Vector2Int(0, 1);
        return Quaternion.LookRotation(CardinalToWorld(exitDir), Vector3.up).eulerAngles.y;
    }

    public static Vector2Int ExitDirFromYaw(float yaw)
    {
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        return ToCardinal(forward);
    }

    public static float MaybeSmartYaw(Vector2Int cell, float strokeYaw, HashSet<Vector2Int> strokeCells)
    {
        Vector2Int exitDir = ExitDirFromYaw(strokeYaw);
        Vector2Int left = Rotate90(exitDir, 1);
        Vector2Int right = Rotate90(exitDir, -1);
        bool leftOk = SmartYawCandidateValid(cell, left, strokeCells);
        bool rightOk = SmartYawCandidateValid(cell, right, strokeCells);
        if (leftOk == rightOk)
            return strokeYaw;
        return YawFromExitDir(leftOk ? left : right);
    }

    static bool SmartYawCandidateValid(Vector2Int cell, Vector2Int candidateExit, HashSet<Vector2Int> strokeCells)
    {
        Vector2Int front = cell + candidateExit;
        if (strokeCells != null && strokeCells.Contains(front))
            return false;
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsOcean(front))
            return false;

        BuildingBase target = GetBuildingAt(front);
        if (target == null)
            return false;

        if (!AcceptsFromCell(target, cell))
            return false;

        if (FeedsInto(target, cell))
            return false;

        return true;
    }
}
