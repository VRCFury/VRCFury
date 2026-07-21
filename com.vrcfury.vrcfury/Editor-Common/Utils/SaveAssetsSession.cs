using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils {
    internal class SaveAssetsSession {
        private readonly HashSet<Object> workLogManifest = new HashSet<Object>();
        private readonly HashSet<UnityEngine.Component> scannedComponents = new HashSet<UnityEngine.Component>();
        private readonly HashSet<Object> savedRootAssets = new HashSet<Object>();

        public void SaveUnsavedComponentAssets(UnityEngine.Component component, string tmpDir) {
            if (!scannedComponents.Add(component)) return;
            foreach (var asset in GetUnsavedChildren(component, false)) {
                string filename;
                if (asset.GetType().Name == "VRCExpressionsMenu") {
                    filename = "VRCFury Menu";
                } else if (asset.GetType().Name == "VRCExpressionParameters") {
                    filename = "VRCFury Params";
                } else {
                    filename = $"VRCFury {asset.name} - {component.owner().name}";
                }
                SaveAssetAndChildren(
                    asset,
                    filename,
                    tmpDir
                );
            }
        }

        private static IList<Object> GetUnsavedChildren(Object obj, bool recurse) {
            var unsavedChildren = new List<Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset is AnimatorController) return false;
                if (asset == obj) return true;
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (!VrcfObjectFactory.DidCreate(asset)) return false;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) return true;
                unsavedChildren.Add(asset);
                return recurse;
            });
            return unsavedChildren;
        }

        public void SaveAssetAndChildren(Object asset, string filename, string tmpDir) {
            SaveAssetAndChildren(asset, GetUnsavedChildren(asset, true), filename, tmpDir);
        }

        public void SaveAssetAndChildren(
            Object asset,
            IEnumerable<Object> children,
            string filename,
            string tmpDir
        ) {
            if (!VrcfObjectFactory.DidCreate(asset)) return;
            if (!savedRootAssets.Add(asset)) return;

            var unsavedChildren = children
                .Where(child => child != null)
                .Where(VrcfObjectFactory.DidCreate)
                .Where(child => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(child)))
                .Distinct()
                .ToList();

            // Save child textures
            // If we don't save textures before the materials that use them, unity just throws them away
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) {
                    VRCFuryAssetDatabase.SaveAsset(subAsset, tmpDir, filename + "_" + subAsset.name);
                    RecordWorkLog(subAsset);
                }
            }

            // Save the main asset
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) {
                VRCFuryAssetDatabase.SaveAsset(asset, tmpDir, filename);
            }
            RecordWorkLog(asset);

            // Attach children
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) continue;
                if (subAsset is AnimatorStateMachine
                    || subAsset is AnimatorState
                    || subAsset is AnimatorTransitionBase
                    || subAsset is StateMachineBehaviour
                    || subAsset is BlendTree
                   ) {
                    subAsset.hideFlags |= HideFlags.HideInHierarchy;
                }

                VRCFuryAssetDatabase.AttachAsset(subAsset, asset);
                RecordWorkLog(subAsset);
            }
        }

        private void RecordWorkLog(Object obj) {
            if (obj is AnimatorTransitionBase
                || obj is Motion
                || obj is AnimatorState
                || obj is StateMachineBehaviour
                || obj is AnimatorStateMachine
                || obj is AvatarMask) {
                // Nobody cares about where these came from
                return;
            }

            var workLog = obj.GetWorkLog();
            if (workLog.Count <= 0) return;

            var outputPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(outputPath)) return;

            workLogManifest.Add(obj);
        }

        public void FlushWorkLogManifest(string outputDir) {
            WriteWorkLogManifest(outputDir, workLogManifest);
            workLogManifest.Clear();
        }

        private static void WriteWorkLogManifest(string outputDir, IEnumerable<Object> manifestEntries) {
            VRCFuryAssetDatabase.CreateFolder(outputDir);

            var manifestPath = outputDir + "/_ work-log.txt";
            var builder = new StringBuilder();
            foreach (var entry in manifestEntries
                         .Where(entry => entry != null)
                         .Select(entry => (obj: entry, workLog: entry.GetWorkLog()))
                         .Where(entry => entry.workLog.Count > 0)
                         .OrderBy(entry => entry.obj.GetPathAndName())) {
                builder.AppendLine($"{entry.obj.GetPathAndName()}:");

                foreach (var item in entry.workLog) {
                    builder.AppendLine($"* {item}");
                }
                builder.AppendLine();
            }
            builder.AppendLine();

            File.AppendAllText(manifestPath, builder.ToString());
            AssetDatabase.ImportAsset(manifestPath);
        }
    }
}
