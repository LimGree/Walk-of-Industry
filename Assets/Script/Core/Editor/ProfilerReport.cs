#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Текстовая сводка по захвату профайлера (.data): время кадра, самые тяжёлые функции главного
/// потока и потока рендера, вызовы отрисовки, мусор памяти, разбор самых медленных кадров.
/// Меню: Walk of Industry → Profiler report from .data. Результат — ProfilerReport.txt в корне проекта.
/// </summary>
public static class ProfilerReport
{
    static readonly string[] Counters =
    {
        "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count",
        "Shadow Casters Count", "Visible Skinned Meshes Count", "Total Batches Count",
        "Dynamic Batches Count", "Static Batches Count", "Instanced Batches Count",
        "Instanced Batched Draw Calls Count", "GC Allocated In Frame", "GC Allocation In Frame Count",
        "GC Used Memory", "Total Used Memory", "Game Object Count", "Object Count",
        "System Used Memory", "Gfx Used Memory"
    };

    class Agg
    {
        public double self, total, gc;
        public long calls;
        public int frames;
    }

    [MenuItem("Walk of Industry/Profiler report from .data")]
    static void Run()
    {
        string path = EditorUtility.OpenFilePanel("Захват профайлера", System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "data");
        if (string.IsNullOrEmpty(path))
            return;
        Build(path);
    }

