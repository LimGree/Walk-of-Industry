using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class UndoStep
{
    public string kind;
    public int coins;
    public BuildingSaveData[] before;
    public BuildingSaveData[] after;
}

[Serializable]
public class UndoFile
{
    public UndoStep[] steps;
    public UndoStep[] redo;
}

public static class BuildUndo
{
    public const int MaxSteps = 50;

    static readonly List<UndoStep> steps = new List<UndoStep>();
    static readonly List<UndoStep> redo = new List<UndoStep>();
    static readonly List<BuildingSaveData> batchBefore = new List<BuildingSaveData>();
    static readonly List<BuildingSaveData> batchAfter = new List<BuildingSaveData>();
    static int nest;
    static int batchCoins;
    static bool undoing;

    public static int Count => steps.Count;
    public static int RedoCount => redo.Count;

    public static void Load()
    {
        steps.Clear();
        redo.Clear();
        string path = PathFile();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;
        try
        {
            UndoFile file = JsonUtility.FromJson<UndoFile>(File.ReadAllText(path));
            if (file == null)
                return;
            AddCapped(steps, file.steps);
            AddCapped(redo, file.redo);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Undo] " + e.Message);
        }
    }

    static void AddCapped(List<UndoStep> dest, UndoStep[] source)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Length && dest.Count < MaxSteps; i++)
        {
            if (source[i] != null)
                dest.Add(source[i]);
        }
    }

    public static void Clear()
    {
        steps.Clear();
        redo.Clear();
        nest = 0;
        batchBefore.Clear();
        batchAfter.Clear();
        batchCoins = 0;
        Persist();
    }

    public static void Begin()
    {
        if (undoing)
            return;
        nest++;
        if (nest != 1)
            return;
        batchBefore.Clear();
        batchAfter.Clear();
        batchCoins = 0;
    }

    public static void End()
    {
        if (undoing || nest <= 0)
            return;
        nest--;
        if (nest != 0)
            return;
        if (batchBefore.Count == 0 && batchAfter.Count == 0)
            return;
        Push(new UndoStep
        {
            kind = KindOf(batchBefore.Count, batchAfter.Count),
            coins = batchCoins,
            before = batchBefore.Count > 0 ? batchBefore.ToArray() : null,
            after = batchAfter.Count > 0 ? batchAfter.ToArray() : null
        });
        batchBefore.Clear();
        batchAfter.Clear();
        batchCoins = 0;
    }

    public static void NoteRemoved(BuildingBase building)
    {
        if (undoing || building == null)
            return;
        BuildingSaveData cap = Capture(building);
        if (cap == null)
            return;
        int refund = Economy.RefundCoins(building.data);
        if (nest > 0)
        {
            batchBefore.Add(cap);
            batchCoins += refund;
            return;
        }

        Push(new UndoStep
        {
            kind = "remove",
            coins = refund,
            before = new[] { cap }
        });
    }

    public static void NotePlaced(BuildingBase building)
    {
        if (undoing || building == null)
            return;
        BuildingSaveData cap = Capture(building);
        if (cap == null)
            return;
        if (nest > 0)
        {
            batchAfter.Add(cap);
            return;
        }

        Push(new UndoStep
        {
            kind = "place",
            after = new[] { cap }
        });
    }

    public static void NoteCoins(int delta)
    {
        if (undoing || delta == 0)
            return;
        if (nest > 0)
            batchCoins += delta;
    }

    public static void NoteEdit(BuildingBase building, Vector3 oldPos, float oldYaw)
    {
        if (undoing || building == null)
            return;
        BuildingSaveData before = Capture(building);
        if (before == null)
            return;
        before.position = oldPos;
        before.rotationY = oldYaw;
        BuildingSaveData after = Capture(building);
        if (nest > 0)
        {
            batchBefore.Add(before);
            batchAfter.Add(after);
            return;
        }

        Push(new UndoStep
        {
            kind = "edit",
            before = new[] { before },
            after = new[] { after }
        });
    }

    public static bool Undo()
    {
        return Replay(steps, redo, reverse: true);
    }

    public static bool Redo()
    {
        return Replay(redo, steps, reverse: false);
    }

    static bool Replay(List<UndoStep> from, List<UndoStep> to, bool reverse)
    {
        if (undoing || nest > 0 || from.Count == 0)
            return false;
        if (KeybindStore.BlocksGameplayInput)
            return false;

        UndoStep step = from[from.Count - 1];
        from.RemoveAt(from.Count - 1);
        undoing = true;
        try
        {
            Apply(step, reverse);
            to.Add(step);
            while (to.Count > MaxSteps)
                to.RemoveAt(0);
            Persist();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Undo] " + e.Message);
            return false;
        }
        finally
        {
            undoing = false;
        }
    }

    static void Apply(UndoStep step, bool reverse)
    {
        if (step == null)
            return;

        BuildingSaveData[] from = reverse ? step.after : step.before;
        BuildingSaveData[] to = reverse ? step.before : step.after;
        ApplyDirected(from, to);

        int coins = reverse ? -step.coins : step.coins;
        if (coins != 0 && PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddCoins(coins);
    }

    static void ApplyDirected(BuildingSaveData[] from, BuildingSaveData[] to)
    {
        bool spawned = false;
        UndergroundConveyor.BeginLoad();
        if (from != null)
        {
            for (int i = 0; i < from.Length; i++)
            {
                BuildingSaveData src = from[i];
                BuildingBase live = FindAt(src);
                BuildingSaveData dest = to != null && i < to.Length ? to[i] : null;
                if (dest != null)
                {
                    if (live != null)
                    {
                        live.transform.SetPositionAndRotation(
                            dest.position,
                            Quaternion.Euler(0f, dest.rotationY, 0f));
                        live.ReRegisterOnGrid();
                        live.OnRotated();
                    }
                    else if (SaveSystem.RespawnFromSave(dest) != null)
                        spawned = true;
                }
                else if (live != null)
                {
                    live.OnRemoved();
                    UnityEngine.Object.Destroy(live.gameObject);
                }
            }
        }
        else if (to != null)
        {
            for (int i = 0; i < to.Length; i++)
            {
                if (SaveSystem.RespawnFromSave(to[i]) != null)
                    spawned = true;
            }
        }

        if (spawned)
            BuildingLinker.RelinkAll();
        UndergroundConveyor.FinishLoad();
    }

    static BuildingBase FindAt(BuildingSaveData save)
    {
        if (save == null)
            return null;
        Vector2Int cell = BuildingLinker.WorldToCell(save.position);
        BuildingBase b = BuildingLinker.GetBuildingAt(cell);
        if (b == null || b.data == null
            || !string.Equals(b.data.id, save.buildingId, StringComparison.OrdinalIgnoreCase))
            b = DecorSystem.FloorAt(cell);   // напольная декорация лежит под зданием
        if (b == null || b.data == null)
            return null;
        if (!string.Equals(b.data.id, save.buildingId, StringComparison.OrdinalIgnoreCase))
            return null;
        return b;
    }

    public static BuildingSaveData Capture(BuildingBase building)
    {
        if (building == null || building.data == null || string.IsNullOrEmpty(building.data.id))
            return null;
        var row = new BuildingSaveData
        {
            buildingId = building.data.id,
            position = building.transform.position,
            rotationY = building.transform.eulerAngles.y,
            level = building.ReadLevel()
        };
        building.WriteSave(row);
        return row;
    }

    static void Push(UndoStep step)
    {
        if (step == null)
            return;
        redo.Clear();
        steps.Add(step);
        while (steps.Count > MaxSteps)
            steps.RemoveAt(0);
        Persist();
    }

    static string KindOf(int before, int after)
    {
        if (before > 0 && after > 0)
            return "edit";
        if (after > 0)
            return "place";
        return "remove";
    }

    static void Persist()
    {
        string path = PathFile();
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var file = new UndoFile { steps = steps.ToArray(), redo = redo.ToArray() };
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Undo] " + e.Message);
        }
    }

    static string PathFile()
    {
        if (!WorldCatalog.HasActive)
            return null;
        return Path.Combine(WorldCatalog.WorldsFolder, WorldCatalog.Active.id, "undo.json");
    }
}
