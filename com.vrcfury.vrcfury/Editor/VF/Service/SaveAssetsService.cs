using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Service {
    [VFService]
    internal class SaveAssetsService {
        private static readonly HashSet<Object> workLogManifest = new HashSet<Object>();

        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly TmpDirService tmpDirService;

        [FeatureBuilderAction(FeatureOrder.SaveAssets)]
        public void Run() {
            // This works without WithoutAssetEditing in <2022, but in unity 6+, saving an asset during AssetEditing
            // causes the Asset Path to never show up until AssetEditing is ended, which breaks NeedsSaved and
            // AttachAsset
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                var tmpDir = tmpDirService.GetTempDir();

                // Save mats and meshes
                foreach (var component in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                    SaveUnsavedComponentAssets(component, tmpDir);
                }

                // Special handling for mask and controller names
                foreach (var controller in controllers.GetAllMutatedControllers()) {
                    foreach (var layer in controller.GetLayers()) {
                        if (layer.mask != null) {
                            layer.mask.name = "Mask for " + layer.name;
                        }
                    }

                    SaveAssetAndChildren(
                        controller.GetRaw(),
                        $"VRCFury {controller.GetType().ToString()}",
                        tmpDir,
                        true
                    );
                }

                // Save everything else
                foreach (var component in avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                    SaveUnsavedComponentAssets(component, tmpDir);
                }

                FlushWorkLogManifest(tmpDir);
            });
        }

        public static void SaveUnsavedComponentAssets(UnityEngine.Component component, string tmpDir) {
            foreach (var asset in GetUnsavedChildren(component, false, true)) {
                string filename;
                if (asset is VRCExpressionsMenu) {
                    filename = "VRCFury Menu";
                } else if (asset is VRCExpressionParameters) {
                    filename = "VRCFury Params";
                } else {
                    filename = $"VRCFury {asset.name} - {component.owner().name}";
                }
                SaveAssetAndChildren(
                    asset,
                    filename,
                    tmpDir,
                    true
                );
            }
        }

        private static IList<Object> GetUnsavedChildren(Object obj, bool recurse, bool reuseOriginalClips) {
            var unsavedChildren = new List<Object>();
            var clipReplacements = new Dictionary<Object, Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset == obj) return true;
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (!VrcfObjectFactory.DidCreate(asset)) return false;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) return true;
                if (asset is AnimationClip vac) {
                    if (reuseOriginalClips) {
                        var useOriginalClip = vac.GetUseOriginalUserClip();
                        if (useOriginalClip != null) {
                            clipReplacements[vac] = useOriginalClip;
                            return false;
                        }
                    }
                    vac.FinalizeAsset();
                }
                unsavedChildren.Add(asset);
                return recurse;
            });
            if (clipReplacements.Count > 0) {
                foreach (var o in unsavedChildren) {
                    MutableManager.RewriteInternals(o, clipReplacements);
                }
            }
            foreach (var o in unsavedChildren) {
                if (o is AnimationClip clip) {
                    // There's a bug in unity where if you change the reference pose using SerializedObject, sometimes it won't update
                    // unity's internal cache and it'll blow up. This forces the cache to refresh. This bug still exists as of Unity 6.2.
                    AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(clip));
                }
            }
            return unsavedChildren;
        }

        public static void SaveAssetAndChildren(Object asset, string filename, string tmpDir, bool reuseOriginalClips) {
            if (!VrcfObjectFactory.DidCreate(asset)) return;

            var unsavedChildren = GetUnsavedChildren(asset, true, reuseOriginalClips);

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

        private static void RecordWorkLog(Object obj) {
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

        public static void FlushWorkLogManifest(string outputDir) {
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
