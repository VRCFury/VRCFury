using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using VF.Builder;

namespace VF {
    internal static class TmpFilePackage {
        private const string TmpDirPath = "Packages/com.vrcfury.temp";
        private const string TmpPackagePath = TmpDirPath + "/" + "package.json";
        private const string LegacyTmpDirPath = "Assets/_VRCFury";
        private const string LegacyPrefabsImportedMarker = TmpDirPath + "/LegacyPrefabsImported";

        public static void Cleanup() {
            var tmpDir = GetPath();
            VRCFuryAssetDatabase.DeleteFolder(tmpDir, path => {
                if (path.StartsWith(tmpDir + "/SPS")) return false;
                if (path.StartsWith(tmpDir + "/package.json")) return false;
                if (path.StartsWith(tmpDir + "/LegacyPrefabsImported")) return false;
                return true;
            });
            VRCFuryAssetDatabase.DeleteFolder("Assets/_VRCFury");
        }

        public static string GetPath() {
            var importLegacyPrefabs = false;
            if ((Directory.Exists(LegacyTmpDirPath) || Directory.Exists(TmpDirPath)) &&
                !File.Exists(LegacyPrefabsImportedMarker)) {
                importLegacyPrefabs = true;
            }

            if (!Directory.Exists(TmpDirPath)) {
                Directory.CreateDirectory(TmpDirPath); 
                File.Create(LegacyPrefabsImportedMarker).Close();
            }

            if (!File.Exists(TmpPackagePath) ||
                Encoding.UTF8.GetString(File.ReadAllBytes(TmpPackagePath)) != PackageJson) {
                File.WriteAllBytes(TmpPackagePath, Encoding.UTF8.GetBytes(PackageJson));

                EditorApplication.delayCall += ReresolvePackages;
            }

            if (importLegacyPrefabs) {
                LegacyPrefabUnpacker.ScanOnce();
                File.Create(LegacyPrefabsImportedMarker).Close();
            }

            return TmpDirPath;
        }

        private static void ReresolvePackages() {
            var method = typeof(Client).GetMethod("Resolve",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new Type[] {},
                null
            );
            method.Invoke(null, null);
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            GetPath();
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
