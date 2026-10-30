using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Component;
using VF.Hooks;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsConfigurer {
        public const uint SharedChannelId = 1337;
        public const string SpsEnabled = "_SPS_Enabled";
        public const string SpsBakedLength = "_SPS_BakedLength";
        public const string SpsBakedRadius = "_SPS_BakedRadius";
        public const string SpsBakedRadiusSamples = "_SPS_BakedRadiusSamples";
        public const string SpsMetadataColor = "_SPS_MetadataColor";
        public const string SpsOverrun = "_SPS_Overrun";
        public const string SpsLegacy = "_SPS_Legacy";
        public const string SpsDisableShadows = "_SPS_DisableShadows";
        public const string SpsDisableDepth = "_SPS_DisableDepth";
        private const string SpsBake = "_SPS_Bake";
        private const uint IncludeSelf = 1;
        private const uint IncludeOthers = 2;

        [VFAutowired] [CanBeNull] private readonly SpsAutoChannelGenerator autoChannelGenerator;

        public class MaterialProperty {
            public UnityEngine.Component component;
            public string propertyName;
            public float value;
        }

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

        private static void SetSplitId(Action<string, float> set, string lowProperty, string highProperty, uint value) {
            set(lowProperty, SpsMarkersService.GetLow(value));
            set(highProperty, SpsMarkersService.GetHigh(value));
        }

        public static void ConfigureSpsMaterial(
            Renderer skin,
            Material m,
            float worldLength,
            Texture2D spsBaked,
            VRCFuryHapticPlug plug,
            VFGameObject bakeRoot,
            IList<string> spsBlendshapes,
            uint resolverHash
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
            m.SetFloatFast(SpsMarkersService.Configured, 1);
            m.SetFloatFast(SpsMarkersService.IdLow, SpsMarkersService.GetLow(resolverHash));
            m.SetFloatFast(SpsMarkersService.IdHigh, SpsMarkersService.GetHigh(resolverHash));
            m.SetFloatFast(SpsMarkersService.PlayerIdLow, 0);
            m.SetFloatFast(SpsMarkersService.PlayerIdHigh, 0);
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

        public List<MaterialProperty> GetResolverProperties(
            Renderer renderer,
            float worldLength,
            float worldRadius,
            Vector4[] bakedRadiusSamples,
            Color metadataColor,
            uint resolverHash,
            VRCFuryHapticPlug plug
        ) {
            Transform transform = renderer.owner();
            renderer.owner().localScale = Vector3.one * 0.001f;

            var properties = new List<MaterialProperty>();
            properties.Add(new MaterialProperty { component = transform, propertyName = "m_LocalScale.x", value = 1 });
            properties.Add(new MaterialProperty { component = transform, propertyName = "m_LocalScale.y", value = 1 });
            properties.Add(new MaterialProperty { component = transform, propertyName = "m_LocalScale.z", value = 1 });
            void Add(string propertyName, float value) {
                properties.Add(new MaterialProperty {
                    component = renderer,
                    propertyName = $"material.{propertyName}",
                    value = value
                });
            }
            Add(SpsBakedLength, worldLength);
            Add(SpsBakedRadius, worldRadius);
            AddPackedVectors(Add, SpsBakedRadiusSamples, bakedRadiusSamples, 4);
            Add(SpsMetadataColor + ".r", metadataColor.r);
            Add(SpsMetadataColor + ".g", metadataColor.g);
            Add(SpsMetadataColor + ".b", metadataColor.b);
            Add(SpsMetadataColor + ".a", 1);
            Add(SpsEnabled, plug.spsAnimatedEnabled);
            Add(SpsOverrun, plug.spsOverrun ? 1 : 0);
            Add(SpsLegacy, plug.useLights ? 1 : 0);
            ConfigureResolverChannelRules(
                Add,
                plug,
                plug.useHipAvoidance && autoChannelGenerator?.GetClosestBone(plug.owner()) == HumanBodyBones.Hips
            );
            Add(SpsMarkersService.Configured, 1);
            SetSplitId(Add, SpsMarkersService.IdLow, SpsMarkersService.IdHigh, resolverHash);
            return properties;
        }

        private static void AddPackedVectors(Action<string, float> add, string baseName, Vector4[] values, int count) {
            var components = new[] { ".x", ".y", ".z", ".w" };
            for (var i = 0; i < count; i++) {
                var value = values.GetOrDefault(i, Vector4.zero);
                var property = $"{baseName}{i}";
                add(property + components[0], value.x);
                add(property + components[1], value.y);
                add(property + components[2], value.z);
                add(property + components[3], value.w);
            }
        }

        public List<MaterialProperty> GetSocketProperties(
            Renderer renderer,
            VRCFuryHapticSocket socket,
            float socketScale,
            VRCFuryHapticSocket.AddLight lightType,
            uint socketId,
            bool useTangentIn,
            Vector3 tangentIn,
            bool useTangentOut,
            Vector3 tangentOut,
            bool useRadiusOffset,
            uint nextSocketId = 0,
            bool includeChannels = true
        ) {
            Transform transform = renderer.owner();
            renderer.owner().localScale = Vector3.one * 0.001f;

            var properties = new List<MaterialProperty>();
            properties.Add(new MaterialProperty { component = transform, propertyName = "m_LocalScale.x", value = 1 });
            properties.Add(new MaterialProperty { component = transform, propertyName = "m_LocalScale.y", value = 1 });
            properties.Add(new MaterialProperty { component = transform, propertyName = "m_LocalScale.z", value = 1 });
            void Add(string propertyName, float value) {
                properties.Add(new MaterialProperty {
                    component = renderer,
                    propertyName = $"material.{propertyName}",
                    value = value
                });
            }
            Add(SpsMarkersService.Configured, 1);
            SetSplitId(Add, SpsMarkersService.IdLow, SpsMarkersService.IdHigh, socketId);
            Add(SpsMarkersService.SocketHole, lightType == VRCFuryHapticSocket.AddLight.Hole ? 1 : 0);
            Add(SpsMarkersService.SocketDoubleSided, lightType == VRCFuryHapticSocket.AddLight.Ring ? 1 : 0);
            Add(SpsMarkersService.SocketRadiusOffset, useRadiusOffset ? 1 : 0);
            SetSplitId(Add, SpsMarkersService.GuidedTargetIdLow, SpsMarkersService.GuidedTargetIdHigh, nextSocketId);
            Add(SpsMarkersService.SocketUseTangentIn, useTangentIn ? 1 : 0);
            Add(SpsMarkersService.SocketUseTangentOut, useTangentOut ? 1 : 0);
            Add(SpsMarkersService.SocketTangentIn + ".x", tangentIn.x);
            Add(SpsMarkersService.SocketTangentIn + ".y", tangentIn.y);
            Add(SpsMarkersService.SocketTangentIn + ".z", tangentIn.z);
            Add(SpsMarkersService.SocketTangentOut + ".x", tangentOut.x);
            Add(SpsMarkersService.SocketTangentOut + ".y", tangentOut.y);
            Add(SpsMarkersService.SocketTangentOut + ".z", tangentOut.z);
            Add(SpsMarkersService.SocketScale, socketScale);
            ConfigureSocketChannels(Add, includeChannels ? GetSocketChannels(socket, ChannelMode.Sender) : Enumerable.Empty<string>());
            return properties;
        }

        public static bool PropagateToResolver(string propertyName) {
            return propertyName == $"material.{SpsEnabled}"
                || propertyName == $"material.{SpsBakedLength}"
                || propertyName == $"material.{SpsBakedRadius}"
                || propertyName == $"material.{SpsMarkersService.PlayerIdLow}"
                || propertyName == $"material.{SpsMarkersService.PlayerIdHigh}"
                || propertyName == $"material.{SpsOverrun}"
                || propertyName == $"material.{SpsLegacy}";
        }

        public static void AddMaterialPropertyAnimator(IEnumerable<MaterialProperty> properties, SaveAssetsSession saveSession) {
            var propertyList = (properties ?? new List<MaterialProperty>())
                .Where(property => property?.component != null)
                .ToList();
            if (propertyList.Count == 0) return;

            foreach (var group in propertyList.GroupBy(property => property.component.owner())) {
                AddMaterialPropertyAnimator(group.Key, group, saveSession);
            }
        }

        private static void AddMaterialPropertyAnimator(
            VFGameObject obj,
            IEnumerable<MaterialProperty> properties,
            SaveAssetsSession saveSession
        ) {
            var clip = VFClip.Create("SpsMaterialProperties");

            var controller = VFController.Create("SpsMaterialProperties");
            controller
                .NewLayer("SPS Material Properties")
                .NewState("Properties")
                .WithAnimation(clip);

            AddMaterialPropertyCurves(clip, properties);

            var animator = obj.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller.Save(obj, saveSession);
        }

        public static void AddMaterialPropertyCurves(VFClip clip, IEnumerable<MaterialProperty> properties) {
            foreach (var property in properties ?? new List<MaterialProperty>()) {
                if (property?.component == null) continue;
                var component = property.component;
                var owner = component.owner();
                clip.SetCurve(owner, component.GetType(), property.propertyName, property.value);
            }
        }

        internal enum ChannelMode {
            Sender,
            ReceiverSelf,
            ReceiverOthers
        }

        internal HashSet<string> GetPlugChannels(VRCFuryHapticPlug plug, ChannelMode mode) {
            var channels = plug.includeTags.Take(VRCFuryHapticPlugEditor.SpsChannelRuleCount)
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
            channels.UnionWith(socket.tags.Take(VRCFuryHapticSocketEditor.SpsChannelCount)
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

        private static void ConfigureSocketChannels(Action<string, float> set, IEnumerable<string> channels) {
            var ids = channels.Select(channel => channel == SpsTags.PublicChannel ? SharedChannelId : HashChannel(channel)).ToArray();
            for (var i = 0; i < 8; i++) {
                var id = i < ids.Length ? ids[i] : 0;
                SetSplitId(set, $"_SPS_SocketTag{i + 1}Low", $"_SPS_SocketTag{i + 1}High", id);
            }
        }

        public static uint HashChannel(string channel) {
            if (string.IsNullOrWhiteSpace(channel)) return 0;

            var normalized = channel.Trim().ToLowerInvariant();
            uint hash = 2166136261;
            foreach (var c in normalized) {
                hash ^= c;
                hash *= 16777619;
            }

            return hash == 0 ? 1u : hash;
        }

        private static void ConfigureResolverChannelRules(
            Action<string, float> set,
            VRCFuryHapticPlug plug,
            bool excludeOwnHips
        ) {
            var includeChannels = new uint[4];
            var includeFlags = new uint[4];
            var excludeChannels = new uint[4];
            var excludeFlags = new uint[4];
            for (var i = 0; i < plug.includeTags.Count && i < VRCFuryHapticPlugEditor.SpsChannelRuleCount; i++) {
                SetChannel(includeChannels, includeFlags, i, plug.includeTags[i]);
            }
            if (plug.useSharedTag) {
                SetChannel(includeChannels, 3, SharedChannelId, IncludeSelf | IncludeOthers, includeFlags);
            }

            for (var i = 0; i < plug.excludeTags.Count && i < VRCFuryHapticPlugEditor.SpsChannelRuleCount; i++) {
                SetChannel(excludeChannels, excludeFlags, i, plug.excludeTags[i]);
            }
            if (excludeOwnHips) {
                SetChannel(excludeChannels, 3, HashChannel("hips"), IncludeSelf, excludeFlags);
            }

            for (var i = 0; i < 4; i++) {
                var slot = i + 1;
                SetSplitId(set, $"_SPS_TagInclude{slot}Low", $"_SPS_TagInclude{slot}High", includeChannels[i]);
                set($"_SPS_TagInclude{slot}Self", (includeFlags[i] & IncludeSelf) != 0 ? 1 : 0);
                set($"_SPS_TagInclude{slot}Others", (includeFlags[i] & IncludeOthers) != 0 ? 1 : 0);
                SetSplitId(set, $"_SPS_TagExclude{slot}Low", $"_SPS_TagExclude{slot}High", excludeChannels[i]);
                set($"_SPS_TagExclude{slot}Self", (excludeFlags[i] & IncludeSelf) != 0 ? 1 : 0);
                set($"_SPS_TagExclude{slot}Others", (excludeFlags[i] & IncludeOthers) != 0 ? 1 : 0);
            }
        }

        private static void SetChannel(uint[] channels, int index, uint channel) {
            if (index < 0 || index >= channels.Length) return;
            channels[index] = channel;
        }

        private static void SetChannel(uint[] channels, int index, uint channel, uint ruleFlags, uint[] flags) {
            if (index < 0 || index >= channels.Length) return;
            SetChannel(channels, index, channel);
            flags[index] = channel == 0 ? 0 : ruleFlags;
        }

        private static void SetChannel(uint[] channels, uint[] flags, int index, VRCFuryHapticPlug.TagRule rule) {
            if (index < 0 || index >= channels.Length) return;
            if (rule == null) return;
            var ruleFlags = 0u;
            if (rule.allowSelf) ruleFlags |= IncludeSelf;
            if (rule.allowOthers) ruleFlags |= IncludeOthers;
            SetChannel(channels, index, HashChannel(NormalizeUserChannel(rule.tag)), ruleFlags, flags);
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

        public static void MarkSpsPropertiesAnimated(Material material) {
            var count = material.shader.GetPropertyCount();
            for (var i = 0; i < count; i++) {
                var propertyName = material.shader.GetPropertyName(i);
                if (propertyName.StartsWith("_SPS_")) {
                    material.SetOverrideTag(propertyName + "Animated", "1");
                }
            }
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
