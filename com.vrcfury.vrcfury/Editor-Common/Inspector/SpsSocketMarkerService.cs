using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Utils;
using static VF.Inspector.VRCFuryHapticSocketEditor;

namespace VF.Inspector {
    [VFService]
    internal class SpsSocketMarkerService {
        [VFAutowired] private readonly SpsMarkersService spsMarkers;
        [VFAutowired] private readonly SpsConfigurer spsConfigurer;

        public ScreenMarkerResult Create(
            VFGameObject parent,
            VRCFuryHapticSocket socket,
            VRCFuryHapticSocket.AddLight lightType,
            uint socketId,
            bool useRadiusOffset,
            bool useTangentIn = false,
            Vector3 tangentIn = default,
            bool useTangentOut = false,
            Vector3 tangentOut = default,
            uint nextSocketId = 0,
            bool includeTags = true,
            string objectName = "SpsScreenMarker"
        ) {
            if (!BuildTargetUtils.IsDesktop()) return null;
            if (lightType == VRCFuryHapticSocket.AddLight.None) return null;

            var screenMarker = GameObjects.Create(objectName, parent);
            screenMarker.AddComponent<MeshFilter>();
            var meshRenderer = screenMarker.AddComponent<MeshRenderer>();
            spsMarkers.ConfigureSocketRenderer(meshRenderer);
            screenMarker.AddComponent<VRCFuryHideGizmoUnlessSelected>();
            screenMarker.AddComponent<VRCFurySpsGreenScreenFix>();
            return new ScreenMarkerResult {
                obj = screenMarker,
                renderer = meshRenderer,
                materialProperties = spsConfigurer.GetSocketProperties(
                    meshRenderer, socket, lightType, socketId, useTangentIn, tangentIn,
                    useTangentOut, tangentOut, useRadiusOffset, nextSocketId, includeTags
                )
            };
        }
    }
}
