using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Component;
using VF.Hooks;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsConfigurer {
        public const string SpsEnabled = "_SPS_Enabled";
        public const string SpsBakedLength = "_SPS_BakedLength";
        public const string SpsBakedRadius = "_SPS_BakedRadius";
        public const string SpsOverrun = "_SPS_Overrun";
        public const string SpsDisableShadows = "_SPS_DisableShadows";
        public const string SpsDisableDepth = "_SPS_DisableDepth";
        private const string SpsBake = "_SPS_Bake";

        [VFAutowired] [CanBeNull] private readonly SpsAutoChannelGenerator autoChannelGenerator;

        public static void ExtendBoundsForSps(Renderer renderer, VFGameObject bakeRoot, float worldLength) {
            if (renderer == null || bakeRoot == null) return;

            var bounds = renderer.GetLocalBounds();
            var rendererRoot = renderer.GetRootBone();
            Vector3 ToRendererLocal(Vector3 direction) {
                return rendererRoot.InverseTransformPoint(
                    bakeRoot.worldPosition + bakeRoot.TransformDirection(direction));
            }
            bounds.Encapsulate(ToRendererLocal(Vector3.zero));
            bounds.Encapsulate(ToRendererLocal(Vector3.right * worldLength));
            bounds.Encapsulate(ToRendererLocal(Vector3.left * worldLength));
            bounds.Encapsulate(ToRendererLocal(Vector3.up * worldLength));
            bounds.Encapsulate(ToRendererLocal(Vector3.down * worldLength));
            bounds.Encapsulate(ToRendererLocal(Vector3.back * worldLength * 0.5f));
            renderer.SetLocalBounds(bounds, "SPS bounds expansion");
        }

        public static void ConfigureSpsMaterial(
            Renderer skin,
            Material m,
            float worldLength,
            float worldRadius,
            Texture2D spsBaked,
            VRCFuryHapticPlug plug,
            VFGameObject bakeRoot,
            IList<string> spsBlendshapes
        ) {
            if (DpsConfigurer.IsDps(m) || TpsConfigurer.IsTps(m)) {
                throw new Exception(
                    $"VRCFury SPS plug was asked to use SPS deformation on renderer," +
                    $" but it already has TPS or DPS. If you want to use SPS, use a regular shader" +
                    $" on the mesh instead.");
            }

            SpsPatcher.Patch(m, SpsDevModeMenuItem.Get() && !IsActuallyUploadingHook.Get(), spsBlendshapes.Count > 0);
            {
                // Prevent poi from stripping our parameters
                var count = m.shader.GetPropertyCount();
                for (var i = 0; i < count; i++) {
                    var propertyName = m.shader.GetPropertyName(i);
                    if (propertyName.StartsWith("_SPS_")) {
                       m.SetOverrideTag(propertyName + "Animated", "1");
                    }
                }
            }
            if (plug.spsAnimatedEnabled == 0) bakeRoot.active = false;
            m.SetTextureFast(SpsBake, spsBaked);
            m.SetFloatFast(SpsEnabled, plug.spsAnimatedEnabled);
            m.SetFloatFast(SpsBakedLength, worldLength);
            m.SetFloatFast(SpsBakedRadius, worldRadius);
            m.SetFloatFast(SpsDisableShadows, 0);
            m.SetFloatFast(SpsDisableDepth, 0);
            m.SetFloatFast(SpsOverrun, plug.spsOverrun ? 1 : 0);
            m.SetFloatFast("_SPS_BlendshapeCount", spsBlendshapes.Count);
            m.SetFloatFast("_SPS_BlendshapeVertCount", skin.GetVertexCount());
            for (var i = 0; i < spsBlendshapes.Count; i++) {
                var name = spsBlendshapes[i];
                if (skin.HasBlendshape(name)) {
                    m.SetFloatFast("_SPS_Blendshape" + i, skin.GetBlendshapeWeight(name));
                }
            }
        }

        internal enum ChannelMode {
            Sender,
            ReceiverSelf,
            ReceiverOthers
        }

        internal HashSet<string> GetPlugChannels(VRCFuryHapticPlug plug, ChannelMode mode) {
            var channels = plug.includeTags.Take(SpsTags.MaxUserChannels)
                .Where(rule => rule != null && (mode == ChannelMode.ReceiverSelf ? rule.allowSelf
                    : mode == ChannelMode.ReceiverOthers ? rule.allowOthers : rule.allowSelf || rule.allowOthers))
                .Select(rule => NormalizeUserChannel(rule.tag))
                .Where(channel => !string.IsNullOrWhiteSpace(channel)).ToHashSet();
            if (plug.useSharedTag) channels.Add(SpsTags.PublicChannel);
            var isOnHips = autoChannelGenerator?.GetClosestBone(plug.owner()) == HumanBodyBones.Hips;
            if (mode == ChannelMode.ReceiverSelf && isOnHips && plug.useHipAvoidance && channels.Remove(SpsTags.PublicChannel)) {
                channels.Add(SpsTags.NotHipsChannel);
            }
            if (mode == ChannelMode.Sender && channels.Contains(SpsTags.PublicChannel) && (!isOnHips || !plug.useHipAvoidance)) {
                channels.Add(SpsTags.NotHipsChannel);
            }
            return channels;
        }

        internal HashSet<string> GetSocketChannels(VRCFuryHapticSocket socket, ChannelMode mode) {
            var channels = new HashSet<string>();
            channels.UnionWith(socket.tags.Take(SpsTags.MaxUserChannels)
                .Select(NormalizeUserChannel)
                .Where(channel => !string.IsNullOrWhiteSpace(channel)));
            if (socket.useSharedTag) {
                channels.UnionWith(GetAutoSocketChannels(socket));
                channels.Add(SpsTags.PublicChannel);
            }
            var isOnHips = autoChannelGenerator?.GetClosestBone(socket.owner()) == HumanBodyBones.Hips;
            if (mode == ChannelMode.ReceiverSelf && isOnHips && channels.Remove(SpsTags.PublicChannel)) {
                channels.Add(SpsTags.NotHipsChannel);
            }
            if (mode == ChannelMode.Sender && !isOnHips && channels.Contains(SpsTags.PublicChannel)) {
                channels.Add(SpsTags.NotHipsChannel);
            }
            return channels;
        }

        private static string NormalizeUserChannel(string channel) {
            channel = channel?.Trim().ToLowerInvariant() ?? "";
            while (channel.StartsWith("_")) channel = channel.TrimStart('_').TrimStart();
            return channel;
        }

        private string GetAutoHipFrontBackChannel(
            VRCFuryHapticSocket socket,
            HumanBodyBones? closestBone
        ) {
            if (closestBone != HumanBodyBones.Hips) return null;
            if (autoChannelGenerator == null) return null;

            var root = socket.owner().root;
            var hips = autoChannelGenerator.GetBone(socket.owner(), HumanBodyBones.Hips);
            var rightHand = autoChannelGenerator.GetBone(socket.owner(), HumanBodyBones.RightHand);
            if (hips == null || rightHand == null) return null;

            var right = rightHand.worldPosition - hips.worldPosition;
            if (right.sqrMagnitude <= 0.000001f) return null;

            var forward = Vector3.Cross(right.normalized, Vector3.up.normalized);
            if (forward.sqrMagnitude <= 0.000001f) return null;
            forward.Normalize();

            var hipSockets = root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(other => other != null
                    && autoChannelGenerator.GetClosestBone(other.owner()) == HumanBodyBones.Hips)
                .OrderBy(other => {
                    var offset = other.owner().worldPosition - hips.worldPosition;
                    return Mathf.Abs(Vector3.Dot(offset, forward));
                })
                .ThenBy(other => other.owner().GetPath(root))
                .Take(2)
                .ToList();

            if (hipSockets.Count != 2) return null;

            hipSockets = hipSockets
                .OrderBy(other => Vector3.Dot(other.owner().worldPosition - hips.worldPosition, forward))
                .ThenBy(other => other.owner().GetPath(root))
                .ToList();

            if (hipSockets[0] == socket) return "hipsback";
            if (hipSockets[1] == socket) return "hipsfront";
            return null;
        }

        internal HashSet<string> GetAutoSocketChannels(VRCFuryHapticSocket socket) {
            var bone = autoChannelGenerator?.GetClosestBone(socket.owner());
            var channels = bone switch {
                HumanBodyBones.Hips => new[] { "hips", GetAutoHipFrontBackChannel(socket, bone) },
                HumanBodyBones.Head or HumanBodyBones.Jaw => new[] { "head" },
                HumanBodyBones.Chest or HumanBodyBones.UpperChest => new[] { "chest" },
                HumanBodyBones.LeftHand => new[] { "hand", "handleft" },
                HumanBodyBones.RightHand => new[] { "hand", "handright" },
                HumanBodyBones.LeftFoot or HumanBodyBones.LeftToes => new[] { "foot", "footleft" },
                HumanBodyBones.RightFoot or HumanBodyBones.RightToes => new[] { "foot", "footright" },
                _ => Array.Empty<string>()
            };
            return channels.Where(channel => !string.IsNullOrWhiteSpace(channel)).ToHashSet();
        }
    }
}
