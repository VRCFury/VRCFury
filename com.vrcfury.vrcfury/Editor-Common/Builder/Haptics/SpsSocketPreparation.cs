using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Utils;
using static VF.Builder.Haptics.DpsUtils;

namespace VF.Builder.Haptics {
    internal static class SpsSocketPreparation {
        public static Light[] GetLegacySocketLights(VFGameObject obj, bool directOnly = false) {
            var lights = obj.Children().SelectMany(child => child.GetComponents<Light>());
            if (!directOnly) lights = lights.Concat(obj.GetComponentsInSelfAndChildren<Light>());
            return lights.Distinct()
                .Where(light => GetLegacyDpsLightType(light) is LegacyDpsLightType.Hole
                    or LegacyDpsLightType.Ring or LegacyDpsLightType.Front).ToArray();
        }

        public static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLights(
            VFGameObject obj, bool directOnly = false) => GetInfoFromLights(obj, GetLegacySocketLights(obj, directOnly));

        private static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLights(
            VFGameObject obj, IEnumerable<Light> lights) {
            Light main = null;
            Light front = null;
            var type = VRCFuryHapticSocket.AddLight.Hole;
            foreach (var light in lights) {
                var lightType = GetLegacyDpsLightType(light);
                if (main == null && (lightType == LegacyDpsLightType.Hole || lightType == LegacyDpsLightType.Ring)) {
                    main = light;
                    type = lightType == LegacyDpsLightType.Hole
                        ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.Ring;
                }
                if (front == null && lightType == LegacyDpsLightType.Front) front = light;
            }
            if (main == null || front == null) return null;
            var position = obj.InverseTransformPoint(main.transform.position);
            var forward = obj.InverseTransformPoint(front.transform.position) - position;
            return Tuple.Create(type, position, Quaternion.LookRotation(forward.normalized));
        }

        // Capture every socket before deleting any shared legacy inputs.
        public static void ImportSettingsFromLegacySystems(IEnumerable<VRCFuryHapticSocket> sockets, bool deleteLights) {
            var lights = new HashSet<Light>();
            foreach (var socket in sockets) {
                var inputs = GetLegacySocketLights(socket.owner());
                ImportSettingsFromLegacySystems(socket, inputs);
                lights.UnionWith(inputs);
            }
            if (deleteLights) {
                foreach (var light in lights) light.Destroy();
            }
        }

        private static void ImportSettingsFromLegacySystems(VRCFuryHapticSocket socket, IReadOnlyCollection<Light> lights) {
            if (socket.addLight != VRCFuryHapticSocket.AddLight.None) return;
            var info = GetInfoFromLights(socket.owner(), lights);
            if (info == null) return;
            socket.addLight = info.Item1;
            socket.position = info.Item2;
            socket.rotation = info.Item3.eulerAngles;
        }

    }
}
