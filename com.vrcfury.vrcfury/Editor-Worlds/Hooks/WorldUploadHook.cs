using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Component;
using VF.Exceptions;
using VF.Inspector;
using VF.Menu;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    internal class WorldUploadHook : VrcfWorldPreprocessor {
        protected override int order => -10000;

        protected override void Process(Scene scene) {
            if (Application.isPlaying && !PlayModeMenuItem.Get()) return;
            if (IsActuallyUploadingWorldHook.Get() && !UseInUploadMenuItem.Get()) return;

            var obj = new GameObject("VRCFury ran!");
            SceneManager.MoveGameObjectToScene(obj, scene);

            foreach (var root in VFGameObject.GetRoots(scene)) {
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
