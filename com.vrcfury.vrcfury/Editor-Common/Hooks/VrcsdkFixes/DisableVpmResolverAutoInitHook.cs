using System.IO;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class DisableVpmResolverAutoInitHook {
        private const string ResolverPath = "Packages/com.vrchat.core.vpm-resolver/Editor/Resolver/Resolver.cs";
        private const string EnabledLine = "    [InitializeOnLoad]";
        private const string DisabledLine = "    // [InitializeOnLoad]";

        [VFInit]
        private static void Init() {
            Apply();
        }

        public static void Apply() {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            var fullPath = Path.Combine(projectRoot, ResolverPath);
            if (!File.Exists(fullPath)) return;

            var text = File.ReadAllText(fullPath);
            var disable = DisableVpmResolverInitMenuItem.Get();
            var next = disable
                ? text.Replace(EnabledLine, DisabledLine)
                : text.Replace(DisabledLine, EnabledLine);

            if (next == text) return;

            File.WriteAllText(fullPath, next);
        }
    }
}
