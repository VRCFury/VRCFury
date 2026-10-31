using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;
using static VF.Inspector.VRCFuryHapticSocketEditor;
using BakeResult = VF.Inspector.VRCFuryHapticSocketEditor.BakeResult;

namespace VF.Inspector {
    [VFService]
    internal class VRCFuryHapticSocketBaker {
        [VFAutowired] [CanBeNull] private readonly SpsAutoChannelGenerator autoChannelGenerator;
        [VFAutowired] private readonly SpsConfigurer spsConfigurer;
        private int nextGroupId = 1;

        [CanBeNull]
        public BakeResult Bake(VRCFuryHapticSocket socket) {
            var closestBone = autoChannelGenerator?.GetClosestBone(socket.owner());
            var channels = spsConfigurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender);
            var transform = socket.owner();
            if (!HapticUtils.AssertValidScale(transform, "socket", shouldThrow: !socket.fromSpsForAll)) {
                return null;
            }

            var (lightType, localPosition, localRotation) = GetInfoFromLightsOrComponent(socket, closestBone);

            var bakeRoot = GameObjects.Create("BakedSpsSocket", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            var oneSpace = GameObjects.Create("OneSpace", bakeRoot);
            oneSpace.worldScale = Vector3.one;

            var worldSpace = GameObjects.Create("WorldSpace", bakeRoot);
            ConstraintUtils.MakeWorldSpace(worldSpace);

            var senders = GameObjects.Create("Senders", worldSpace);

            var legacyLightType = GetLegacyLightType(socket, lightType);
            VFGameObject rootSender;

            // Senders
            {
                var legacySuffixes = new List<string>();
                if (channels.Contains(SpsTags.PublicChannel)) legacySuffixes.Add("");
                if (channels.Contains(SpsTags.NotHipsChannel)) legacySuffixes.Add("_SelfNotOnHips");
                var rootTags = new List<string>();
                rootTags.AddRange(legacySuffixes.Select(suffix => SpsTags.LegacySocket2 + suffix));
                rootTags.AddRange(legacySuffixes.Select(suffix => SpsTags.LegacySocket + suffix));
                rootSender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = senders,
                    objName = "Root",
                    radius = 0,
                    tags = rootTags.ToArray()
                });
            }