    /// <summary>Unity -batchmode -executeMethod ProfilerReport.FromCommandLine -profilerData "x.data" -reportOut "y.txt"</summary>
    public static void FromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string data = null, output = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-profilerData")
                data = args[i + 1];
            else if (args[i] == "-reportOut")
                output = args[i + 1];
        }

        if (string.IsNullOrEmpty(data))
        {
            Debug.LogError("[ProfilerReport] нужен -profilerData <файл>");
            return;
        }

        Build(data, output);
    }

    public static void Build(string path, string output = null)
    {
        var inv = CultureInfo.InvariantCulture;
        EditorUtility.DisplayProgressBar("Profiler report", "Загрузка " + Path.GetFileName(path), 0f);
        try
        {
            if (!ProfilerDriver.LoadProfile(path, false))
            {
                Debug.LogError("[ProfilerReport] не удалось загрузить " + path);
                return;
            }

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            int count = last - first + 1;
            if (count <= 0)
            {
                Debug.LogError("[ProfilerReport] в файле нет кадров");
                return;
            }

            var frameMs = new List<(int frame, float ms)>(count);
            var main = new Dictionary<string, Agg>();
            var render = new Dictionary<string, Agg>();
            var counters = new Dictionary<string, List<double>>();
            int renderThread = -1;
            string renderName = null;

            for (int f = first; f <= last; f++)
            {
                if ((f - first) % 25 == 0
                    && EditorUtility.DisplayCancelableProgressBar("Profiler report", "Кадр " + (f - first) + " / " + count, (f - first) / (float)count))
                    break;

                using (RawFrameDataView raw = ProfilerDriver.GetRawFrameDataView(f, 0))
                {
                    if (raw == null || !raw.valid)
                        continue;
                    frameMs.Add((f, raw.frameTimeMs));
                    for (int c = 0; c < Counters.Length; c++)
                    {
                        int id = raw.GetMarkerId(Counters[c]);
                        if (id == FrameDataView.invalidMarkerId || !raw.HasCounterValue(id))
                            continue;
                        if (!counters.TryGetValue(Counters[c], out List<double> list))
                            counters[Counters[c]] = list = new List<double>(count);
                        list.Add(raw.GetCounterValueAsDouble(id));
                    }
                }

                Collect(f, 0, main);

                if (renderThread < 0)
                    renderThread = FindThread(f, "Render Thread", out renderName);
                if (renderThread > 0)
                    Collect(f, renderThread, render);
            }

            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Directory.GetCurrentDirectory(), "ProfilerReport.txt");
            File.WriteAllText(output,
                Format(path, frameMs, main, render, renderName, counters, inv), Encoding.UTF8);
            Debug.Log("[ProfilerReport] готово: ProfilerReport.txt (" + frameMs.Count + " кадров)");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static int FindThread(int frame, string name, out string found)
    {
        found = null;
        for (int t = 1; t < 64; t++)
        {
            using (RawFrameDataView v = ProfilerDriver.GetRawFrameDataView(frame, t))
            {
                if (v == null || !v.valid)
                    return -1;
                if (v.threadName == name)
                {
                    found = v.threadName;
                    return t;
                }
            }
        }

        return -1;
    }

    static readonly List<int> Kids = new List<int>(64);

    static void Collect(int frame, int thread, Dictionary<string, Agg> into)
    {
        using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, thread,
                   HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
        {
            if (view == null || !view.valid)
                return;
            var seen = new HashSet<string>();
            var stack = new Stack<int>();
            stack.Push(view.GetRootItemID());
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                Kids.Clear();
                view.GetItemChildren(id, Kids);
                for (int i = 0; i < Kids.Count; i++)
                {
                    int k = Kids[i];
                    string name = view.GetItemName(k);
                    if (!into.TryGetValue(name, out Agg a))
                        into[name] = a = new Agg();
                    a.self += view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnSelfTime);
                    // total по имени считаем один раз за кадр (верхнее вхождение), иначе вложенные дублируются
                    if (seen.Add(name))
                    {
                        a.total += view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnTotalTime);
                        a.frames++;
                    }
                    a.calls += (long)view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnCalls);
                    a.gc += view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnGcMemory);
                    stack.Push(k);
                }
            }
        }
    }

    static string Format(string path, List<(int frame, float ms)> frames, Dictionary<string, Agg> main,
        Dictionary<string, Agg> render, string renderName, Dictionary<string, List<double>> counters, CultureInfo inv)
    {
        var sb = new StringBuilder();
        int n = Mathf.Max(1, frames.Count);
        var ms = frames.Select(f => f.ms).OrderBy(x => x).ToList();
        float P(float q) => ms.Count == 0 ? 0f : ms[Mathf.Clamp(Mathf.RoundToInt(q * (ms.Count - 1)), 0, ms.Count - 1)];
        sb.AppendLine("Файл: " + path);
        sb.AppendLine("Кадров: " + frames.Count);
        if (ms.Count > 0)
        {
            float avg = ms.Average();
            sb.AppendLine(string.Format(inv, "Кадр, мс: среднее {0:0.0} (≈{1:0} fps) · медиана {2:0.0} · 95% {3:0.0} · 99% {4:0.0} · макс {5:0.0}",
                avg, 1000f / Mathf.Max(0.01f, avg), P(0.5f), P(0.95f), P(0.99f), ms[ms.Count - 1]));
        }

        sb.AppendLine();
        sb.AppendLine("== Счётчики (среднее / макс)");
        foreach (var pair in counters.OrderBy(p => p.Key))
        {
            if (pair.Value.Count == 0)
                continue;
            sb.AppendLine(string.Format(inv, "{0,-36} {1,14:N0} {2,14:N0}", pair.Key, pair.Value.Average(), pair.Value.Max()));
        }

        Table(sb, "Главный поток — по собственному времени (мс за кадр)", main, n, a => a.self, inv, 45);
        Table(sb, "Главный поток — по полному времени (мс за кадр)", main, n, a => a.total, inv, 30);
        Table(sb, "Главный поток — мусор памяти (КБ за кадр)", main, n, a => a.gc / 1024.0, inv, 20);
        if (render.Count > 0)
            Table(sb, (renderName ?? "Render") + " — по собственному времени (мс за кадр)", render, n, a => a.self, inv, 25);

        sb.AppendLine();
        sb.AppendLine("== Самые медленные кадры");
        foreach (var f in frames.OrderByDescending(x => x.ms).Take(8))
        {
            sb.AppendLine(string.Format(inv, "-- кадр {0}: {1:0.0} мс", f.frame, f.ms));
            var one = new Dictionary<string, Agg>();
            Collect(f.frame, 0, one);
            foreach (var pair in one.OrderByDescending(p => p.Value.self).Take(10))
                sb.AppendLine(string.Format(inv, "   {0,8:0.00} мс self  {1,6} выз.  {2}", pair.Value.self, pair.Value.calls, pair.Key));
        }

        return sb.ToString();
    }

    static void Table(StringBuilder sb, string title, Dictionary<string, Agg> data, int frames,
        System.Func<Agg, double> key, CultureInfo inv, int top)
    {
        sb.AppendLine();
        sb.AppendLine("== " + title);
        sb.AppendLine("   мс/кадр   выз/кадр    ГК КБ/кадр  имя");
        foreach (var pair in data.OrderByDescending(p => key(p.Value)).Take(top))
        {
            Agg a = pair.Value;
            sb.AppendLine(string.Format(inv, "{0,9:0.000} {1,10:0.0} {2,12:0.0}  {3}",
                key(a) / frames, a.calls / (double)frames, a.gc / 1024.0 / frames, pair.Key));
        }
    }
}
#endif
