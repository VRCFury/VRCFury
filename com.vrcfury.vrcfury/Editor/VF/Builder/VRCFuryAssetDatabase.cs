using System;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public static class VRCFuryAssetDatabase {
        public static string MakeFilenameSafe(string str) {
            var output = "";
            foreach (var c in str) {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '.') {
                    output += c;
                } else {
                    output += '_';
                }
            }
            
            if (output.Length > 64) output = output.Substring(0, 64);

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

        public static string GetUniquePath(string dir, string filename, string ext) {
            var safeFilename = MakeFilenameSafe(filename);

            string fullPath;
            for (var i = 0;; i++) {
                fullPath = dir
                           + "/"
                           + safeFilename + (i > 0 ? "_" + i : "")
                           + (filename.Contains("(VF_1_G_BAKED)") ? "(VF_1_G_BAKED)" : "")
                           + (ext != "" ? "." + ext : "");
                if (!File.Exists(fullPath)) break;
            }
            return fullPath;
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
            // If object was already part of another asset, or was recently deleted, we MUST
            // call this first, or unity will throw an exception
            AssetDatabase.RemoveObjectFromAsset(obj);
            AssetDatabase.CreateAsset(obj, fullPath);
        }

        private static bool assetEditing = false;
        public static void WithAssetEditing(Action go) {
            if (!assetEditing) {
                AssetDatabase.StartAssetEditing();
                assetEditing = true;
                try {
                    go();
                } finally {
                    AssetDatabase.StopAssetEditing();
                    assetEditing = false;
                }
            } else {
                go();
            }
        }

        public static void WithoutAssetEditing(Action go) {
            if (assetEditing) {
                AssetDatabase.StopAssetEditing();
                assetEditing = false;
                try {
                    go();
                } finally {
                    AssetDatabase.StartAssetEditing();
                    assetEditing = true;
                }
            } else {
                go();
            }
        }

        /** In case you're running code that counts on the system locale being standardized... */
        public static void WithStandardizedLocale(Action go) {
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            var oldUICulture = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            try {
                go();
            } finally {
                Thread.CurrentThread.CurrentCulture = oldCulture;
                Thread.CurrentThread.CurrentUICulture = oldUICulture;
            }
        }

        public static void DeleteFolder(string path) {
            if (Directory.Exists(path)) {
                foreach (var asset in AssetDatabase.FindAssets("", new[] { path })) {
                    var assetPath = AssetDatabase.GUIDToAssetPath(asset);
                    AssetDatabase.DeleteAsset(assetPath);
                }
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
    }
}
