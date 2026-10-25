#if PERFORMANCE_BENCHMARK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    /// <summary>
    /// Stopwatches around the ECS game loop: every pipeline category tick (all its systems, including the
    /// reactive flush after each system), summed per rendered frame. Off unless the player is started with
    /// <c>-ecsLoopProfile &lt;file.csv&gt;</c>; then it costs two timestamps per category tick.
    /// Optional: <c>-ecsLoopProfileWarmup &lt;frames&gt;</c> (default 300) frames excluded from the summary,
    /// <c>-ecsLoopProfileFrom &lt;mark&gt;</c> the warmup counts from the first frame of that mark instead of the
    /// first recorded frame, <c>-ecsLoopProfileFrames &lt;frames&gt;</c> measured frames after which the player quits.
    /// Writes one CSV row per frame and <c>&lt;file&gt;.summary.txt</c> with median, p95, p99 and mean.
    /// Game code can add per-frame CSV columns (<see cref="AddColumn"/>) and named marks (<see cref="Mark"/>).
    /// With <see cref="OnSystemTicked"/> attached to the pipeline, every system gets its own per-frame column
    /// (gating check, Tick and the reactive flush at its Unlock), and game code can time named sections
    /// (<see cref="EndSection"/>); both go to <c>&lt;file&gt;.systems.csv</c> and a table in the summary.
    /// </summary>
    public static class EcsLoopProfiler
    {
        private const int CategoryCount = 8;
        private static readonly string[] CategoryNames =
            { "Init", "Update", "LateUpdate", "FixedUpdate", "LateFixedUpdate", "OnEnable", "OnDisable", "Reactive" };
        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        public static bool Enabled { get; private set; }

        private struct Frame
        {
            public int Number;
            public float UnscaledDeltaMs;
            public int FixedTicks;
            // This frame's samples of the extra columns in ColumnValues.
            public int ValuesStart, ValuesCount;
            // This frame's per-system and per-section Stopwatch ticks in SlotTicks.
            public int SlotsStart, SlotsCount;
            public long Category0, Category1, Category2, Category3, Category4, Category5, Category6, Category7;

            public long this[int category]
            {
                get => category switch
                {
                    0 => Category0, 1 => Category1, 2 => Category2, 3 => Category3,
                    4 => Category4, 5 => Category5, 6 => Category6, _ => Category7,
                };
                set
                {
                    switch (category)
                    {
                        case 0: Category0 = value; break;
                        case 1: Category1 = value; break;
                        case 2: Category2 = value; break;
                        case 3: Category3 = value; break;
                        case 4: Category4 = value; break;
                        case 5: Category5 = value; break;
                        case 6: Category6 = value; break;
                        default: Category7 = value; break;
                    }
                }
            }
        }

        private static string _path;
        private static int _warmup = 300;
        private static int _measuredFramesToQuit;
        private static readonly List<Frame> Frames = new List<Frame>(1 << 16);
        private static Frame _current;
        private static int _currentFrame = -1;
        private static bool _written;
        private static readonly List<(string Name, Func<double> Sample)> Columns = new();
        private static readonly List<double> ColumnValues = new(1 << 17);
        private static readonly List<(string Name, int Frame, int Row)> Marks = new();
        // Per-system and per-section columns: one slot per column per recorded frame, Stopwatch ticks. A frame
        // gets a slot for every column known at its roll-over; a column first seen later extends that frame.
        private static readonly List<string> SlotNames = new();
        private static readonly Dictionary<EcsSystem, int> SystemSlots = new();
        private static readonly Dictionary<string, int> SectionSlots = new();
        private static readonly List<long> SlotTicks = new(1 << 22);
        private static string _warmupFrom;
        // First row the warmup counts from; -1 until the -ecsLoopProfileFrom mark happens.
        private static int _measureStartRow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            _path = ArgumentAfter(args, "-ecsLoopProfile");
            Enabled = !string.IsNullOrEmpty(_path);
            if (!Enabled)
                return;
            if (int.TryParse(ArgumentAfter(args, "-ecsLoopProfileWarmup"), out var warmup))
                _warmup = Math.Max(0, warmup);
            if (int.TryParse(ArgumentAfter(args, "-ecsLoopProfileFrames"), out var frames))
                _measuredFramesToQuit = Math.Max(0, frames);
            _warmupFrom = ArgumentAfter(args, "-ecsLoopProfileFrom");
            _measureStartRow = string.IsNullOrEmpty(_warmupFrom) ? 0 : -1;
            Frames.Clear();
            Columns.Clear();
            ColumnValues.Clear();
            Marks.Clear();
            SlotNames.Clear();
            SystemSlots.Clear();
            SectionSlots.Clear();
            SlotTicks.Clear();
            _currentFrame = -1;
            _written = false;
            Application.quitting += Write;
            UnityEngine.Debug.Log($"[EcsLoopProfiler] recording to {_path}, warmup {_warmup} frames" +
                                  (_measureStartRow < 0 ? $" from mark '{_warmupFrom}'" : "") +
                                  (_measuredFramesToQuit > 0 ? $", quitting after {_measuredFramesToQuit} measured frames" : ""));
        }

        /// <summary>
        /// Adds a CSV column sampled once per recorded frame, when the frame's first category tick ends; NaN
        /// leaves the cell empty. The summary reports the column over the measured frames. No effect when off.
        /// </summary>
        public static void AddColumn(string name, Func<double> sample)
        {
            if (Enabled && sample != null)
                Columns.Add((name, sample));
        }

        /// <summary>
        /// Names the current frame, e.g. "round-start": the summary lists marks, and
        /// <c>-ecsLoopProfileFrom &lt;name&gt;</c> counts the warmup from the first one. No effect when off.
        /// </summary>
        public static void Mark(string name)
        {
            if (!Enabled)
                return;
            var frame = Time.frameCount;
            // The row of this frame: the current one if this frame already ticked, else the next one.
            var row = _currentFrame < 0 ? 0 : _currentFrame >= frame ? Frames.Count : Frames.Count + 1;
            Marks.Add((name, frame, row));
            if (_measureStartRow < 0 && string.Equals(name, _warmupFrom, StringComparison.OrdinalIgnoreCase))
                _measureStartRow = row;
            UnityEngine.Debug.Log($"[EcsLoopProfiler] mark '{name}' at frame {frame} (row {row})");
        }

        private static string ArgumentAfter(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        public static long Begin() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Attach to <c>EcsPipeline.SystemTicked</c>: one column per system instance, named
        /// <c>Category.SystemType</c>, summed per rendered frame like the categories.
        /// </summary>
        public static void OnSystemTicked(ESystemCategory category, EcsSystem system, long elapsedTicks)
        {
            if (!SystemSlots.TryGetValue(system, out var slot))
            {
                slot = SlotNames.Count;
                SlotNames.Add(category + "." + system.GetType().Name);
                SystemSlots.Add(system, slot);
            }
            Accumulate(slot, elapsedTicks);
        }

        /// <summary>
        /// Ends a section begun with <see cref="Begin"/>: a named column (<c>section.name</c>) summed per
        /// rendered frame. Call only when <see cref="Enabled"/>.
        /// </summary>
        public static void EndSection(string name, long startTimestamp)
        {
            var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            if (!SectionSlots.TryGetValue(name, out var slot))
            {
                slot = SlotNames.Count;
                SlotNames.Add("section." + name);
                SectionSlots.Add(name, slot);
            }
            Accumulate(slot, elapsed);
        }

        private static void Accumulate(int slot, long ticks)
        {
            var frame = Time.frameCount;
            if (frame != _currentFrame)
                RollOver(frame);
            // The current frame's slots end the list, so a column first seen in this frame extends them.
            while (_current.SlotsCount <= slot)
            {
                SlotTicks.Add(0);
                _current.SlotsCount++;
            }
            SlotTicks[_current.SlotsStart + slot] += ticks;
        }

        public static void End(ESystemCategory category, long startTimestamp)
        {
            var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            var frame = Time.frameCount;
            if (frame != _currentFrame)
                RollOver(frame);
            var index = CategoryIndex(category);
            _current[index] += elapsed;
            if (category == ESystemCategory.FixedUpdate)
                _current.FixedTicks++;
        }

        private static int CategoryIndex(ESystemCategory category)
        {
            var value = (int)category;
            var index = 0;
            while (value > 1)
            {
                value >>= 1;
                index++;
            }
            return Math.Min(index, CategoryCount - 1);
        }

        private static void RollOver(int frame)
        {
            if (_currentFrame >= 0)
                Frames.Add(_current);
            _current = new Frame
            {
                Number = frame,
                UnscaledDeltaMs = Time.unscaledDeltaTime * 1000f,
                ValuesStart = ColumnValues.Count,
                ValuesCount = Columns.Count,
                SlotsStart = SlotTicks.Count,
                SlotsCount = SlotNames.Count,
            };
            foreach (var column in Columns)
                ColumnValues.Add(column.Sample());
            for (var s = 0; s < SlotNames.Count; s++)
                SlotTicks.Add(0);
            _currentFrame = frame;
            if (_measuredFramesToQuit > 0 && _measureStartRow >= 0 &&
                Frames.Count >= _measureStartRow + _warmup + _measuredFramesToQuit)
            {
                _measuredFramesToQuit = 0;
                Write();
                Application.Quit();
            }
        }

        private static void Write()
        {
            if (!Enabled || _written)
                return;
            _written = true;
            if (_currentFrame >= 0)
                Frames.Add(_current);
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var inv = CultureInfo.InvariantCulture;
                using (var csv = new StreamWriter(_path, false, new UTF8Encoding(false)))
                {
                    csv.Write("frame,unscaled_delta_ms,fixed_ticks");
                    foreach (var name in CategoryNames)
                        csv.Write("," + name.ToLowerInvariant() + "_ms");
                    csv.Write(",ecs_total_ms");
                    foreach (var column in Columns)
                        csv.Write("," + column.Name);
                    csv.WriteLine();
                    foreach (var f in Frames)
                    {
                        csv.Write(f.Number.ToString(inv));
                        csv.Write(',');
                        csv.Write(f.UnscaledDeltaMs.ToString("0.###", inv));
                        csv.Write(',');
                        csv.Write(f.FixedTicks.ToString(inv));
                        long total = 0;
                        for (var c = 0; c < CategoryCount; c++)
                        {
                            total += f[c];
                            csv.Write(',');
                            csv.Write((f[c] * TicksToMs).ToString("0.####", inv));
                        }
                        csv.Write(',');
                        csv.Write((total * TicksToMs).ToString("0.####", inv));
                        for (var c = 0; c < Columns.Count; c++)
                        {
                            csv.Write(',');
                            var value = ColumnValue(f, c);
                            if (!double.IsNaN(value))
                                csv.Write(value.ToString("0.###", inv));
                        }
                        csv.WriteLine();
                    }
                }
                if (SlotNames.Count > 0)
                {
                    // Milliseconds per frame per system or section; an empty cell is 0 (skipped or not yet registered).
                    using (var csv = new StreamWriter(_path + ".systems.csv", false, new UTF8Encoding(false)))
                    {
                        csv.Write("frame");
                        foreach (var name in SlotNames)
                            csv.Write("," + name);
                        csv.WriteLine();
                        foreach (var f in Frames)
                        {
                            csv.Write(f.Number.ToString(inv));
                            for (var s = 0; s < SlotNames.Count; s++)
                            {
                                csv.Write(',');
                                var ticks = SlotTicksOf(f, s);
                                if (ticks != 0)
                                    csv.Write((ticks * TicksToMs).ToString("0.####", inv));
                            }
                            csv.WriteLine();
                        }
                    }
                }
                File.WriteAllText(_path + ".summary.txt", Summary(), new UTF8Encoding(false));
                UnityEngine.Debug.Log($"[EcsLoopProfiler] wrote {Frames.Count} frames to {_path}");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static double ColumnValue(in Frame frame, int column) =>
            column < frame.ValuesCount ? ColumnValues[frame.ValuesStart + column] : double.NaN;

        private static long SlotTicksOf(in Frame frame, int slot) =>
            slot < frame.SlotsCount ? SlotTicks[frame.SlotsStart + slot] : 0;

        private static string Summary()
        {
            var inv = CultureInfo.InvariantCulture;
            var measured = new List<Frame>();
            var first = _measureStartRow < 0 ? Frames.Count : _measureStartRow + _warmup;
            for (var i = first; i < Frames.Count; i++)
                measured.Add(Frames[i]);
            var sb = new StringBuilder();
            sb.AppendLine($"frames recorded: {Frames.Count}, warmup excluded: {Math.Min(first, Frames.Count)}, measured: {measured.Count}");
            if (!string.IsNullOrEmpty(_warmupFrom))
                sb.AppendLine(_measureStartRow < 0
                    ? $"warmup counted from mark '{_warmupFrom}', which never happened"
                    : $"warmup counted from mark '{_warmupFrom}' (row {_measureStartRow})");
            foreach (var mark in Marks)
                sb.AppendLine($"mark '{mark.Name}' at frame {mark.Frame} (row {mark.Row})");
            sb.AppendLine($"build: {Application.version}, unity {Application.unityVersion}, {SystemInfo.processorType}, {SystemInfo.operatingSystem}");
            sb.AppendLine("ms per frame          median      p95      p99     mean      max");
            void Row(string name, Func<Frame, double> select)
            {
                var values = new List<double>(measured.Count);
                double sum = 0;
                foreach (var frame in measured)
                {
                    var value = select(frame);
                    if (double.IsNaN(value))
                        continue;
                    values.Add(value);
                    sum += value;
                }
                if (values.Count == 0)
                    return;
                values.Sort();
                double Pct(double p) => values[Math.Min(values.Count - 1, (int)Math.Floor(p * (values.Count - 1)))];
                sb.AppendLine(string.Format(inv, "{0,-18} {1,9:0.000} {2,8:0.000} {3,8:0.000} {4,8:0.000} {5,8:0.000}",
                    name, Pct(0.5), Pct(0.95), Pct(0.99), sum / values.Count, values[values.Count - 1]));
            }
            Row("ecs total", f =>
            {
                long total = 0;
                for (var c = 0; c < CategoryCount; c++)
                    total += f[c];
                return total * TicksToMs;
            });
            for (var c = 0; c < CategoryCount; c++)
            {
                var category = c;
                var any = false;
                foreach (var f in measured)
                {
                    if (f[category] == 0)
                        continue;
                    any = true;
                    break;
                }
                if (any)
                    Row(CategoryNames[category], f => f[category] * TicksToMs);
            }
            Row("frame (unscaled)", f => f.UnscaledDeltaMs);
            if (Columns.Count > 0)
            {
                sb.AppendLine("columns               median      p95      p99     mean      max");
                for (var c = 0; c < Columns.Count; c++)
                {
                    var column = c;
                    Row(Columns[column].Name, f => ColumnValue(f, column));
                }
            }
            if (SlotNames.Count > 0 && measured.Count > 0)
            {
                // Share is of the mean ECS total; a section is part of its system, so shares do not add up.
                double totalMean = 0;
                foreach (var f in measured)
                {
                    long total = 0;
                    for (var c = 0; c < CategoryCount; c++)
                        total += f[c];
                    totalMean += total * TicksToMs;
                }
                totalMean /= measured.Count;
                var stats = new List<(string Name, double Median, double P95, double P99, double Mean, double Max)>();
                var values = new List<double>(measured.Count);
                for (var s = 0; s < SlotNames.Count; s++)
                {
                    values.Clear();
                    double sum = 0, max = 0;
                    foreach (var f in measured)
                    {
                        var value = SlotTicksOf(f, s) * TicksToMs;
                        values.Add(value);
                        sum += value;
                        if (value > max)
                            max = value;
                    }
                    values.Sort();
                    double At(double p) => values[Math.Min(values.Count - 1, (int)Math.Floor(p * (values.Count - 1)))];
                    stats.Add((SlotNames[s], At(0.5), At(0.95), At(0.99), sum / values.Count, max));
                }
                stats.Sort((a, b) => b.Mean.CompareTo(a.Mean));
                sb.AppendLine("systems and sections (ms per measured frame, by mean)");
                sb.AppendLine(string.Format(inv, "{0,-52} {1,8} {2,8} {3,8} {4,8} {5,8} {6,7}",
                    "name", "median", "p95", "p99", "mean", "max", "share"));
                foreach (var row in stats)
                    sb.AppendLine(string.Format(inv, "{0,-52} {1,8:0.0000} {2,8:0.0000} {3,8:0.0000} {4,8:0.0000} {5,8:0.000} {6,6:0.0}%",
                        row.Name, row.Median, row.P95, row.P99, row.Mean, row.Max,
                        totalMean > 0 ? 100 * row.Mean / totalMean : 0));
            }
            return sb.ToString();
        }
    }
}
#endif
