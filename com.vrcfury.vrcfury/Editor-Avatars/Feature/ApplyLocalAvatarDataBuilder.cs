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

    [FeatureTitle("Apply Local Avatar Data")]
    [FeatureRootOnly]
    [FeatureOnlyOneAllowed]
    internal class ApplyLocalAvatarDataBuilder : FeatureBuilder<ApplyLocalAvatarData> {
        [VFAutowired] private readonly ParamsService paramsService;
        [VFAutowired] private readonly ParameterSourceService paramSourceService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;

        [FeatureBuilderAction(FeatureOrder.DriveNonFloatTypes)]
        public void Apply() {
            var pm = avatar.GetComponent<PipelineManager>();
            var blueprintId = pm != null ? pm.blueprintId : null;

            var manual = model.filePath;
            var path = !string.IsNullOrEmpty(manual) ? manual : DetectFile(blueprintId);
            Dictionary<string, float> values;
            if (!string.IsNullOrEmpty(manual)) {
                values = ReadFile(manual);
            } else if (string.IsNullOrEmpty(path)) {
                Debug.LogWarning($"[ApplyLocalAvatarData] no file set and none auto-detected for '{blueprintId}'; skipping.");
                return;
            } else {
                try {
                    values = ReadFile(path);
                } catch (Exception e) {
                    Debug.LogWarning($"[ApplyLocalAvatarData] auto-detected file could not be read, skipping - {e.Message}");
                    return;
                }
            }

            var fileAvtr = Regex.Match(path ?? "", @"avtr_[0-9a-fA-F-]+").Value;
            if (!string.IsNullOrEmpty(fileAvtr) && !string.IsNullOrEmpty(blueprintId) && fileAvtr != blueprintId) {
                Debug.LogWarning($"[ApplyLocalAvatarData] file is for {fileAvtr}, but this avatar is {blueprintId} - wrong file?");
            }

            var matcher = new Matcher(values, LoadDesktopSyncData(blueprintId));
            var changed = 0;

            void ApplyValue(VRCExpressionParameters.Parameter param, float v) {
                var newVal = Coerce(param.valueType, v);
                if (!Mathf.Approximately(param.defaultValue, newVal)) changed++;
                param.defaultValue = newVal;
            }

            var allParams = paramsService.GetParams().GetRaw().parameters.Where(p => p?.name != null).ToList();
            var sources = allParams.ToDictionary(p => p, p => paramSourceService.GetSource(p.name));

            var afterSource = new List<VRCExpressionParameters.Parameter>();
            foreach (var p in allParams) {
                if (matcher.TryMatchSource(sources[p], out var v)) ApplyValue(p, v);
                else afterSource.Add(p);
            }
            var afterName = new List<VRCExpressionParameters.Parameter>();
            foreach (var p in afterSource) {
                if (matcher.TryMatchName(p.name, out var v)) ApplyValue(p, v);
                else afterName.Add(p);
            }
            foreach (var p in afterName) {
                if (matcher.TryMatchLoose(sources[p].originalParamName, out var v)) ApplyValue(p, v);
            }

            var unmatched = matcher.Unmatched();
            Debug.Log($"[ApplyLocalAvatarData] updated {changed} default(s) from {values.Count} saved value(s)" +
                      (unmatched.Count > 0 ? $" ({unmatched.Count} unmatched: {string.Join(", ", unmatched)})" : ""));
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
                throw new Exception("Apply Local Avatar Data: no file selected");
            if (!File.Exists(path))
                throw new Exception("Apply Local Avatar Data: file not found:\n" + path);

            LocalAvatarDataFile data;
            try {
                data = JsonUtility.FromJson<LocalAvatarDataFile>(File.ReadAllText(path));
            } catch (Exception e) {
                throw new Exception("Apply Local Avatar Data: failed to parse file as JSON\n" + e.Message);
            }
            if (data?.animationParameters == null || data.animationParameters.Count == 0)
                throw new Exception("Apply Local Avatar Data: no animationParameters found in file");

            var values = new Dictionary<string, float>();
            foreach (var p in data.animationParameters)
                if (!string.IsNullOrEmpty(p.name)) values[p.name] = p.value;
            return values;
        }

        private static Dictionary<string, ParameterSourceService.Source> LoadDesktopSyncData(string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            try {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData)) return null;
                var path = Path.Combine(localAppData, "VRCFury", "DesktopSyncData", blueprintId + ".json");
                if (!File.Exists(path)) return null;
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

        // Newest LocalAvatarData file for the blueprint id, across all platforms/Steam libraries, null if none
        public static string DetectFile(string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var dir in LocalAvatarDataDirs()) {
                if (!Directory.Exists(dir)) continue;
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
            // Linux: VRChat runs under Proton, so its LocalLow is in Steam's compat prefix for app 438100
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

        // Matches by (in order): stable Source (survives VF re-numbering), exact name, then VF-prefix-stripped name.
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

            public List<string> Unmatched() => entries.Where(e => !e.consumed).Select(e => e.name).ToList();
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var content = new VisualElement();

            content.Add(VRCFuryEditorUtils.Info(
                "During upload, this overwrites the avatar's expression parameter defaults with the values saved in" +
                " your VRChat LocalAvatarData file, so the avatar uploads matching your saved in-game customization.\n\n" +
                "Leave the file path empty to auto-detect the file from this avatar's blueprint id. Set a path to use" +
                " a specific file instead."));

            var pathProp = prop.FindPropertyRelative("filePath");
            content.Add(VRCFuryEditorUtils.Prop(pathProp, "File path (empty = auto-detect)"));

            var browse = new Button(() => {
                var startDir = "";
                if (!string.IsNullOrEmpty(pathProp.stringValue)) {
                    try { startDir = Path.GetDirectoryName(pathProp.stringValue); } catch { /* ignore */ }
                }
                var picked = EditorUtility.OpenFilePanel("Select LocalAvatarData file", startDir ?? "", "");
                if (!string.IsNullOrEmpty(picked)) {
                    pathProp.stringValue = picked;
                    pathProp.serializedObject.ApplyModifiedProperties();
                }
            }) { text = "Browse..." };
            content.Add(browse);

            var detectLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 4, marginBottom = 4 } };
            content.Add(new Button(() => {
                var pm = avatarObject != null ? avatarObject.GetComponent<PipelineManager>() : null;
                var blueprintId = pm != null ? pm.blueprintId : null;
                if (string.IsNullOrEmpty(blueprintId)) {
                    detectLabel.text = "This avatar has no blueprint id yet - upload once first.";
                } else {
                    var detected = DetectFile(blueprintId);
                    detectLabel.text = detected != null ? "Auto-detected file:\n" + detected
                        : $"No LocalAvatarData file found for {blueprintId}.";
                }
            }) { text = "Preview auto-detected file" });
            content.Add(detectLabel);

            return content;
        }
    }
}
