using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Hooks.UnityFixes;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class VRCFuryAssetDatabase {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Action<Object[], string> CreateAssetFromObjects = typeof(AssetDatabase)
                .GetMatchingDelegate<Action<Object[], string>>("CreateAssetFromObjects");
        }

        private static readonly Dictionary<string, string> pendingSavePaths = new Dictionary<string, string>();
        private static HashSet<Object> pendingAssetSaves;

        public static string GetAssetPath(Object obj) {
            // Unity assigns the file GUID before it registers the path. Subassets share that GUID.
            if (pendingSavePaths.Count > 0
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _)
                && pendingSavePaths.TryGetValue(guid, out var path)
                && (File.Exists(path) || Directory.Exists(path))) {
                return path;
            }
            return AssetDatabase.GetAssetPath(obj);
        }

        public static string GetDirectoryName(string path) {
            // Path.GetDirectoryName returns with backslashes on windows :(
            return Path.GetDirectoryName(path)?.Replace("\\", "/");
        }

        private static string MakeFilenameSafe(string str, int maxLen) {
            if (maxLen < 4) maxLen = 4;

            var output = "";
            foreach (var c in str) {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '.') {
                    output += c;
                } else {
                    output += '_';
                }
            }

            if (output.Length > maxLen) {
                var startLength = (maxLen - 1) / 2;
                var endLength = (maxLen - 1) - startLength;
                output = output.Substring(0, startLength)
                    + "_"
                    + output.Substring(output.Length - endLength, endLength);
            }

            // Unity will reject importing folders / files that start or end with a dot (this is undocumented)
            while (output.StartsWith(" ") || output.StartsWith(".")) {
                output = output.Substring(1);
            }
            while (output.EndsWith(" ") || output.EndsWith(".")) {
                output = output.Substring(0, output.Length-1);
            }

            if (output.Length == 0) output = "Unknown";
            return output;
        }

        public static string GetUniquePath(string dir, string filename, string ext = "", string importantSuffix = "", int startMaxLen = 64) {
            for (var maxLen = startMaxLen; maxLen > 4; maxLen--) {
                var safeFilename = MakeFilenameSafe(filename, maxLen);

                string FullPath(string name) => dir + "/" + name + importantSuffix + (ext != "" ? "." + ext : "");
                var uniqueName = safeFilename.MakeUnique(name => File.Exists(FullPath(name)) || Directory.Exists(FullPath(name)));
                var fullPath = FullPath(uniqueName);

                if (Path.GetFullPath(fullPath).Length < 250) {
                    return fullPath;
                }
            }

            throw new Exception(
                "Failed to find a path that can fit in Windows file length limits!" +
                " The file path for your Unity project is probably too long.");
        }

        public static void SaveAsset(Object obj, string fullPath) {
            SaveAsset(obj, fullPath, subAssets: null);
        }

        /** Writes the main asset and its subassets together so Unity's first import sees the complete file. */
        public static void SaveAsset(Object obj, string fullPath, IReadOnlyCollection<Object> subAssets) {
            CreateFolder(GetDirectoryName(fullPath));

            // If object was already part of another asset, or was recently deleted, we MUST
            // call this first, or unity will throw an exception
            AssetDatabase.RemoveObjectFromAsset(obj);
            obj.hideFlags &= ~HideFlags.DontSaveInEditor;
            if (subAssets == null || subAssets.Count == 0) {
                AssetDatabase.CreateAsset(obj, fullPath);
            } else {
                var objects = new Object[subAssets.Count + 1];
                objects[0] = obj;
                var index = 1;
                foreach (var subAsset in subAssets) {
                    subAsset.hideFlags &= ~HideFlags.DontSaveInEditor;
                    AssetDatabase.RemoveObjectFromAsset(subAsset);
                    objects[index++] = subAsset;
                }
                Reflection.CreateAssetFromObjects(objects, fullPath);
            }
            if (assetEditing && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _)) {
                pendingSavePaths[guid] = fullPath;
            }
        }

        public static void MoveAsset(string from, string to) {
            if (assetEditing) {
                // Technically this "could" be supported, but it's complicated because we'd have to update
                // all the
                throw new InvalidOperationException($"Cannot move asset '{from}' to '{to}' while asset editing is active.");
            }

            CreateFolder(GetDirectoryName(to));
            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }

        public static void SaveAsset(Object obj, string dir, string filename) {
            string ext;
            if (obj is AnimationClip) {
                ext = "anim";
            } else if (obj is Material) {
                ext = "mat";
            } else if (obj is AnimatorController) {
                ext = "controller";
            } else if (obj is AvatarMask) {
                ext = "mask";
            } else {
                ext = "asset";
            }

            var fullPath = GetUniquePath(dir, filename, ext);
            SaveAsset(obj, fullPath);
        }

        public static void AttachAsset(Object objectToAttach, Object parent) {
            objectToAttach.hideFlags &= ~HideFlags.DontSaveInEditor;
            AssetDatabase.RemoveObjectFromAsset(objectToAttach);
            AssetDatabase.AddObjectToAsset(objectToAttach, parent);
        }

        public static void SaveAssetIfDirty(Object asset) {
#if UNITY_6000_0_OR_NEWER
            // Unity can skip writing subassets until their file has been imported.
            if (assetEditing && pendingSavePaths.Count > 0
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long _)
                && pendingSavePaths.ContainsKey(guid)) {
                pendingAssetSaves ??= new HashSet<Object>();
                pendingAssetSaves.Add(asset);
                return;
            }