            VFGameObject lights = null;
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
                                + stopObj.GetDebugPath());
                        }
                    }
                    if (socket.useLights) {
                        Vector3 legacyOffset;
                        if (socket.overrideLegacyOffset) {
                            legacyOffset = socket.legacyOffsetLocal;
                        } else {
                            legacyOffset = socket.useRadiusOffset
                                ? Vector3.up * VRCFuryHapticSocketEditor.LegacyRadiusOffset
                                : Vector3.zero;
                        }
                        if (legacyOffset == Vector3.zero) {
                            lights = GameObjects.Create("Lights", worldSpace);
                        } else {
                            // If the lights are offset, we need to normalize the scale separately from WorldSpace so the offset
                            // can remain in local-units.
                            lights = GameObjects.Create("Lights", bakeRoot);
                            lights.localPosition = legacyOffset;
                            ConstraintUtils.MakeWorldSpace(lights);
                        }
                        var main = GameObjects.Create("Root", lights);
                        main.localPosition = Vector3.zero;
                        var mainLight = main.AddComponent<Light>();
                        mainLight.type = LightType.Point;
                        mainLight.color = Color.black;
                        mainLight.range =
                            (legacyLightType == VRCFuryHapticSocket.AddLight.Ring || legacyLightType == VRCFuryHapticSocket.AddLight.RingOneWay)
                                ? 0.4202f
                                : 0.4102f;
                        mainLight.shadows = LightShadows.None;
                        mainLight.renderMode = LightRenderMode.ForceVertex;

                        var front = GameObjects.Create("Front", lights);
                        front.localPosition = Vector3.forward * 0.01f / lights.worldScale.x;
                        var frontLight = front.AddComponent<Light>();
                        frontLight.type = LightType.Point;
                        frontLight.color = Color.black;
                        frontLight.range = 0.4502f;
                        frontLight.shadows = LightShadows.None;
                        frontLight.renderMode = LightRenderMode.ForceVertex;
                    }

                }
            }

            ConfigureSpsSenders(socket, lightType, channels, rootSender);

            if (EditorApplication.isPlaying && !socket.fromSpsForAll) {
                var gizmo = socket.owner().AddComponent<VRCFurySocketGizmo>();
                gizmo.data = GetGizmoData(socket);
            }

            return new BakeResult {
                bakeRoot = bakeRoot,
                oneSpace = oneSpace,
                worldSpace = worldSpace,
                lights = lights,
                senders = senders
            };
        }

        public VRCFurySocketGizmo.SocketGizmoData GetGizmoData(VRCFuryHapticSocket socket) {
            var closestBone = autoChannelGenerator?.GetClosestBone(socket.owner());
            var (lightType, localPosition, localRotation) = GetInfoFromLightsOrComponent(socket, closestBone);
            return VRCFuryHapticSocketGizmo.BuildGizmoData(
                socket,
                lightType,
                GetHandTouchZoneSize(socket),
                localPosition,
                localRotation
            );
        }

        internal static bool ShouldProbablyHaveTouchZone(
            VRCFuryHapticSocket socket,
            HumanBodyBones? closestBone
        ) {
            if (closestBone != HumanBodyBones.Hips) return false;

            var name = GetMenuName(socket).ToLower();
            return !name.Contains("rubbing") && !name.Contains("job");
        }

        public Tuple<float, float> GetHandTouchZoneSize(VRCFuryHapticSocket socket) {
            var enableHandTouchZone = socket.enableHandTouchZone2 == VRCFuryHapticSocket.EnableTouchZone.On
                || (socket.enableHandTouchZone2 == VRCFuryHapticSocket.EnableTouchZone.Auto
                    && ShouldProbablyHaveTouchZone(
                        socket,
                        autoChannelGenerator?.GetClosestBone(socket.owner())
                    ));
            if (!enableHandTouchZone) return null;

            var length = socket.length * (socket.unitsInMeters ? 1f : socket.owner().worldScale.z);
            if (length <= 0) {
                var viewPosition = autoChannelGenerator?.GetAvatarViewPosition(socket.owner());
                if (viewPosition == null || viewPosition.Value.y <= 0) return null;
                length = viewPosition.Value.y * 0.05f;
            }
            return Tuple.Create(length, length / 2.5f);
        }

        private static bool ShouldProbablyBeHole(
            VRCFuryHapticSocket socket,
            HumanBodyBones? closestBone
        ) {
            if (closestBone == HumanBodyBones.Head || closestBone == HumanBodyBones.Jaw) return true;
            return ShouldProbablyHaveTouchZone(socket, closestBone);
        }

        internal void ConfigureSpsSenders(VRCFuryHapticSocket socket,
            VRCFuryHapticSocket.AddLight type, IReadOnlyCollection<string> channels, VFGameObject rootSender) {
            // Automatic legacy sockets keep their original lights; don't also advertise a native visual target.
            if (socket.fromSpsForAll) return;
            if (rootSender == null || type == VRCFuryHapticSocket.AddLight.None) return;
            if (channels.Count == 0) return;
            var rootTags = channels.Select(SpsTags.FormatSocket).ToArray();
            var pathTags = channels.Select(SpsTags.FormatSocketPath).ToArray();

            var group = nextGroupId++;
            if (group > HapticUtils.GroupCount) throw new Exception($"SPS3 supports at most {HapticUtils.GroupCount} sockets per build.");

            var stops = socket.guidedPathStops;
            var stopCount = Math.Min(stops.Count, SpsTags.MaxUserPathStops);
            var rootType = stopCount == 0 ? type : stops[0].shrink
                ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay;
            AddNode(rootSender, rootType, 0);

            for (var i = 0; i < stopCount; i++) {
                var sender = GameObjects.Create($"SPS3 Stop #{i + 1}", rootSender.parent);
                var target = stops[i].transform.asVf();
                sender.worldPosition = target.worldPosition;
                sender.worldRotation = target.worldRotation;
                VFConstraint.CreateParent(sender).AddSource(target, 1);
                sender.AddComponent<VRCContactSender>();
                var nodeType = i == stops.Count - 1
                    ? VRCFuryHapticSocketEditor.GetGuidedPathTerminalType(type)
                    : stops[i + 1].shrink ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay;
                AddNode(sender, nodeType, i + 1);
            }

            void AddNode(VFGameObject sender, VRCFuryHapticSocket.AddLight nodeType, int pathIndex) {
                var tags = (pathIndex == 0 ? rootTags : pathTags).ToList();
                tags.Add(SpsTags.ScaleV1);
                var flags = HapticUtils.SocketScaleFlags.None;
                if (pathIndex == 0 && socket.useRadiusOffset) {
                    tags.Add(SpsTags.SocketRadiusOffset);
                    flags |= HapticUtils.SocketScaleFlags.RadiusOffset;
                }
                if (nodeType == VRCFuryHapticSocket.AddLight.Hole) {
                    tags.Add(SpsTags.SocketCollapse);
                    flags |= HapticUtils.SocketScaleFlags.Collapse;
                } else if (nodeType == VRCFuryHapticSocket.AddLight.Ring) {
                    tags.Add(SpsTags.SocketTwoway);
                    flags |= HapticUtils.SocketScaleFlags.Twoway;
                }
                AddTags(sender, tags);
                // WorldSpace keeps metadata scale independent of avatar/stop scaling.
                sender.worldScale = HapticUtils.EncodeSocketScale(group, pathIndex, flags);
                sender.GetComponent<VRCContactSender>().radius = 0;
            }
        }

        private static void AddTags(VFGameObject obj, IEnumerable<string> tags) {
            var sender = obj.GetComponent<VRCContactSender>();
            var combined = sender.collisionTags.Concat(tags).Distinct().ToList();
            if (combined.Count > 16) throw new Exception("SPS3 sender exceeds the 16 contact tag limit.");
            sender.collisionTags = combined;
        }
        private static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLightsOrComponent(
            VRCFuryHapticSocket socket,
            HumanBodyBones? closestBone
        ) {
            var (type, socketTransformLocal) = GetSocketTransformLocal(socket);
            if (type == VRCFuryHapticSocket.AddLight.Auto) {
                type = ShouldProbablyBeHole(socket, closestBone)
                    ? VRCFuryHapticSocket.AddLight.Hole
                    : VRCFuryHapticSocket.AddLight.Ring;
            }
            return Tuple.Create(type, socketTransformLocal.position, socketTransformLocal.rotation);
        }

    }
}
