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
using VF.Model;
using VF.Utils;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsConfigurer {
        public const string SpsEnabled = "_SPS_Enabled";
        public const string SpsObjectEnabled = "_SPS_ObjectEnabled";
        public const string SpsBakedLength = "_SPS_BakedLength";
        public const string SpsBakedRadius = "_SPS_BakedRadius";
        public const string SpsBaseActivationRadiusOverride = "_SPS_BaseActivationRadiusOverride";
        public const string SpsRadiusOffsetMultiplier = "_SPS_RadiusOffsetMultiplier";
        public const string SpsDisableShadows = "_SPS_DisableShadows";
        public const string SpsDisableDepth = "_SPS_DisableDepth";
        private const string SpsBake = "_SPS_Bake";

        [VFAutowired] [CanBeNull] private readonly SpsAutoChannelGenerator autoChannelGenerator;
        internal readonly string selfTagSuffix = "/self/" + BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);

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
            m.SetFloatFast(SpsObjectEnabled, 1);
            m.SetFloatFast("_SPS_Overrun", plug.spsOverrun ? 1 : 0);
            m.SetFloatFast(SpsBakedLength, worldLength);
            m.SetFloatFast(SpsBakedRadius, worldRadius);
            m.SetFloatFast(SpsBaseActivationRadiusOverride, 0);
            m.SetFloatFast(SpsRadiusOffsetMultiplier, 1);
            m.SetFloatFast(SpsDisableShadows, 0);
            m.SetFloatFast(SpsDisableDepth, 0);
            m.SetFloatFast("_SPS_BlendshapeCount", spsBlendshapes.Count);
            m.SetFloatFast("_SPS_BlendshapeVertCount", skin.GetVertexCount());
            for (var i = 0; i < spsBlendshapes.Count; i++) {
                var name = spsBlendshapes[i];
                if (skin.HasBlendshape(name)) {
                    m.SetFloatFast("_SPS_Blendshape" + i, skin.GetBlendshapeWeight(name));
                }
            }
        }

        internal HashSet<string> GetChannels(VFGameObject obj, bool isPlug, Party party, SpsChannelSettings settings) {
            HashSet<string> ForParty(bool self) => GetLogicalChannels(obj, isPlug, self, settings)
                .Select(c => self ? c + selfTagSuffix : c).ToHashSet();
            return party switch {
                Party.Self => ForParty(true),
                Party.Others => ForParty(false),
                Party.Merged => ForParty(true).Union(ForParty(false)).ToHashSet(),
                _ => throw new ArgumentOutOfRangeException(nameof(party))
            };
        }

        internal HashSet<string> GetLogicalChannels(VFGameObject obj, bool isPlug, bool self, SpsChannelSettings settings) {
            var channels = GetRuleChannels(settings.channels, self);
            if (settings.useSharedTag) {
                if (!isPlug) channels.UnionWith(GetAutoSocketChannels(obj));
                channels.Add(self && isPlug && settings.useHipAvoidance
                    && autoChannelGenerator?.GetClosestBone(obj) == HumanBodyBones.Hips
                    ? SpsTags.NotHipsChannel : SpsTags.PublicChannel);
            }
            return channels;
        }

        private static HashSet<string> GetRuleChannels(IEnumerable<SpsChannelSettings.Channel> rules, bool self) =>
            rules.Take(SpsTags.MaxUserChannels).Where(rule => rule != null && (self ? rule.allowSelf : rule.allowOthers))
                .Select(rule => NormalizeUserChannel(rule.id)).Where(channel => !string.IsNullOrWhiteSpace(channel)).ToHashSet();

        private static string NormalizeUserChannel(string channel) => channel?.Trim().ToLowerInvariant() ?? "";

        private string GetAutoHipFrontBackChannel(
            VFGameObject socket,
            HumanBodyBones? closestBone
        ) {
            if (closestBone != HumanBodyBones.Hips) return null;
            if (autoChannelGenerator == null) return null;

            var root = socket.root;
            var hips = autoChannelGenerator.GetBone(socket, HumanBodyBones.Hips);
            var rightHand = autoChannelGenerator.GetBone(socket, HumanBodyBones.RightHand);
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

            if (hipSockets[0].owner() == socket) return "hipsback";
            if (hipSockets[1].owner() == socket) return "hipsfront";
            return null;
        }

        internal HashSet<string> GetAutoSocketChannels(VFGameObject socket) {
            var bone = autoChannelGenerator?.GetClosestBone(socket);
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
            var result = channels.Where(channel => !string.IsNullOrWhiteSpace(channel)).ToHashSet();
            if (bone != HumanBodyBones.Hips) result.Add(SpsTags.NotHipsChannel);
            return result;
        }
    }
}