#endif
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static bool assetEditing = false;

        private class AssetEditingScope : IDisposable {
            private bool startedAssetEditing;

            public AssetEditingScope(bool startedAssetEditing) {
                this.startedAssetEditing = startedAssetEditing;
            }

            public void Dispose() {
                if (!startedAssetEditing) return;
                startedAssetEditing = false;
                StopAssetEditing();
                Unity6RendererFixHook.Process();
            }
        }

        private static void StopAssetEditing() {
            // Import callbacks can open a new batch while Unity is finishing this one.
            var saves = pendingAssetSaves;
            pendingAssetSaves = null;
            pendingSavePaths.Clear();
            assetEditing = false;
            AssetDatabase.StopAssetEditing();
            if (saves != null) {
                foreach (var asset in saves) {
                    if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
        }

        public static IDisposable WithAssetEditing() {
            if (assetEditing) return new AssetEditingScope(false);

            AssetDatabase.StartAssetEditing();
            assetEditing = true;
            return new AssetEditingScope(true);
        }

        public static void WithAssetEditing(Action go) {
            using (WithAssetEditing()) {
                go();
            }
        }

        public static void WithoutAssetEditing(Action go) {
            if (assetEditing) {
                try {
                    StopAssetEditing();
                    go();
                } finally {
                    AssetDatabase.StartAssetEditing();
                    assetEditing = true;
                }
            } else {
                go();
            }
        }

        public static void Delete(string path) {
            //Debug.Log("Deleting " + path);
            if (!AssetDatabase.DeleteAsset(path) || pendingSavePaths.Count == 0) return;
            foreach (var entry in pendingSavePaths.ToArray()) {
                if (IsPathAtOrBelow(entry.Value, path)) pendingSavePaths.Remove(entry.Key);
            }
        }

        private static bool IsPathAtOrBelow(string path, string root) {
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && (path.Length == root.Length || path[root.Length] == '/');
        }

        public static void DeleteFiltered(string path, Func<string,bool> filter) {
            var deleted = new List<string>();
            var subPaths = AssetDatabase.FindAssets("", new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p);
            foreach (var subPath in subPaths) {
                if (deleted.Any(d => subPath.StartsWith(d + "/"))) {
                    // already deleted a parent folder
                    continue;
                }
                if (!filter(subPath)) {
                    continue;
                }
                deleted.Add(subPath);
                Delete(subPath);
            }
        }

        /**
         * Directory.CreateDirectory causes a SIGSEGV on some systems if used to create directories recursively.
         * No idea why. So we have to make them one-by-one ourselves.
         *
         * Received signal SIGSEGV
         * Obtained 2 stack frames
         * RtlLookupFunctionEntry returned NULL function. Aborting stack walk.
         */
        public static void CreateFolder(string path) {
            var paths = new List<string>();
            while (!string.IsNullOrEmpty(path)) {
                paths.Add(path);
                path = GetDirectoryName(path);
            }
            paths.Reverse();
            foreach (var p in paths) {
                if (!p.Contains("/")) {
                    // All first level paths are not considered part of the asset database ("Assets", "Packages")
                    continue;
                }

                var exists = Directory.Exists(p);
                if (AssetDatabase.IsValidFolder(p)) {
                    if (!exists) Directory.CreateDirectory(p);
                    continue;
                }

                if (exists) {
                    // Unity may still be waiting to import a folder or the assets we saved inside it.
                    if (pendingSavePaths.Values.Any(pending => IsPathAtOrBelow(pending, p))) continue;
                    Directory.Delete(p, true);
                }

                if (File.Exists(p + ".meta")) {
                    // Delete the meta too, because the assetdb is going to try to write a new one when we "create" the new folder
                    File.Delete(p + ".meta");
                }

                var parent = GetDirectoryName(p);
                if (string.IsNullOrEmpty(parent)) continue;
                var basename = Path.GetFileName(p);
                var guid = AssetDatabase.CreateFolder(parent, basename);
                if (string.IsNullOrEmpty(guid)) {
                    throw new Exception("Failed to create directory " + p);
                }
                if (assetEditing) pendingSavePaths[guid] = p;
            }
        }

        public static Tuple<string, long> ParseId(string id) {
            if (!string.IsNullOrWhiteSpace(id)) {
                var split = id.Split(':');
                if (split.Length == 2) {
                    var guid = split[0];
                    var fileID = long.Parse(split[1]);
                    return Tuple.Create(guid, fileID);
                }
            }
            return null;
        }

        public static T FindAsset<T>(string id) where T : Object {
            var parsed = ParseId(id);
            if (parsed == null) return null;
            return FindAsset<T>(parsed.Item1, parsed.Item2);
        }
        
        public static T FindAsset<T>(string guid, long fileID) where T : Object {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null) return null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                if (!(asset is T t)) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid_, out long fileID_)) continue;
                if (guid_ != guid) continue;
                if (fileID_ != fileID) continue;
                return t;
            }
            
            // Sometimes the fileId of the main animator controller in a file changes randomly, so if the main asset
            // in the file is the right type, just assume it's the one we're looking for.
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            if (main is T mainT) {
                return mainT;
            }

            return null;
        }

        public static T LoadAssetByGuid<T>(string guid) where T : Object {
            if (guid.IsEmpty()) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IsEmpty()) return null;
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static void DeleteDirIfEmpty(string path) {
            if (string.IsNullOrEmpty(path)) return;
            if (!AssetDatabase.IsValidFolder(path)) return;
            if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any()) return;
            Delete(path);
        }
    }
}
