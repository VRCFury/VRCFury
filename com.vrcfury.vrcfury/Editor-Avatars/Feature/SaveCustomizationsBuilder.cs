using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Service.Compressor;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    [Serializable]
    internal class LocalAvatarDataFile {
        public List<LocalAvatarDataParam> animationParameters = new List<LocalAvatarDataParam>();
    }

    [Serializable]
    internal class LocalAvatarDataParam {
        public string name;
        public float value;
    }

    [FeatureTitle("Save Customizations")]
    [FeatureRootOnly]
    [FeatureOnlyOneAllowed]
    internal class SaveCustomizationsBuilder : FeatureBuilder<SaveCustomizations> {
        [VFAutowired] private readonly ParamsService paramsService;
        [VFAutowired] private readonly ParameterSourceService paramSourceService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly MenuService menu;

        [FeatureBuilderAction(FeatureOrder.DriveNonFloatTypes)]
        public void Apply() {
            var pm = avatar.GetComponent<PipelineManager>();
            var blueprintId = pm != null ? pm.blueprintId : null;

            var source = model.source?.Trim() ?? "";
            var path = ResolveSource(model.source, blueprintId, out var explicitPath);
            Dictionary<string, float> values;
            if (explicitPath) {
                values = ReadFile(path);
            } else if (string.IsNullOrEmpty(path)) {
                var what = source.Length > 0 ? $"blueprint id '{source}'" : $"avatar '{blueprintId}'";
                Debug.LogWarning($"[SaveCustomizations] no saved data found for {what}; skipping.");
                return;
            } else {
                try {
                    values = ReadFile(path);
                } catch (Exception e) {
                    Debug.LogWarning($"[SaveCustomizations] saved data could not be read, skipping - {e.Message}");
                    return;
                }
            }

            var fileAvtr = Regex.Match(path ?? "", @"avtr_[0-9a-fA-F-]+").Value;
            if (!string.IsNullOrEmpty(fileAvtr) && !string.IsNullOrEmpty(blueprintId) && fileAvtr != blueprintId) {
                Debug.LogWarning($"[SaveCustomizations] file is for {fileAvtr}, but this avatar is {blueprintId} - wrong file?");
            }

            var matcher = new Matcher(values, LoadDesktopSyncData(blueprintId));
            var matched = new List<(VRCExpressionParameters.Parameter param, float value)>();
            void Collect(VRCExpressionParameters.Parameter param, float v) => matched.Add((param, Coerce(param.valueType, v)));

            var allParams = paramsService.GetParams().GetRaw().parameters.Where(p => p?.name != null).ToList();
            var sources = allParams.ToDictionary(p => p, p => paramSourceService.GetSource(p.name));

            var afterSource = new List<VRCExpressionParameters.Parameter>();
            foreach (var p in allParams) {
                if (matcher.TryMatchSource(sources[p], out var v)) Collect(p, v);
                else afterSource.Add(p);
            }
            var afterName = new List<VRCExpressionParameters.Parameter>();
            foreach (var p in afterSource) {
                if (matcher.TryMatchName(p.name, out var v)) Collect(p, v);
                else afterName.Add(p);
            }
            foreach (var p in afterName) {
                if (matcher.TryMatchLoose(sources[p].originalParamName, out var v)) Collect(p, v);
            }

            if (matched.Count == 0) {
                Debug.LogWarning("[SaveCustomizations] no saved values matched any avatar parameter; skipping.");
                return;
            }

            if (model.bakeIntoDefaults) {
                var changed = 0;
                foreach (var (param, value) in matched) {
                    if (!Mathf.Approximately(param.defaultValue, value)) changed++;
                    param.defaultValue = value;
                }
                Debug.Log($"[SaveCustomizations] baked {changed} default(s) from {matched.Count} matched value(s)");
            } else {
                BuildMenuButton(matched);
                Debug.Log($"[SaveCustomizations] menu button drives {matched.Count} matched param(s)");
            }
        }

        private void BuildMenuButton(List<(VRCExpressionParameters.Parameter param, float value)> matched) {
            var fx = controllers.GetFx();
            var trigger = fx.NewBool("Save Customizations", synced: true, networkSynced: false);
            var menuPath = string.IsNullOrWhiteSpace(model.menuPath) ? "Load Customization" : model.menuPath;
            menu.GetMenu().NewMenuButton(menuPath, trigger, icon: model.icon?.Get());

            var layer = fx.NewLayer("Save Customizations");
            var idle = layer.NewState("Idle");
            var apply = layer.NewState("Apply");
            foreach (var (param, value) in matched) apply.Drives(param.name, value);
            idle.TransitionsTo(apply).When(trigger.IsTrue());
            apply.TransitionsTo(idle).When(trigger.IsFalse());
        }

        private static string ResolveSource(string source, string blueprintId, out bool explicitPath) {
            explicitPath = false;
            if (string.IsNullOrWhiteSpace(source)) return DetectFile(blueprintId);
            source = source.Trim();
            if (source.Contains('/') || source.Contains('\\')) {
                explicitPath = true;
                return source;
            }
            return DetectFile(source.StartsWith("avtr_") ? source : "avtr_" + source);
        }

        private static float Coerce(VRCExpressionParameters.ValueType type, float v) {
            switch (type) {
                case VRCExpressionParameters.ValueType.Bool: return Mathf.Clamp(Mathf.Round(v), 0f, 1f);
                case VRCExpressionParameters.ValueType.Int: return Mathf.Clamp(Mathf.Round(v), 0f, 255f);
                default: return v;
            }
        }

        private static Dictionary<string, float> ReadFile(string path) {
            if (string.IsNullOrWhiteSpace(path))
                throw new Exception("Save Customizations: no file selected");
            if (!File.Exists(path))
                throw new Exception("Save Customizations: file not found:\n" + path);

            LocalAvatarDataFile data;
            try {
                data = JsonUtility.FromJson<LocalAvatarDataFile>(File.ReadAllText(path));
            } catch (Exception e) {
                throw new Exception("Save Customizations: failed to parse file as JSON\n" + e.Message);
            }
            if (data?.animationParameters == null || data.animationParameters.Count == 0)
                throw new Exception("Save Customizations: no animationParameters found in file");

            var values = new Dictionary<string, float>();
            foreach (var p in data.animationParameters)
                if (!string.IsNullOrEmpty(p.name)) values[p.name] = p.value;
            return values;
        }

        private static Dictionary<string, ParameterSourceService.Source> LoadDesktopSyncData(string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            try {
                var path = ParameterPlatformAlignmentService.GetSavePath(blueprintId);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var data = JsonUtility.FromJson<ParameterPlatformAlignmentService.SavedData>(File.ReadAllText(path));
                if (data?.parameters == null || data.saveVersion != 3) return null;
                var map = new Dictionary<string, ParameterSourceService.Source>();
                foreach (var p in data.parameters)
                    if (p.parameter?.name != null) map[p.parameter.name] = p.source;
                return map;
            } catch {
                return null;
            }
        }

        public static string DetectFile(string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var dir in LocalAvatarDataDirs()) {
                if (!Directory.Exists(dir)) continue;
                // scan every account dir and take the newest match, rather than the logged-in SDK user's dir:
                // the SDK's current user isn't reliably available at build time
                string[] userDirs;
                try { userDirs = Directory.GetDirectories(dir); } catch { continue; }
                foreach (var userDir in userDirs) {
                    var candidate = Path.Combine(userDir, blueprintId);
                    DateTime t;
                    try {
                        if (!File.Exists(candidate)) continue;
                        t = File.GetLastWriteTimeUtc(candidate);
                    } catch { continue; }
                    if (best == null || t > bestTime) { best = candidate; bestTime = t; }
                }
            }
            return best;
        }

        private static IEnumerable<string> LocalAvatarDataDirs() {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) {
                yield return Path.Combine(home, "AppData", "LocalLow", "VRChat", "VRChat", "LocalAvatarData");
            }
            foreach (var lib in SteamLibraries()) {
                yield return Path.Combine(lib, "steamapps", "compatdata", "438100", "pfx",
                    "drive_c", "users", "steamuser", "AppData", "LocalLow", "VRChat", "VRChat", "LocalAvatarData");
            }
        }

        private static IEnumerable<string> SteamLibraries() {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) yield break;
            var seen = new HashSet<string>();
            var roots = new[] {
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".steam", "root"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            };
            foreach (var root in roots) {
                if (!Directory.Exists(root)) continue;
                if (seen.Add(root)) yield return root;
                foreach (var vdf in new[] {
                    Path.Combine(root, "steamapps", "libraryfolders.vdf"),
                    Path.Combine(root, "config", "libraryfolders.vdf"),
                }) {
                    if (!File.Exists(vdf)) continue;
                    string text;
                    try { text = File.ReadAllText(vdf); } catch { continue; }
                    foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\"")) {
                        var p = Regex.Replace(m.Groups[1].Value, @"\\+", "/");
                        if (Directory.Exists(p) && seen.Add(p)) yield return p;
                    }
                }
            }
        }

        private class FileEntry {
            public string name;
            public float value;
            public bool consumed;
        }

        private class Matcher {
            private static readonly Regex VfPrefix = new Regex(@"^VF\d+_");
            private readonly Dictionary<string, FileEntry> byName = new Dictionary<string, FileEntry>();
            private readonly Dictionary<ParameterSourceService.Source, FileEntry> bySource =
                new Dictionary<ParameterSourceService.Source, FileEntry>();
            private readonly Dictionary<string, FileEntry> byOrig = new Dictionary<string, FileEntry>();
            private readonly HashSet<string> ambiguousOrig = new HashSet<string>();
            private readonly List<FileEntry> entries = new List<FileEntry>();

            public Matcher(Dictionary<string, float> values, Dictionary<string, ParameterSourceService.Source> desktop) {
                foreach (var kv in values) {
                    var e = new FileEntry { name = kv.Key, value = kv.Value };
                    entries.Add(e);
                    byName[kv.Key] = e;

                    string orig;
                    if (desktop != null && desktop.TryGetValue(kv.Key, out var src)) {
                        bySource[src] = e;
                        orig = (src.originalParamName ?? "").Trim();
                    } else {
                        orig = VfPrefix.Replace(kv.Key, "").Trim();
                    }
                    if (byOrig.TryGetValue(orig, out var existing)) {
                        if (!Mathf.Approximately(existing.value, kv.Value)) ambiguousOrig.Add(orig);
                    } else {
                        byOrig[orig] = e;
                    }
                }
            }

            public bool TryMatchSource(ParameterSourceService.Source src, out float value) {
                if (bySource.TryGetValue(src, out var e) && !e.consumed) { value = e.value; e.consumed = true; return true; }
                value = 0;
                return false;
            }

            public bool TryMatchName(string paramName, out float value) {
                if (byName.TryGetValue(paramName, out var e) && !e.consumed) { value = e.value; e.consumed = true; return true; }
                value = 0;
                return false;
            }

            public bool TryMatchLoose(string originalParamName, out float value) {
                var key = (originalParamName ?? "").Trim();
                if (!ambiguousOrig.Contains(key) && byOrig.TryGetValue(key, out var e) && !e.consumed) {
                    value = e.value; e.consumed = true; return true;
                }
                value = 0;
                return false;
            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();

            content.Add(VRCFuryEditorUtils.Info(
                "This feature adds a menu button that sets your avatar back to the way you last had it set up in-game," +
                " including all of your toggles, colours and sliders.\n\n" +
                "Enable 'Bake into defaults' to make that saved setup the avatar's default instead, without adding a button."));

            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("source"), "Blueprint ID or File Path"));
            content.Add(VRCFuryEditorUtils.Info(
                "If left empty, the blueprint id of the avatar being uploaded is used automatically."));

            var bakeProp = prop.FindPropertyRelative("bakeIntoDefaults");

            var menuSection = new VisualElement();
            menuSection.style.marginTop = 8;
            menuSection.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuPath"), "Custom Menu Path", labelWidth: 130));
            menuSection.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Menu Icon", labelWidth: 130));
            void RefreshMenuEnabled() => menuSection.SetEnabled(!bakeProp.boolValue);
            RefreshMenuEnabled();

            content.Add(VRCFuryEditorUtils.Prop(bakeProp, "Bake into defaults", onChange: RefreshMenuEnabled));
            content.Add(menuSection);

            return content;
        }
    }
}
