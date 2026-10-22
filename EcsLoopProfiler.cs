using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    /// <summary>
    /// Stopwatches around the ECS game loop: every pipeline category tick (all its systems, including the
    /// reactive flush after each system), summed per rendered frame. Off unless the player is started with
    /// <c>-ecsLoopProfile &lt;file.csv&gt;</c>; then it costs two timestamps per category tick.
    /// Optional: <c>-ecsLoopProfileWarmup &lt;frames&gt;</c> (default 300) frames excluded from the summary,
    /// <c>-ecsLoopProfileFrames &lt;frames&gt;</c> measured frames after which the player quits.
    /// Writes one CSV row per frame and <c>&lt;file&gt;.summary.txt</c> with median, p95, p99 and mean.
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
            Frames.Clear();
            _currentFrame = -1;
            _written = false;
            Application.quitting += Write;
            UnityEngine.Debug.Log($"[EcsLoopProfiler] recording to {_path}, warmup {_warmup} frames" +
                                  (_measuredFramesToQuit > 0 ? $", quitting after {_measuredFramesToQuit} measured frames" : ""));
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
            _current = new Frame { Number = frame, UnscaledDeltaMs = Time.unscaledDeltaTime * 1000f };
            _currentFrame = frame;
            if (_measuredFramesToQuit > 0 && Frames.Count >= _warmup + _measuredFramesToQuit)
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
                    csv.WriteLine(",ecs_total_ms");
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
                        csv.WriteLine((total * TicksToMs).ToString("0.####", inv));
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

        private static string Summary()
        {
            var inv = CultureInfo.InvariantCulture;
            var measured = new List<Frame>();
            for (var i = _warmup; i < Frames.Count; i++)
                measured.Add(Frames[i]);
            var sb = new StringBuilder();
            sb.AppendLine($"frames recorded: {Frames.Count}, warmup excluded: {Math.Min(_warmup, Frames.Count)}, measured: {measured.Count}");
            sb.AppendLine($"build: {Application.version}, unity {Application.unityVersion}, {SystemInfo.processorType}, {SystemInfo.operatingSystem}");
            sb.AppendLine("ms per frame          median      p95      p99     mean      max");
            void Row(string name, Func<Frame, double> select)
            {
                if (measured.Count == 0)
                    return;
                var values = new double[measured.Count];
                double sum = 0;
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = select(measured[i]);
                    sum += values[i];
                }
                Array.Sort(values);
                double Pct(double p) => values[Math.Min(values.Length - 1, (int)Math.Floor(p * (values.Length - 1)))];
                sb.AppendLine(string.Format(inv, "{0,-18} {1,9:0.000} {2,8:0.000} {3,8:0.000} {4,8:0.000} {5,8:0.000}",
                    name, Pct(0.5), Pct(0.95), Pct(0.99), sum / values.Length, values[values.Length - 1]));
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
            return sb.ToString();
        }
    }
}
