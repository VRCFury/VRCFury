using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public static class VRCFuryAssetDatabase {
        public static string MakeFilenameSafe(string str) {
            var output = "";
            foreach (var c in str) {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '.' || c == '(' || c == ')') {
                    output += c;
                } else {
                    output += '_';
                }
            }
            
            if (output.Length > 64) output = output.Substring(0, 32) + output.Substring(output.Length - 32);

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
            for (int i = 0;; i++) {
                fullPath = dir
                           + "/"
                           + safeFilename + (i > 0 ? "_" + i : "")
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
    }
}
