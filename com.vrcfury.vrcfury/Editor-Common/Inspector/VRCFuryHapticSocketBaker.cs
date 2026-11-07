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
        private readonly Dictionary<VFGameObject, VFGameObject> nonUniformRoots = new();

        public BakeResult Bake(VRCFuryHapticSocket socket, bool legacyPlugCompatibility = true,
            bool tpsDepthAnimationCompatibility = false, VFGameObject avatarRoot = null) {
            var closestBone = autoChannelGenerator?.GetClosestBone(socket.owner());
            var channels = spsConfigurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender);
            var transform = socket.owner();
            var relocate = false;
            try {
                HapticUtils.AssertValidScale(transform);
            } catch (Exception) when (avatarRoot != null) {
                relocate = true;
            }

            var (lightType, localPosition, localRotation) = GetSocketTransform(socket, closestBone);

            var bakeRoot = GameObjects.Create("BakedSpsSocket", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            if (relocate) {
                var target = bakeRoot;
                target.name = "SPS Socket Target";
                var root = nonUniformRoots.GetOrCreate(avatarRoot,
                    () => GameObjects.Create("Nonuniform SPS Sockets", avatarRoot));
                bakeRoot = GameObjects.Create("BakedSpsSocket", root);
                bakeRoot.worldPosition = target.worldPosition;
                bakeRoot.worldRotation = target.worldRotation;
                VFConstraint.CreateParent(bakeRoot).AddSource(target, weight: 1);
            }

            var worldSpace = GameObjects.Create("WorldSpace", bakeRoot);
            ConstraintUtils.MakeWorldSpace(worldSpace);

            var legacyLightType = GetLegacyLightType(socket, lightType);
            VFGameObject rootSender;
            VFGameObject legacyFrontSender = null;

            // Senders
            {
                var legacySuffixes = new List<string>();
                if (legacyPlugCompatibility && channels.Contains(SpsTags.PublicChannel)) legacySuffixes.Add("");
                var rootTags = new List<string>();
                rootTags.AddRange(legacySuffixes.Select(suffix => SpsTags.LegacySocket2 + suffix));
                rootTags.AddRange(legacySuffixes.Select(suffix => SpsTags.LegacySocket + suffix));
                rootSender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = worldSpace,
                    objName = "Sender - Root",
                    radius = 0,
                    tags = rootTags.ToArray()
                });
                if (legacyPlugCompatibility && tpsDepthAnimationCompatibility && legacySuffixes.Count > 0) {
                    legacyFrontSender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest {
                        obj = worldSpace,
                        objName = "Sender - Legacy Front",
                        pos = Vector3.forward * 0.01f,
                        radius = 0.001f,
                        tags = legacySuffixes.Select(suffix => SpsTags.LegacySocketFront + suffix).ToArray()
                    });
                }
            }

            VFGameObject legacyLights = null;
            if (lightType != VRCFuryHapticSocket.AddLight.None) {
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
                    if (legacyPlugCompatibility && socket.useSharedTag) {
                        Vector3 legacyOffset;
                        if (socket.overrideLegacyOffset) {
                            legacyOffset = socket.legacyOffsetLocal;
                        } else {
                            legacyOffset = socket.useRadiusOffset
                                ? Vector3.up * VRCFuryHapticSocketEditor.LegacyRadiusOffset
                                : Vector3.zero;
                        }
                        legacyLights = GameObjects.Create("LegacyLights", bakeRoot);
                        // Keep the offset in socket-local units while light spacing stays in meters.
                        legacyLights.localPosition = legacyOffset;
                        ConstraintUtils.MakeWorldSpace(legacyLights);
                        var main = GameObjects.Create("Light - Legacy Root", legacyLights);
                        var mainLight = main.AddComponent<Light>();
                        mainLight.type = LightType.Point;
                        mainLight.color = Color.black;
                        mainLight.range =
                            (legacyLightType == VRCFuryHapticSocket.AddLight.Ring || legacyLightType == VRCFuryHapticSocket.AddLight.RingOneWay)
                                ? 0.4204f
                                : 0.4104f;
                        mainLight.shadows = LightShadows.None;
                        mainLight.renderMode = LightRenderMode.ForceVertex;

                        var front = GameObjects.Create("Light - Legacy Front", legacyLights);
                        front.localPosition = Vector3.forward * 0.01f;
                        var frontLight = front.AddComponent<Light>();
                        frontLight.type = LightType.Point;
                        frontLight.color = Color.black;
                        frontLight.range = 0.4504f;
                        frontLight.shadows = LightShadows.None;
                        frontLight.renderMode = LightRenderMode.ForceVertex;
                    }

                }
            }

            var pathRoots = ConfigureSpsSenders(socket, lightType, channels, rootSender);

            if (EditorApplication.isPlaying) {
                var gizmo = socket.owner().AddComponent<VRCFurySocketGizmo>();
                gizmo.data = GetGizmoData(socket);
                gizmo.data.useLegacyLights = legacyLights != null;
            }

            return new BakeResult {
                bakeRoot = bakeRoot,
                pathRoots = pathRoots,
                worldSpace = worldSpace,
                legacyLights = legacyLights,
                legacyFrontSender = legacyFrontSender
            };
        }

        public VRCFurySocketGizmo.SocketGizmoData GetGizmoData(VRCFuryHapticSocket socket) {
            var closestBone = autoChannelGenerator?.GetClosestBone(socket.owner());
            var (lightType, localPosition, localRotation) = GetSocketTransform(socket, closestBone);
            return VRCFuryHapticSocketGizmo.BuildGizmoData(
                socket,
                lightType,
                localPosition,
                localRotation
            );
        }

        internal static bool ShouldSupportOgbTouch(
            VRCFuryHapticSocket socket,
            HumanBodyBones? closestBone
        ) {
            if (closestBone != HumanBodyBones.Hips) return false;
            var name = GetMenuName(socket).ToLower();
            return !name.Contains("rubbing") && !name.Contains("job");
        }

        private static bool ShouldProbablyBeHole(
            VRCFuryHapticSocket socket,
            HumanBodyBones? closestBone
        ) {
            if (closestBone == HumanBodyBones.Head || closestBone == HumanBodyBones.Jaw) return true;
            return ShouldSupportOgbTouch(socket, closestBone);
        }

        internal VFGameObject[] ConfigureSpsSenders(VRCFuryHapticSocket socket,
            VRCFuryHapticSocket.AddLight type, IReadOnlyCollection<string> channels, VFGameObject rootSender) {
            if (rootSender == null || type == VRCFuryHapticSocket.AddLight.None) return Array.Empty<VFGameObject>();
            if (channels.Count == 0) return Array.Empty<VFGameObject>();
            var rootTags = channels.Select(SpsTags.FormatSocket).ToArray();
            var pathTags = channels.Select(SpsTags.FormatSocketPath).ToArray();

            var group = nextGroupId++;
            if (group > HapticUtils.GroupCount) throw new Exception($"SPS3 supports at most {HapticUtils.GroupCount} sockets per build.");

            var stops = socket.guidedPathStops;
            var stopCount = Math.Min(stops.Count, SpsTags.MaxUserPathStops);
            var pathRoots = new VFGameObject[stopCount];
            AddNode(rootSender, GetEntranceType(socket, type), 0);

            for (var i = 0; i < stopCount; i++) {
                var sender = GameObjects.Create($"Sender - Path Stop {i + 1}", rootSender.parent);
                var target = GameObjects.Create("BakedSpsPath", stops[i].transform.asVf());
                pathRoots[i] = target;
                sender.worldPosition = target.worldPosition;
                sender.worldRotation = target.worldRotation;
                VFConstraint.CreateParent(sender).AddSource(target, 1);
                sender.AddComponent<VRCContactSender>();
                var nodeType = i == stops.Count - 1
                    ? VRCFuryHapticSocketEditor.GetGuidedPathTerminalType(type)
                    : stops[i + 1].shrink ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay;
                AddNode(sender, nodeType, i + 1);
            }
            return pathRoots;

            void AddNode(VFGameObject sender, VRCFuryHapticSocket.AddLight nodeType, int pathIndex) {
                var tags = (pathIndex == 0 ? rootTags : pathTags).ToList();
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

        internal VRCFuryHapticSocket.AddLight GetEntranceType(VRCFuryHapticSocket socket) =>
            GetEntranceType(socket, GetSocketTransform(socket, autoChannelGenerator?.GetClosestBone(socket.owner())).Item1);

        private static VRCFuryHapticSocket.AddLight GetEntranceType(VRCFuryHapticSocket socket, VRCFuryHapticSocket.AddLight type) {
            if (type == VRCFuryHapticSocket.AddLight.None || socket.guidedPathStops.Count == 0) return type;
            return socket.guidedPathStops[0].shrink
                ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay;
        }

        private static void AddTags(VFGameObject obj, IEnumerable<string> tags) {
            var sender = obj.GetComponent<VRCContactSender>();
            var combined = sender.collisionTags.Concat(tags).Distinct().ToList();
            if (combined.Count > 16) throw new Exception("SPS3 sender exceeds the 16 contact tag limit.");
            sender.collisionTags = combined;
        }
        private static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetSocketTransform(
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
