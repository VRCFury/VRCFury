using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class VfInitTimingLoggerMenuItem {
        private const string EditorPref = "com.vrcfury.logVfInitTimings";
        private static readonly List<(string name, double milliseconds)> Timings = new();

        [MenuItem(MenuItems.logVfInitTimings, priority = MenuItems.logVfInitTimingsPriority)]
        private static void Click() {
            EditorPrefs.SetBool(EditorPref, !Get());
        }

        [MenuItem(MenuItems.logVfInitTimings, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.logVfInitTimings, Get());
            return true;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, false);
        }

        public static void Run(Action action, string name) {
            if (!Get()) {
                action();
                return;
            }

            var timer = Stopwatch.StartNew();
            try {
                action();
            } finally {
                timer.Stop();
                if (Timings.Count == 0) {
                    EditorApplication.update += Flush;
                    AssemblyReloadEvents.beforeAssemblyReload += Flush;
                }
                Timings.Add((name, timer.Elapsed.TotalMilliseconds));
            }
        }

        private static void Flush() {
            EditorApplication.update -= Flush;
            AssemblyReloadEvents.beforeAssemblyReload -= Flush;
            var report = string.Join("\n", Timings
                .OrderByDescending(timing => timing.milliseconds)
                .Select(timing => $"{timing.milliseconds:F1} ms  {timing.name}"));
            Timings.Clear();
            VFLog.Info("Timings:\n" + report);
        }
    }
}
