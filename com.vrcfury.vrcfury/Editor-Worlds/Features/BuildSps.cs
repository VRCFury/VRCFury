using System;
using UnityEngine.SceneManagement;
using VF.Component;
using VF.Exceptions;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Features {
    internal static class BuildSps {
        public static void Process(Scene scene) {
            foreach (var root in scene.Roots()) {
                foreach (var socket in root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                    socket.Upgrade();
                    try {
                        var bakeResult = VRCFuryHapticSocketEditor.Bake(socket);
                        VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to bake SPS Socket: {socket.owner().GetPath()}", e);
                    }
                    Object.DestroyImmediate(socket);
                }
                foreach (var plug in root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                    plug.Upgrade();
                    try {
                        var bakeResult = VRCFuryHapticPlugEditor.Bake(plug);
                        if (bakeResult != null) {
                            var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", bakeResult.oscId);
                            var saver = new SaveAssetsSession();
                            foreach (var renderer in bakeResult.renderers) {
                                saver.SaveUnsavedComponentAssets(renderer.renderer, tmpDir);
                            }
                            VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                        }
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to bake SPS Plug: {plug.owner().GetPath()}", e);
                    }
                    Object.DestroyImmediate(plug);
                }
            }
        }
    }
}
