using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal class SaveAssetsSession {
        private readonly string outputDir;
        private readonly Lazy<Object> otherParent;
        private readonly HashSet<Object> workLogManifest = new HashSet<Object>();
        private readonly HashSet<Object> assetsToSave = new HashSet<Object>();

        public SaveAssetsSession(string outputDirName) {
            outputDir = VRCFuryAssetDatabase.GetUniquePath(
                TmpFilePackage.GetPath() + "/Builds",
                outputDirName,
                startMaxLen: 16
            );
            otherParent = new Lazy<Object>(() => {
                var parent = VrcfObjectFactory.Create<BinaryContainer>();
                parent.name = "VRCFury Other";
                SaveParent(parent);
                return parent;
            });
        }

        public void SaveAssetAndChildren(Object root, Func<Object> getParent = null, bool recurse = true) {
            IEnumerable<Object> toSave;
            if (recurse) {
                toSave = GetUnsavedChildren(root, true);
            } else {
                toSave = new [] { root };
            }
            var parent = new Lazy<Object>(() => {
                if (getParent == null) return otherParent.Value;
                return getParent();
            });
            foreach (var child in toSave) {
                if (IsControllerInternalAsset(child)) {
                    child.hideFlags |= HideFlags.HideInHierarchy;
                }
                VRCFuryAssetDatabase.AttachAsset(child, parent.Value);
                RecordWorkLog(child);
            }
            // If we don't do this, the attached assets don't show up in the browser
            // until the next time all assets are saved
            if (parent.IsValueCreated) assetsToSave.Add(parent.Value);
        }

        private static IEnumerable<Object> GetUnsavedChildren(Object obj, bool recurse) {
            var unsavedChildren = new List<Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset is AnimatorController || asset is GameObject || asset is UnityEngine.Component) {
                    if (asset == obj) return true;
                    return false;
                }
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (!VrcfObjectFactory.DidCreate(asset)) return false;
                if (!IsUnsaved(asset)) return true;
                unsavedChildren.Add(asset);
                return recurse;
            });
            return unsavedChildren
                .Distinct()
                // If you save a material before a texture it contains, the texture reference disappears
                .OrderBy(child => child is Texture2D ? 0 : 1);
        }

        private static bool IsUnsaved(Object asset) {
            return asset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset));
        }

        public void SaveControllerAndChildren(
            AnimatorController controller,
            IEnumerable<Object> children,
            IEnumerable<Object> otherAssets
        ) {
            if (!VrcfObjectFactory.DidCreate(controller)) return;
            SaveParent(controller);
            foreach (var asset in children) {
                SaveAssetAndChildren(asset, () => controller, false);
            }
            foreach (var asset in otherAssets) {
                SaveAssetAndChildren(asset, () => controller, true);
            }
        }

        private static bool IsControllerInternalAsset(Object asset) {
            return asset is AnimatorStateMachine
                   || asset is AnimatorState
                   || asset is AnimatorTransitionBase
                   || asset is StateMachineBehaviour
                   || asset is BlendTree;
        }

        private void SaveParent(Object parent) {
            if (!VrcfObjectFactory.DidCreate(parent)) return;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(parent))) {
                VRCFuryAssetDatabase.SaveAsset(parent, outputDir, parent.name);
            }
            RecordWorkLog(parent);
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

        private void WriteWorkLogManifest() {
            VRCFuryAssetDatabase.CreateFolder(outputDir);

            var manifestPath = VRCFuryAssetDatabase.GetUniquePath(outputDir, "_ work-log", "txt");
            var builder = new StringBuilder();
            foreach (var entry in workLogManifest
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

        public void Finish() {
            foreach (var asset in assetsToSave) {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            assetsToSave.Clear();
            WriteWorkLogManifest();
            workLogManifest.Clear();
        }
    }
}
