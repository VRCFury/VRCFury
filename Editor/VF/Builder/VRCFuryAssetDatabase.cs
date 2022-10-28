using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Builder {
    public class VRCFuryAssetDatabase {
        public static string MakeFilenameSafe(string str) {
            var output = "";
            foreach (var c in str) {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '.') {
                    output += c;
                } else {
                    output += '_';
                }
            }
            output = output.Trim();
            if (output.Length > 64) output = output.Substring(0, 32);
            if (output.Length == 0) output = "Unknown";
            return output;
        }

        public static string GetUniquePath(string dir, string filename, string ext) {
            var safeFilename = MakeFilenameSafe(filename);

            string fullPath;
            for (int i = 0;; i++) {
                fullPath = dir + "/" + safeFilename + (i > 0 ? "_" + i : "") + (ext != "" ? "." + ext : "");
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
        
        public static T CopyAsset<T>(T obj, string toPath) where T : Object {
            AssetDatabase.StopAssetEditing();
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(obj), toPath)) {
                throw new VRCFBuilderException("Failed to copy asset " + obj);
            }

            var copy = AssetDatabase.LoadAssetAtPath<T>(toPath);
            AssetDatabase.StartAssetEditing();
            if (copy == null) {
                throw new VRCFBuilderException("Failed to load copy asset " + obj);
            }
            return copy;
        }
    }
}
