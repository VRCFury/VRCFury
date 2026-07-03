using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Component;
using VF.Hooks;
using VF.Inspector;
using VF.Menu;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Builder.Haptics {
    internal static class SpsConfigurer {
        public const uint SharedTag = 1337;
        public const string SpsEnabled = "_SPS_Enabled";
        public const string SpsBakedLength = "_SPS_BakedLength";
        public const string SpsBakedRadius = "_SPS_BakedRadius";
        public const string SpsBakedRadiusSamples = "_SPS_BakedRadiusSamples";
        public const string SpsMetadataColor = "_SPS_MetadataColor";
        public const string SpsOverrun = "_SPS_Overrun";
        public const string SpsLegacy = "_SPS_Legacy";
        private const string SpsBake = "_SPS_Bake";
        private const uint IncludeSelf = 1;
        private const uint IncludeOthers = 2;

        public class MaterialProperty {
            public UnityEngine.Component component;
            public string propertyName;
            public float value;
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

            SpsPatcher.Patch(m, SpsDevModeMenuItem.Get() && !IsActuallyUploadingHook.Get());
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
            m.SetTexture(SpsBake, spsBaked);
            m.SetFloat(SpsMarkersService.Configured, 1);
            m.SetFloat(SpsMarkersService.IdLow, SpsMarkersService.GetLow(resolverHash));
            m.SetFloat(SpsMarkersService.IdHigh, SpsMarkersService.GetHigh(resolverHash));
            m.SetFloat(SpsMarkersService.PlayerIdLow, 0);
            m.SetFloat(SpsMarkersService.PlayerIdHigh, 0);
            m.SetFloat("_SPS_BlendshapeCount", spsBlendshapes.Count);
            m.SetFloat("_SPS_BlendshapeVertCount", skin.GetVertexCount());
            for (var i = 0; i < spsBlendshapes.Count; i++) {
                var name = spsBlendshapes[i];
                if (skin.HasBlendshape(name)) {
                    m.SetFloat("_SPS_Blendshape" + i, skin.GetBlendshapeWeight(name));
                }
            }
        }

        public static List<MaterialProperty> GetResolverProperties(
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
            Add(SpsMetadataColor + ".x", metadataColor.r);
            Add(SpsMetadataColor + ".y", metadataColor.g);
            Add(SpsMetadataColor + ".z", metadataColor.b);
            Add(SpsMetadataColor + ".w", 1);
            Add(SpsEnabled, plug.spsAnimatedEnabled);
            Add(SpsOverrun, plug.spsOverrun ? 1 : 0);
            Add(SpsLegacy, plug.useLights ? 1 : 0);
            ConfigureResolverTagRules(Add, plug);
            Add(SpsMarkersService.Configured, 1);
            SetSplitId(Add, SpsMarkersService.IdLow, SpsMarkersService.IdHigh, resolverHash);
            return properties;
        }

        private static void AddPackedVectors(Action<string, float> add, string baseName, Vector4[] values, int count) {
            var components = new[] { ".x", ".y", ".z", ".w" };
            for (var i = 0; i < count; i++) {
                var value = values != null && i < values.Length ? values[i] : Vector4.zero;
                var property = $"{baseName}{i}";
                add(property + components[0], value.x);
                add(property + components[1], value.y);
                add(property + components[2], value.z);
                add(property + components[3], value.w);
            }
        }

        public static List<MaterialProperty> GetSocketProperties(
            Renderer renderer,
            VRCFuryHapticSocket socket,
            VRCFuryHapticSocket.AddLight lightType,
            uint socketId,
            bool useTangentIn,
            Vector3 tangentIn,
            bool useTangentOut,
            Vector3 tangentOut,
            bool useRadiusOffset,
            uint nextSocketId = 0,
            bool includeTags = true
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
            ConfigureSocketTags(Add, socket, includeTags);
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

        public static void AddMaterialPropertyAnimator(IEnumerable<MaterialProperty> properties) {
            var propertyList = (properties ?? new List<MaterialProperty>())
                .Where(property => property?.component != null)
                .ToList();
            if (propertyList.Count == 0) return;

            foreach (var group in propertyList.GroupBy(property => property.component.owner())) {
                AddMaterialPropertyAnimator(group.Key, group);
            }
        }

        private static void AddMaterialPropertyAnimator(VFGameObject obj, IEnumerable<MaterialProperty> properties) {
            var clip = VrcfObjectFactory.Create<AnimationClip>();
            clip.name = "SpsMaterialProperties";

            var controller = VrcfObjectFactory.Create<AnimatorController>();
            controller.name = "SpsMaterialProperties";
            new VFController(controller)
                .NewLayer("SPS Material Properties")
                .NewState("Properties")
                .WithAnimation(clip);

            AddMaterialPropertyCurves(clip, obj, properties);

            var animator = obj.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
        }

        public static void AddMaterialPropertyCurves(
            AnimationClip clip,
            VFGameObject animatorObject,
            IEnumerable<MaterialProperty> properties
        ) {
            foreach (var property in properties ?? new List<MaterialProperty>()) {
                if (property?.component == null) continue;
                var component = property.component;
                var owner = component.owner();
                var path = owner.GetPath(animatorObject);
                clip.SetCurve(path, component.GetType(), property.propertyName, property.value);
            }
        }

        private static void ConfigureSocketTags(Action<string, float> set, VRCFuryHapticSocket socket, bool includeTags = true) {
            if (!includeTags) {
                for (var i = 0; i < 8; i++) {
                    SetSplitId(set, $"_SPS_SocketTag{i + 1}Low", $"_SPS_SocketTag{i + 1}High", 0);
                }
                return;
            }

            var closestBone = GetClosestBone(socket.owner());
            var tags = new uint[8];
            for (var i = 0; i < socket.tags.Count && i < VRCFuryHapticSocketEditor.SpsTagCount; i++) {
                SetTag(tags, i, HashTag(socket.tags[i]));
            }
            if (socket.useSharedTag) {
                var autoTags = GetAutoSocketTags(closestBone, socket);
                if (autoTags.Count >= 1) SetTag(tags, 5, autoTags[0]);
                if (autoTags.Count >= 2) SetTag(tags, 6, autoTags[1]);
                SetTag(tags, 7, SharedTag);
            }
            for (var i = 0; i < tags.Length; i++) {
                SetSplitId(set, $"_SPS_SocketTag{i + 1}Low", $"_SPS_SocketTag{i + 1}High", tags[i]);
            }
        }

        public static uint HashTag(string tag) {
            if (string.IsNullOrWhiteSpace(tag)) return 0;

            var normalized = tag.Trim().ToLowerInvariant();
            uint hash = 2166136261;
            foreach (var c in normalized) {
                hash ^= c;
                hash *= 16777619;
            }

            return hash == 0 ? 1u : hash;
        }

        private static void ConfigureResolverTagRules(Action<string, float> set, VRCFuryHapticPlug plug) {
            var onHips = IsOnHips(plug.owner());
            var includeTags = new uint[4];
            var includeFlags = new uint[4];
            var excludeTags = new uint[4];
            var excludeFlags = new uint[4];
            for (var i = 0; i < plug.includeTags.Count && i < VRCFuryHapticPlugEditor.SpsTagRuleCount; i++) {
                SetTag(includeTags, includeFlags, i, plug.includeTags[i]);
            }
            if (plug.useSharedTag) {
                SetTag(includeTags, 3, SharedTag, IncludeSelf | IncludeOthers, includeFlags);
            }

            for (var i = 0; i < plug.excludeTags.Count && i < VRCFuryHapticPlugEditor.SpsTagRuleCount; i++) {
                SetTag(excludeTags, excludeFlags, i, plug.excludeTags[i]);
            }
            if (plug.useHipAvoidance && onHips) {
                SetTag(excludeTags, 3, HashTag("hips"), IncludeSelf, excludeFlags);
            }

            for (var i = 0; i < 4; i++) {
                var slot = i + 1;
                SetSplitId(set, $"_SPS_TagInclude{slot}Low", $"_SPS_TagInclude{slot}High", includeTags[i]);
                set($"_SPS_TagInclude{slot}Self", (includeFlags[i] & IncludeSelf) != 0 ? 1 : 0);
                set($"_SPS_TagInclude{slot}Others", (includeFlags[i] & IncludeOthers) != 0 ? 1 : 0);
                SetSplitId(set, $"_SPS_TagExclude{slot}Low", $"_SPS_TagExclude{slot}High", excludeTags[i]);
                set($"_SPS_TagExclude{slot}Self", (excludeFlags[i] & IncludeSelf) != 0 ? 1 : 0);
                set($"_SPS_TagExclude{slot}Others", (excludeFlags[i] & IncludeOthers) != 0 ? 1 : 0);
            }
        }

        private static void SetTag(uint[] tags, int index, uint tag) {
            if (index < 0 || index >= tags.Length) return;
            tags[index] = tag;
        }

        private static void SetTag(uint[] tags, int index, uint tag, uint ruleFlags, uint[] flags) {
            if (index < 0 || index >= tags.Length) return;
            SetTag(tags, index, tag);
            flags[index] = tag == 0 ? 0 : ruleFlags;
        }

        private static void SetTag(uint[] tags, uint[] flags, int index, VRCFuryHapticPlug.TagRule rule) {
            if (index < 0 || index >= tags.Length) return;
            if (rule == null) return;
            var ruleFlags = 0u;
            if (rule.allowSelf) ruleFlags |= IncludeSelf;
            if (rule.allowOthers) ruleFlags |= IncludeOthers;
            SetTag(tags, index, HashTag(rule.tag), ruleFlags, flags);
        }

        private static HumanBodyBones? GetClosestBone(VFGameObject obj) {
            return VRCFuryHapticSocketEditor.getClosestBone?.Invoke(obj);
        }

        private static bool IsOnHips(VFGameObject obj) {
            return GetClosestBone(obj) == HumanBodyBones.Hips;
        }

        private static uint GetAutoHipFrontBackTag(VRCFuryHapticSocket socket, HumanBodyBones? closestBone) {
            if (closestBone != HumanBodyBones.Hips) return 0;

            var root = socket.owner().root;
            var hipSockets = root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(other => other != null && GetClosestBone(other.owner()) == HumanBodyBones.Hips)
                .OrderBy(other => other.owner().worldPosition.y)
                .ThenBy(other => other.owner().GetPath(root))
                .Take(2)
                .ToList();

            if (hipSockets.Count != 2) return 0;

            var hips = VRCFuryHapticSocketEditor.getBoneOnArmature?.Invoke(root, HumanBodyBones.Hips);
            var rightHand = VRCFuryHapticSocketEditor.getBoneOnArmature?.Invoke(root, HumanBodyBones.RightHand);
            if (hips == null || rightHand == null) return 0;

            var right = rightHand.worldPosition - hips.worldPosition;
            if (right.sqrMagnitude <= 0.000001f) return 0;

            var up = Vector3.up;

            var forward = Vector3.Cross(right.normalized, up.normalized);
            if (forward.sqrMagnitude <= 0.000001f) return 0;
            forward.Normalize();

            var midpoint = (hipSockets[0].owner().worldPosition + hipSockets[1].owner().worldPosition) * 0.5f;
            hipSockets = hipSockets
                .OrderBy(other => Vector3.Dot(other.owner().worldPosition - midpoint, forward))
                .ThenBy(other => other.owner().GetPath(root))
                .ToList();

            return hipSockets[0] == socket
                ? HashTag("hipsback")
                : hipSockets[1] == socket
                    ? HashTag("hipsfront")
                    : 0;
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

        private static IList<uint> GetAutoSocketTags(HumanBodyBones? bone, VRCFuryHapticSocket socket) {
            switch (bone) {
                case HumanBodyBones.Hips:
                    return new[] { HashTag("hips"), GetAutoHipFrontBackTag(socket, bone), };
                case HumanBodyBones.Head:
                case HumanBodyBones.Jaw:
                    return new[] { HashTag("head"), };
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                    return new[] { HashTag("chest"), };
                case HumanBodyBones.LeftHand:
                    return new[] { HashTag("hand"), HashTag("handleft"), };
                case HumanBodyBones.RightHand:
                    return new[] { HashTag("hand"), HashTag("handright"), };
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.LeftToes:
                    return new[] { HashTag("foot"), HashTag("footleft"), };
                case HumanBodyBones.RightFoot:
                case HumanBodyBones.RightToes:
                    return new[] { HashTag("foot"), HashTag("footright"), };
            }

            return new uint[] { };
        }
    }
}
