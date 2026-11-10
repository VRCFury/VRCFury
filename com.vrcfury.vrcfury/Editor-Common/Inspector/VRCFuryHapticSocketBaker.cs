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
        [VFAutowired] private readonly SpsChannelsService channels;
        [VFAutowired] private readonly SpsSocketReceiverService socketReceivers;
        private int nextGroupId = 1;
        private readonly Dictionary<VFGameObject, VFGameObject> nonUniformRoots = new();

        public BakeResult Bake(VRCFuryHapticSocket socket, bool legacyPlugCompatibility = true,
            bool tpsDepthAnimationCompatibility = false, VFGameObject avatarRoot = null) {
            if (!BuildTargetUtils.IsDesktop()) return null;
            var closestBone = autoChannelGenerator?.GetClosestBone(socket.owner());
            var contacts = channels.Get(socket);
            var result = new BakeResult { channelContacts = contacts };
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
            var pathRoots = Array.Empty<VFGameObject>();

            // Senders
            var enableDeformation = lightType != VRCFuryHapticSocket.AddLight.None;
            if (enableDeformation) {
                var rootSender = GameObjects.Create("Sender - Root", worldSpace);
                var stops = socket.guidedPathStops;
                for (var i = 0; i < stops.Count; i++) {
                    var stop = stops[i];
                    if (stop == null || stop.transform == null)
                        throw new Exception($"SPS guided path stop {i + 1} is missing its transform.");
                }
                foreach (var stop in stops) {
                    if (stop.transform.asVf().GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any())
                        throw new Exception("SPS guided path stops should not contain their own sockets. Invalid stop: "
                            + stop.transform.asVf().GetDebugPath());
                }
                var group = nextGroupId++;
                if (group > HapticUtils.GroupCount)
                    throw new Exception($"SPS3 supports at most {HapticUtils.GroupCount} sockets per build.");
                pathRoots = new VFGameObject[Math.Min(stops.Count, SpsTags.MaxUserPathStops)];
                for (var index = 0; index <= pathRoots.Length; index++) {
                    var pathIndex = index;
                    var sender = rootSender;
                    var nodeType = GetEntranceType(socket, lightType);
                    if (pathIndex > 0) {
                        sender = GameObjects.Create($"Sender - Path Stop {pathIndex}", worldSpace);
                        var target = GameObjects.Create("BakedSpsPath", stops[pathIndex - 1].transform.asVf());
                        pathRoots[pathIndex - 1] = target;
                        sender.worldPosition = target.worldPosition;
                        sender.worldRotation = target.worldRotation;
                        VFConstraint.CreateParent(sender).AddSource(target, 1);
                        nodeType = pathIndex == stops.Count
                            ? GetGuidedPathTerminalType(lightType)
                            : stops[pathIndex].shrink ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay;
                    }
                    var tags = new List<string>();
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
                    // WorldSpace keeps metadata scale independent of avatar/stop scaling.
                    sender.worldScale = HapticUtils.EncodeSocketScale(group, pathIndex, flags);
                    contacts.OnAllSetsAvailable(sets => {
                        var variants = new Dictionary<string, VFGameObject>();
                        foreach (var set in sets) {
                            var channelTags = new List<string>();
                            if (set.sender.Length > 0) {
                                channelTags.AddRange(pathIndex == 0 ? set.sender.Select(SpsTags.FormatSocket)
                                    : set.sender.Select(SpsTags.FormatSocketPath));
                                channelTags.AddRange(tags);
                            }
                            if (pathIndex == 0 && set.isPublic && legacyPlugCompatibility)
                                channelTags.AddRange(new[] { SpsTags.LegacySocket2, SpsTags.LegacySocket });
                            if (channelTags.Count == 0) continue;
                            if (channelTags.Count > 16) throw new Exception("SPS3 sender exceeds the 16 contact tag limit.");
                            var owner = variants.GetOrCreate(string.Join("\n", channelTags), () => {
                                var owner = sets.Count == 1 ? sender : GameObjects.Create(
                                    "Channel (" + string.Join(", ", set.sender) + ")", sender);
                                var contact = owner.AddComponent<VRCContactSender>();
                                contact.collisionTags = channelTags;
                                contact.radius = 0;
                                return owner;
                            });
                            set.Register(owner);
                        }
                    });
                }

                if (legacyPlugCompatibility) {
                    Vector3 legacyOffset;
                    if (socket.overrideLegacyOffset) {
                        legacyOffset = socket.legacyOffsetLocal;
                    } else {
                        legacyOffset = socket.useRadiusOffset
                            ? Vector3.up * VRCFuryHapticSocketEditor.LegacyRadiusOffset
                            : Vector3.zero;
                    }
                    contacts.OnAllSetsAvailable(sets => {
                        var publicSets = sets.Where(s => s.isPublic).ToArray();
                        if (publicSets.Length == 0) return;
                        var legacyLights = GameObjects.Create("LegacyLights", bakeRoot);
                        result.legacyLights = legacyLights;
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
                        foreach (var set in publicSets) set.Register(legacyLights);
                    });

                    if (tpsDepthAnimationCompatibility) {
                        var frontPosition = Vector3.forward * 0.01f / worldSpace.worldScale.x;
                        var frontRadius = 0.001f / worldSpace.worldScale.x;
                        contacts.OnAllSetsAvailable(sets => {
                            var publicSets = sets.Where(s => s.isPublic).ToArray();
                            if (publicSets.Length == 0) return;
                            result.legacyFrontSender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest {
                                obj = worldSpace, objName = "Sender - Legacy Front", pos = frontPosition,
                                radius = frontRadius, useWorldUnits = false, tags = new[] { SpsTags.LegacySocketFront }
                            });
                            foreach (var set in publicSets) set.Register(result.legacyFrontSender);
                        });
                    }
                }
            }

            if (EditorApplication.isPlaying) {
                var gizmo = socket.owner().AddComponent<VRCFurySocketGizmo>();
                gizmo.data = GetGizmoData(socket);
                contacts.OnAllSetsAvailable(_ => {
                    gizmo.data.useLegacyLights = result.legacyLights != null;
                    gizmo.data.legacyLights = result.legacyLights;
                });
            }

            result.plugReceivers = socketReceivers.GetReceivers(socket, worldSpace, socket.name, legacyPlugCompatibility);
            result.bakeRoot = bakeRoot;
            result.pathRoots = pathRoots;
            result.worldSpace = worldSpace;
            return result;
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


        internal VRCFuryHapticSocket.AddLight GetEntranceType(VRCFuryHapticSocket socket) =>
            GetEntranceType(socket, GetSocketTransform(socket, autoChannelGenerator?.GetClosestBone(socket.owner())).Item1);

        private static VRCFuryHapticSocket.AddLight GetEntranceType(VRCFuryHapticSocket socket, VRCFuryHapticSocket.AddLight type) {
            if (type == VRCFuryHapticSocket.AddLight.None || socket.guidedPathStops.Count == 0) return type;
            return socket.guidedPathStops[0].shrink
                ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.RingOneWay;
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
