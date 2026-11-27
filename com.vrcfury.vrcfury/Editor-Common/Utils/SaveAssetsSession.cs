using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Hooks.UnityFixes;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal class SaveAssetsSession {
        private const int BinaryClipMinKeyCount = 4096;
        private readonly string outputDir;
        private readonly Lazy<BinaryContainer> otherParent;
        private readonly Dictionary<Object, Object> workLogManifest = new Dictionary<Object, Object>();
        private readonly List<(string title, string body)> additionalSections = new();
        private readonly List<string> notes = new();
        private readonly HashSet<Object> assetsToSave = new HashSet<Object>();
        private readonly HashSet<Object> otherAssetsToSave = new HashSet<Object>();
        private readonly HashSet<string> savedMaterialGuids = new HashSet<string>();

        public SaveAssetsSession(string outputDirName) {
            outputDir = VRCFuryAssetDatabase.GetUniquePath(
                TmpFilePackage.GetPath() + "/Builds",
                outputDirName,
                startMaxLen: 16
            );
            otherParent = new Lazy<BinaryContainer>(() => {
                var parent = VrcfObjectFactory.Create<BinaryContainer>();
                parent.name = "VRCFury Other";
                return parent;
            });
        }

        public void SaveAssetAndChildren(Object root) {
            otherAssetsToSave.UnionWith(GetUnsavedChildren(root, otherAssetsToSave));

#if UNITY_6000_0_OR_NEWER
            if (root is SkinnedMeshRenderer skin && VrcfObjectFactory.DidCreate(skin.sharedMesh)) {
                Unity6RendererFixHook.Register(skin);
            }
#endif
        }

        private void AttachAsset(Object child, Object parent) {
            if (IsControllerInternalAsset(child)) child.hideFlags |= HideFlags.HideInHierarchy;
            VRCFuryAssetDatabase.AttachAsset(child, parent);
            RecordSavedAsset(child, parent);
            assetsToSave.Add(parent);
        }

        private void RecordSavedAsset(Object child, Object parent) {
            if (child is Material && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(child, out var guid, out long _)) {
                savedMaterialGuids.Add(guid);
            }
            RecordWorkLog(child, parent);
        }

        private static IEnumerable<Object> GetUnsavedChildren(Object obj, ISet<Object> alreadyQueued) {
            var unsavedChildren = new List<Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset is AnimatorController || asset is GameObject || asset is UnityEngine.Component) {
                    if (asset == obj) return true;
                    return false;
                }
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (!VrcfObjectFactory.DidCreate(asset)) return false;
                if (alreadyQueued.Contains(asset)) return false;
                if (asset.IsSaved()) return true;
                unsavedChildren.Add(asset);
                return true;
            });
            return unsavedChildren.Distinct();
        }

        public void SaveControllerAndChildren(
            AnimatorController controller,
            IEnumerable<Object> children,
            IEnumerable<Object> otherAssets,
            long clipKeyCount
        ) {
            if (!VrcfObjectFactory.DidCreate(controller)) return;
            SaveParent(controller);
            var clips = new HashSet<Object>();
            foreach (var asset in children) {
                // A second asset file is worthwhile when the animation keys would make the controller large.
                if (asset is AnimationClip && clipKeyCount >= BinaryClipMinKeyCount) clips.Add(asset);
                else AttachAsset(asset, controller);
            }
            // Save objects used by animation keyframes before serializing the clips that reference them.
            foreach (var asset in otherAssets) {
                foreach (var child in GetUnsavedChildren(asset, clips)) AttachAsset(child, controller);
            }
            if (clips.Count > 0) {
                var parent = VrcfObjectFactory.Create<BinaryContainer>();
                parent.name = controller.name + " Clips";
                SaveBinaryFile(parent, clips);
            }
        }

        private void SaveBinaryFile(BinaryContainer parent, IReadOnlyCollection<Object> children) {
            var path = VRCFuryAssetDatabase.GetUniquePath(outputDir, parent.name, "asset");
            VRCFuryAssetDatabase.SaveAsset(parent, path, children);
            RecordWorkLog(parent, parent);
            foreach (var child in children) RecordSavedAsset(child, parent);
        }

        private static bool IsControllerInternalAsset(Object asset) {
            return asset is AnimatorStateMachine
                   || asset is AnimatorState
                   || asset is AnimatorTransitionBase
                   || asset is StateMachineBehaviour
                   || asset is BlendTree;
        }

        private void SaveParent(Object parent) {
            if (parent == null || !VrcfObjectFactory.DidCreate(parent)) return;
            if (!parent.IsSaved()) {
                VRCFuryAssetDatabase.SaveAsset(parent, outputDir, parent.name);
                assetsToSave.Add(parent);
            }
            RecordWorkLog(parent, parent);
        }

        private void RecordWorkLog(Object obj, Object parent) {
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

            workLogManifest[obj] = parent;
        }

        public void AddWorkLogSection(string title, string body) {
            additionalSections.Add((title, body));
        }

        public void AddNote(string note) {
            notes.Add(note);
        }

        public void WriteWorkLogManifest() {
            if (workLogManifest.Count == 0 && additionalSections.Count == 0 && notes.Count == 0) return;
            VRCFuryAssetDatabase.CreateFolder(outputDir);

            var manifestPath = VRCFuryAssetDatabase.GetUniquePath(outputDir, "_ work-log", "txt");
            var builder = new StringBuilder();
            // GetAssetPath is expensive on subassets in large generated files. Query each owner once.
            var parentPaths = new Dictionary<Object, string>();
            string GetName(Object obj, Object parent) {
                if (!parentPaths.TryGetValue(parent, out var path)) {
                    path = VRCFuryAssetDatabase.GetAssetPath(parent);
                    parentPaths[parent] = path;
                }
                return obj == parent ? path : $"{path} ({obj.name})";
            }
            foreach (var entry in workLogManifest
                         .Where(entry => entry.Key != null)
                         .Select(entry => (name: GetName(entry.Key, entry.Value), workLog: entry.Key.GetWorkLog()))
                         .Where(entry => entry.workLog.Count > 0)
                         .OrderBy(entry => entry.name)) {
                builder.AppendLine($"{entry.name}:");

                foreach (var item in entry.workLog) {
                    builder.AppendLine($"* {item}");
                }
                builder.AppendLine();
            }
            foreach (var (title, body) in additionalSections) {
                builder.AppendLine($"{title}:");
                builder.AppendLine(body);
                builder.AppendLine();
            }
            if (notes.Count > 0) {
                builder.AppendLine("Notes:");
                foreach (var note in notes) builder.AppendLine($"* {note}");
                builder.AppendLine();
            }
            builder.AppendLine();

            File.WriteAllText(manifestPath, builder.ToString());
            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);
            workLogManifest.Clear();
            additionalSections.Clear();
            notes.Clear();
        }

        public void FlushAssets() {
            var otherAssets = otherAssetsToSave.Where(asset => !asset.IsSaved()).ToArray();
            if (otherAssets.Length > 0) {
                var parent = otherParent.Value;
                if (parent.IsSaved()) {
                    foreach (var asset in otherAssets) AttachAsset(asset, parent);
                } else {
                    SaveBinaryFile(parent, otherAssets);
                }
            }
            otherAssetsToSave.Clear();
            foreach (var asset in assetsToSave) {
                VRCFuryAssetDatabase.SaveAssetIfDirty(asset);
            }
            assetsToSave.Clear();
            PoiyomiUtils.AddToKnownMaterials(savedMaterialGuids);
            savedMaterialGuids.Clear();
        }
    }
}
