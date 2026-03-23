using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.PackageManager;
using VF.Builder;
using VF.Utils;

namespace VF {
    internal static class TmpFilePackage {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Action Resolve = typeof(Client).GetMatchingDelegate<Action>("Resolve");
        }

        private const string TmpDirPath = "Packages/com.vrcfury.temp";
        private const string TmpPackagePath = TmpDirPath + "/" + "package.json";

        public static void Cleanup(ISet<string> usedFolders = null) {
            var tmpDir = GetPathNullable();
            if (tmpDir == null) return;

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                VRCFuryAssetDatabase.DeleteFiltered(tmpDir, path => {
                    if (usedFolders != null && usedFolders.Any(used => path.StartsWith($"{used}/") || path == used || used.StartsWith($"{path}/"))) return false;
                    if (path.StartsWith(tmpDir + "/SPS")) return false;
                    if (path.StartsWith(tmpDir + "/XR")) return false;
                    if (path.StartsWith(tmpDir + "/package.json")) return false;
                    if (path.StartsWith(tmpDir + "/PlayModeSettings")) return false;
                    if (path.StartsWith(tmpDir + "/LegacyPrefabsImported")) return false;
                    return true;
                });
                if (AssetDatabase.IsValidFolder("Assets/_VRCFury")) {
                    VRCFuryAssetDatabase.Delete("Assets/_VRCFury");
                }
            });
            // If we don't disable asset editing temporarily, the asset database does WEIRD things,
            // like showing that the deleted directories still exist, and reusing data from the
            // assets that used to be in those folders
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {});
        }

        [CanBeNull]
        public static string GetPathNullable() {
            if (!AssetDatabase.IsValidFolder(TmpDirPath)) return null;
            return TmpDirPath;
        }

        public static string GetPath() {
            var tmpDir = GetPathNullable();
            if (tmpDir == null) throw new Exception("VRCFury Temp Files package has not been created yet. Try again?");
            return tmpDir;
        }

        private static void InitIfMissing() {
            if (GetPathNullable() != null) return;

            if (!Directory.Exists(TmpDirPath)) {
                Directory.CreateDirectory(TmpDirPath); 
            }

            if (!File.Exists(TmpPackagePath) ||
                Encoding.UTF8.GetString(File.ReadAllBytes(TmpPackagePath)) != PackageJson) {
                File.WriteAllBytes(TmpPackagePath, Encoding.UTF8.GetBytes(PackageJson));

                EditorApplication.delayCall += ReresolvePackages;
            }
        }

        private static void ReresolvePackages() {
            Reflection.Resolve?.Invoke();
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            Scheduler.Schedule(InitIfMissing, 5000);
        }

        private static readonly string PackageJson =
            "{\n" +
            "\"name\": \"com.vrcfury.temp\",\n" +
            "\"displayName\": \"VRCFury Temp Files\",\n" +
            "\"version\": \"0.0.0\",\n" +
            "\"hideInEditor\": false,\n" +
            "\"author\": { \"name\": \"VRCFury\" }\n" +
            "}";
    }
}
