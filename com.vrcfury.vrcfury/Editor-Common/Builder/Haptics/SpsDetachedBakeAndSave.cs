using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Exceptions;
using VF.Hooks.UnityFixes;
using VF.Injector;
using VF.Inspector;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsDetachedBakeAndSave {
        public static void Run(
            IList<VRCFuryHapticSocket> sockets,
            IList<VRCFuryHapticPlug> plugs,
            Action<VRCFuryHapticSocket, VRCFuryHapticSocketEditor.BakeResult> onSocketBaked = null,
            Action<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult> onPlugBaked = null
        ) {
            if (!BuildTargetUtils.IsDesktop()) return;
            if (sockets.Count == 0 && plugs.Count == 0) return;

            foreach (var socket in sockets) socket.Upgrade();
            foreach (var plug in plugs) plug.Upgrade();
            SpsPlugPreparation.ImportSettingsFromLegacySystems(plugs);
            SpsSocketPreparation.ImportSettingsFromLegacySystems(sockets, deleteLights: true);

            var injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
            var plugBaker = injector.GetService<VRCFuryHapticPlugBaker>();
            var socketBaker = injector.GetService<VRCFuryHapticSocketBaker>();
            var saveSession = new SaveAssetsSession("SPS");

            foreach (var socket in sockets) {
                try {
                    var result = socketBaker.Bake(socket);
                    onSocketBaked?.Invoke(socket, result);
                    injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
                    foreach (var component in result.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    VRCFuryHideGizmoUnlessSelectedExtensions.Hide(result.bakeRoot);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build SPS Socket: {socket.owner().GetDebugPath()}", e);
                } finally {
                    UnityEngine.Object.DestroyImmediate(socket);
                }
            }

            foreach (var plug in plugs) {
                try {
                    var result = plugBaker.Bake(plug);
                    onPlugBaked?.Invoke(plug, result);
                    injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
                    Unity6RendererFixHook.Register(result.bakeRoot);
                    foreach (var component in result.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    foreach (var component in result.renderers.SelectMany(renderer => renderer.renderer.owner().GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                        saveSession.SaveAssetAndChildren(component);
                    }
                    VRCFuryHideGizmoUnlessSelectedExtensions.Hide(result.bakeRoot);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build SPS Plug: {plug.owner().GetDebugPath()}", e);
                } finally {
                    UnityEngine.Object.DestroyImmediate(plug);
                }
            }

            saveSession.Finish();
        }
    }
}
