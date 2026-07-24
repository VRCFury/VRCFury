using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Exceptions;
using VF.Inspector;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsBakeAndSave {
        public static void Run(
            IList<VRCFuryHapticSocket> sockets,
            IList<VRCFuryHapticPlug> plugs
        ) {
            if (sockets.Count == 0 && plugs.Count == 0) return;

            var spsMarkers = new SpsMarkersService();
            var saveSession = new SaveAssetsSession("SPS");

            foreach (var socket in sockets) {
                socket.Upgrade();
                try {
                    var result = VRCFuryHapticSocketEditor.Bake(socket, spsMarkers);
                    if (result == null) continue;
                    SpsConfigurer.AddMaterialPropertyAnimator(
                        result.screenMarkerResults.Select(marker => marker.materialProperties).SelectMany(properties => properties),
                        saveSession
                    );
                    foreach (var component in result.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    foreach (var component in result.screenMarkers.SelectMany(marker => marker.GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    VRCFuryHideGizmoUnlessSelectedExtensions.Hide(result.bakeRoot);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Socket: {socket.owner().GetPath()}", e);
                } finally {
                    UnityEngine.Object.DestroyImmediate(socket);
                }
            }

            foreach (var plug in plugs) {
                plug.Upgrade();
                try {
                    var result = VRCFuryHapticPlugEditor.Bake(plug, spsMarkers: spsMarkers);
                    if (result == null) continue;
                    if (result.resolverMaterialProperties != null) {
                        SpsConfigurer.AddMaterialPropertyAnimator(result.resolverMaterialProperties, saveSession);
                    }
                    foreach (var component in result.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    foreach (var component in result.renderers.SelectMany(renderer => renderer.renderer.owner().GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    VRCFuryHideGizmoUnlessSelectedExtensions.Hide(result.bakeRoot);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Plug: {plug.owner().GetPath()}", e);
                } finally {
                    UnityEngine.Object.DestroyImmediate(plug);
                }
            }

            saveSession.Finish();
        }
    }
}
