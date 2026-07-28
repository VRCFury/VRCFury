using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Utils;
using static VF.Inspector.VRCFuryHapticSocketEditor;
using BakeResult = VF.Inspector.VRCFuryHapticSocketEditor.BakeResult;
using ScreenMarkerResult = VF.Inspector.VRCFuryHapticSocketEditor.ScreenMarkerResult;

namespace VF.Inspector {
    internal static class VRCFuryHapticSocketBaker {
        [CanBeNull]
        public static BakeResult Bake(VRCFuryHapticSocket socket, SpsMarkersService spsMarkers) {
            var transform = socket.owner();
            if (!HapticUtils.AssertValidScale(transform, "socket", shouldThrow: !socket.fromSpsForAll)) {
                return null;
            }

            var (lightType, localPosition, localRotation) = GetInfoFromLightsOrComponent(socket);

            var bakeRoot = GameObjects.Create("BakedSpsSocket", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            var oneSpace = GameObjects.Create("OneSpace", bakeRoot);
            oneSpace.worldScale = Vector3.one;

            var worldSpace = GameObjects.Create("WorldSpace", bakeRoot);
            ConstraintUtils.MakeWorldSpace(worldSpace);

            var senders = GameObjects.Create("Senders", worldSpace);

            // Senders
            {
                var rootTags = new List<string>();
                rootTags.Add(HapticUtils.TagTpsOrfRoot);
                rootTags.Add(HapticUtils.TagSpsSocketRoot);
                if (lightType != VRCFuryHapticSocket.AddLight.None && !socket.fromSpsForAll) {
                    switch (lightType) {
                        case VRCFuryHapticSocket.AddLight.Ring:
                            rootTags.Add(HapticUtils.TagSpsSocketIsRing);
                            break;
                        case VRCFuryHapticSocket.AddLight.RingOneWay:
                            rootTags.Add(HapticUtils.TagSpsSocketIsRing);
                            rootTags.Add(HapticUtils.TagSpsSocketIsHole);
                            break;
                        default:
                            rootTags.Add(HapticUtils.TagSpsSocketIsHole);
                            break;
                    }
                }
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = senders,
                    objName = "Root",
                    radius = 0.001f,
                    tags = rootTags.ToArray(),
                    useHipAvoidance = true
                });
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = senders,
                    pos = Vector3.forward * 0.01f,
                    objName = "Front",
                    radius = 0.001f,
                    tags = new[] { HapticUtils.TagTpsOrfFront, HapticUtils.TagSpsSocketFront },
                    useHipAvoidance = true
                });
            }

            VFGameObject lights = null;
            var screenMarkers = new List<VFGameObject>();
            var screenMarkerResults = new List<ScreenMarkerResult>();
            if (lightType != VRCFuryHapticSocket.AddLight.None && !socket.fromSpsForAll) {
                ForEachPossibleLight(transform, false, light => {
                    light.Destroy();
                });

                if (BuildTargetUtils.IsDesktop()) {
                    var guidedPathStops = socket.guidedPathStops.ToList();
                    for (var i = 0; i < guidedPathStops.Count; i++) {
                        var stop = guidedPathStops[i];
                        if (stop == null || stop.transform == null) {
                            throw new Exception($"SPS guided path stop {i + 1} is missing its transform.");
                        }
                    }
                    foreach (var stop in guidedPathStops) {
                        var stopObj = stop.transform.asVf();
                        if (stopObj.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any()) {
                            throw new Exception(
                                "SPS guided path stops should not contain their own sockets. Invalid stop: "
                                + stopObj.GetPath());
                        }
                    }
                    var guidedPath = guidedPathStops
                        .Select(stop => stop.transform.asVf())
                        .ToList();
                    var hasGuidedPath = guidedPath.Count > 0;
                    var legacyLightType = GetLegacyLightType(socket, lightType);

                    void AddScreenMarker(ScreenMarkerResult result) {
                        if (result == null) return;
                        screenMarkerResults.Add(result);
                        screenMarkers.Add(result.obj);
                    }

                    ScreenMarkerResult CreateGuidedPathScreenMarker(
                        VFGameObject target,
                        VRCFuryHapticSocket.AddLight markerType,
                        uint socketId,
                        bool useTangentIn,
                        Vector3 tangentIn,
                        bool useTangentOut,
                        Vector3 tangentOut,
                        uint nextSocketId
                    ) {
                        var result = CreateScreenMarker(
                            oneSpace,
                            socket,
                            markerType,
                            socketId,
                            spsMarkers,
                            false,
                            useTangentIn,
                            tangentIn,
                            useTangentOut,
                            tangentOut,
                            nextSocketId,
                            includeTags: false,
                            objectName: "SPS Socket Path"
                        );
                        if (result == null) return null;

                        result.obj.worldPosition = target.worldPosition;
                        result.obj.worldRotation = target.worldRotation;
                        var constraint = VFConstraint.CreateParent(result.obj);
                        constraint.AddSource(target, 1);

                        return result;
                    }

                    if (socket.useLights) {
                        lights = GameObjects.Create("Lights", worldSpace);
                        Vector3 legacyOffset;
                        if (socket.overrideLegacyOffset) {
                            legacyOffset = socket.legacyOffset;
                        } else {
                            legacyOffset = socket.useRadiusOffset ? (Vector3.up * 0.03f) : Vector3.zero;
                        }
                        lights.localPosition = legacyOffset;
                        var main = GameObjects.Create("Root", lights);
                        main.localPosition = Vector3.zero;
                        var mainLight = main.AddComponent<Light>();
                        mainLight.type = LightType.Point;
                        mainLight.color = Color.black;
                        mainLight.range =
                            (legacyLightType == VRCFuryHapticSocket.AddLight.Ring || legacyLightType == VRCFuryHapticSocket.AddLight.RingOneWay)
                                ? 0.4206f
                                : 0.4106f;
                        mainLight.shadows = LightShadows.None;
                        mainLight.renderMode = LightRenderMode.ForceVertex;

                        var front = GameObjects.Create("Front", lights);
                        front.localPosition = Vector3.forward * 0.01f / lights.worldScale.x;
                        var frontLight = front.AddComponent<Light>();
                        frontLight.type = LightType.Point;
                        frontLight.color = Color.black;
                        frontLight.range = 0.4506f;
                        frontLight.shadows = LightShadows.None;
                        frontLight.renderMode = LightRenderMode.ForceVertex;
                    }

                    if (hasGuidedPath) {
                        var pathIds = guidedPath
                            .Select(_ => spsMarkers.NewMarkerId())
                            .ToList();
                        var firstStop = guidedPathStops[0];
                        AddScreenMarker(CreateScreenMarker(
                            oneSpace,
                            socket,
                            firstStop.shrink ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay,
                            spsMarkers.NewMarkerId(),
                            spsMarkers,
                            socket.useRadiusOffset,
                            false,
                            Vector3.zero,
                            firstStop.customizeTangentOut,
                            firstStop.tangentOut,
                            pathIds[0]
                        ));
                        for (var i = 0; i < guidedPath.Count; i++) {
                            var isLast = i == guidedPath.Count - 1;
                            var nextStop = isLast ? null : guidedPathStops[i + 1];
                            var pathType = isLast
                                ? GetGuidedPathTerminalType(lightType)
                                : nextStop.shrink
                                    ? VRCFuryHapticSocket.AddLight.Hole
                                    : VRCFuryHapticSocket.AddLight.RingOneWay;
                            var nextSocketId = isLast ? 0 : pathIds[i + 1];
                            var stop = guidedPathStops[i];

                            AddScreenMarker(CreateGuidedPathScreenMarker(
                                guidedPath[i],
                                pathType,
                                pathIds[i],
                                stop.customizeTangentIn,
                                stop.tangentIn,
                                nextStop?.customizeTangentOut ?? false,
                                nextStop?.tangentOut ?? Vector3.zero,
                                nextSocketId
                            ));
                        }
                    } else {
                        AddScreenMarker(CreateScreenMarker(
                            oneSpace,
                            socket,
                            lightType,
                            spsMarkers.NewMarkerId(),
                            spsMarkers,
                            socket.useRadiusOffset,
                            false,
                            Vector3.zero,
                            false,
                            Vector3.zero
                        ));
                    }
                }
            }
            
            if (EditorApplication.isPlaying && !socket.fromSpsForAll) {
                var gizmo = socket.owner().AddComponent<VRCFurySocketGizmo>();
                gizmo.data = VRCFuryHapticSocketGizmo.BuildGizmoData(socket);
            }

            return new BakeResult {
                bakeRoot = bakeRoot,
                oneSpace = oneSpace,
                worldSpace = worldSpace,
                screenMarkers = screenMarkers,
                screenMarkerResults = screenMarkerResults,
                lights = lights,
                senders = senders
            };
        }
    }
}
