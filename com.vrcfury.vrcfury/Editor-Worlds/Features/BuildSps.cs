using System;
using System.Linq;
using UnityEngine.SceneManagement;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Features {
    internal static class BuildSps {
        public static void Process(Scene scene) {
            var spsMarkers = new SpsMarkersService();
            foreach (var root in scene.Roots()) {
                foreach (var socket in root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                    socket.Upgrade();
                    try {
                        var bakeResult = VRCFuryHapticSocketEditor.Bake(socket, spsMarkers);
                        if (bakeResult != null) {
                            SpsConfigurer.AddMaterialPropertyAnimator(
                                bakeResult.screenMarkerResults.SelectMany(result => result.materialProperties)
                            );
                            var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", "Socket mat");
                            var saver = new SaveAssetsSession();
                            foreach (var c in bakeResult.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
                            foreach (var c in bakeResult.screenMarkers
                                         .SelectMany(c => c.GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
                            VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                        }
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to bake SPS Socket: {socket.owner().GetPath()}", e);
                    }
                    Object.DestroyImmediate(socket);
                }
                foreach (var plug in root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                    plug.Upgrade();
                    try {
                        var bakeResult = VRCFuryHapticPlugEditor.Bake(plug, spsMarkers: spsMarkers);
                        if (bakeResult != null) {
                            if (bakeResult.resolverMaterialProperties != null) {
                                SpsConfigurer.AddMaterialPropertyAnimator(bakeResult.resolverMaterialProperties);
                            }
                            var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", bakeResult.oscId);
                            var saver = new SaveAssetsSession();
                            foreach (var c in bakeResult.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
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
