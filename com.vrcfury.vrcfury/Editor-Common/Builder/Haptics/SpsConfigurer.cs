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
        private const uint TagMask = 0x00ffffff;
        private const uint IncludeSelf = 1;
        private const uint IncludeOthers = 2;

        public class MaterialProperty {
            public UnityEngine.Component component;
            public string propertyName;
            public float value;
        }

        public static void ConfigureSpsMaterial(
            Renderer skin,
            Material m,
            float worldLength,
            Texture2D spsBaked,
            VRCFuryHapticPlug plug,
            VFGameObject bakeRoot,
            IList<string> spsBlendshapes,
            float resolverHash
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
            m.SetFloat(SpsMarkersService.Id, resolverHash);
            m.SetFloat(SpsMarkersService.PlayerId, 0);
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
            float resolverHash,
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
            AddPackedVectors(Add, SpsBakedRadiusSamples, bakedRadiusSamples, 8);
            Add(SpsMetadataColor + ".x", metadataColor.r);
            Add(SpsMetadataColor + ".y", metadataColor.g);
            Add(SpsMetadataColor + ".z", metadataColor.b);
            Add(SpsMetadataColor + ".w", 1);
            Add(SpsEnabled, plug.spsAnimatedEnabled);
            Add(SpsOverrun, plug.spsOverrun ? 1 : 0);
            Add(SpsLegacy, plug.useLights ? 1 : 0);
            ConfigureResolverTagRules(Add, plug);
            Add(SpsMarkersService.Configured, 1);
            Add(SpsMarkersService.Id, resolverHash);
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
            float socketId,
            bool useTangentIn,
            Vector3 tangentIn,
            bool useTangentOut,
            Vector3 tangentOut,
            bool useRadiusOffset,
            int nextSocketId = 0,
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
            Add(SpsMarkersService.Id, socketId);
            Add(SpsMarkersService.SocketHole, lightType == VRCFuryHapticSocket.AddLight.Hole ? 1 : 0);
            Add(SpsMarkersService.SocketDoubleSided, lightType == VRCFuryHapticSocket.AddLight.Ring ? 1 : 0);
            Add(SpsMarkersService.SocketRadiusOffset, useRadiusOffset ? 1 : 0);
            Add(SpsMarkersService.SocketNextId, nextSocketId);
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
                || propertyName == $"material.{SpsMarkersService.PlayerId}"
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
                    set($"_SPS_SocketTag{i + 1}", 0);
                }
                return;
            }

            var closestBone = GetClosestBone(socket.owner());
            var tags = new uint[8];
            var count = 0;
            foreach (var tag in socket.tags) {
                AddTag(tags, ref count, HashTag(tag));
            }
            if (socket.useHipAvoidance && closestBone == HumanBodyBones.Hips) {
                AddTag(tags, ref count, HashTag("hips"));
            }
            AddTag(tags, ref count, GetAutoSocketTag(closestBone));
            if (socket.useSharedTag) {
                AddTag(tags, ref count, SharedTag);
            }
            for (var i = 0; i < tags.Length; i++) {
                set($"_SPS_SocketTag{i + 1}", tags[i]);
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

            hash &= TagMask;
            return hash == 0 ? 1u : hash;
        }

        private static void ConfigureResolverTagRules(Action<string, float> set, VRCFuryHapticPlug plug) {
            var onHips = IsOnHips(plug.owner());
            var includeTags = new uint[4];
            var includeFlags = new uint[4];
            var excludeTags = new uint[4];
            var excludeFlags = new uint[4];
            var includeCount = 0;
            var excludeCount = 0;

            foreach (var rule in plug.includeTags) {
                AddTag(includeTags, ref includeCount, rule, includeFlags);
            }
            if (plug.useSharedTag) {
                AddTag(includeTags, ref includeCount, SharedTag, IncludeSelf | IncludeOthers, includeFlags);
            }

            foreach (var rule in plug.excludeTags) {
                AddTag(excludeTags, ref excludeCount, rule, excludeFlags);
            }
            if (plug.useHipAvoidance && onHips) {
                AddTag(excludeTags, ref excludeCount, HashTag("hips"), IncludeSelf, excludeFlags);
            }

            for (var i = 0; i < 4; i++) {
                var slot = i + 1;
                set($"_SPS_TagInclude{slot}", includeTags[i]);
                set($"_SPS_TagInclude{slot}Self", (includeFlags[i] & IncludeSelf) != 0 ? 1 : 0);
                set($"_SPS_TagInclude{slot}Others", (includeFlags[i] & IncludeOthers) != 0 ? 1 : 0);
                set($"_SPS_TagExclude{slot}", excludeTags[i]);
                set($"_SPS_TagExclude{slot}Self", (excludeFlags[i] & IncludeSelf) != 0 ? 1 : 0);
                set($"_SPS_TagExclude{slot}Others", (excludeFlags[i] & IncludeOthers) != 0 ? 1 : 0);
            }
        }

        private static void AddTag(uint[] tags, ref int count, uint tag, uint ruleFlags = 0, uint[] flags = null) {
            if (tag == 0 || count >= tags.Length) return;
            tags[count] = tag;
            if (flags != null) {
                flags[count] = ruleFlags;
            }
            count++;
        }

        private static void AddTag(uint[] tags, ref int count, VRCFuryHapticPlug.TagRule rule, uint[] flags = null) {
            if (rule == null) return;
            var ruleFlags = 0u;
            if (rule.allowSelf) ruleFlags |= IncludeSelf;
            if (rule.allowOthers) ruleFlags |= IncludeOthers;
            AddTag(tags, ref count, HashTag(rule.tag), ruleFlags, flags);
        }

        private static HumanBodyBones? GetClosestBone(VFGameObject obj) {
            return VRCFuryHapticSocketEditor.getClosestBone?.Invoke(obj);
        }

        private static bool IsOnHips(VFGameObject obj) {
            return GetClosestBone(obj) == HumanBodyBones.Hips;
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

        private static uint GetAutoSocketTag(HumanBodyBones? bone) {
            switch (bone) {
                case HumanBodyBones.Head:
                case HumanBodyBones.Jaw:
                    return HashTag("head");
                case HumanBodyBones.LeftHand:
                case HumanBodyBones.RightHand:
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm:
                    return HashTag("hand");
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                case HumanBodyBones.LeftToes:
                case HumanBodyBones.RightToes:
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg:
                    return HashTag("foot");
                default:
                    return 0;
            }
        }
    }
}
