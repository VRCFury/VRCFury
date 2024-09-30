using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.VersionControl;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using Object = System.Object;

namespace VF {
    internal static class TmpFilePackage {
        private const string TmpDirPath = "Packages/com.vrcfury.temp";
        private const string TmpPackagePath = TmpDirPath + "/" + "package.json";
        private const string LegacyTmpDirPath = "Assets/_VRCFury";
        private const string LegacyPrefabsImportedMarker = TmpDirPath + "/LegacyPrefabsImported";

        public static void Cleanup() {
            var usedFolders = Resources.FindObjectsOfTypeAll<VRCAvatarDescriptor>()
                .SelectMany(VRCAvatarUtils.GetAllControllers)
                .Where(c => !c.isDefault && c.controller != null)
                .Select(c => AssetDatabase.GetAssetPath(c.controller))
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(VRCFuryAssetDatabase.GetDirectoryName)
                .ToImmutableHashSet();

            var tmpDir = GetPath();
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                VRCFuryAssetDatabase.DeleteFiltered(tmpDir, path => {
                    if (usedFolders.Any(used => path.StartsWith($"{used}/") || path == used || used.StartsWith($"{path}/"))) return false;
                    if (path.StartsWith(tmpDir + "/SPS")) return false;
                    if (path.StartsWith(tmpDir + "/XR")) return false;
                    if (path.StartsWith(tmpDir + "/package.json")) return false;
                    if (path.StartsWith(tmpDir + "/LegacyPrefabsImported")) return false;
                    return true;
                });
                VRCFuryAssetDatabase.Delete("Assets/_VRCFury");
            });
            // If we don't disable asset editing temporarily, the asset database does WEIRD things,
            // like showing that the deleted directories still exist, and reusing data from the
            // assets that used to be in those folders
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {});
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
