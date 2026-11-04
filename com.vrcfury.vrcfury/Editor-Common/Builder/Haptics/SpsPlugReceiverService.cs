using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsPlugReceiverService {
        internal const float RangeInPlugLengths = 1.6f;

        [VFAutowired] private readonly SpsConfigurer configurer;
        [VFAutowired] private readonly SelfAndOthersReceiverService receivers;
        [VFAutowired] private readonly PlugSizeService sizes;
        [VFAutowired] private readonly PlugFrameService frames;
        private readonly Dictionary<VRCFuryHapticPlug, SelfAndOthersReceiverService.Receivers> cache = new();

        public SelfAndOthersReceiverService.Receivers GetReceivers(VRCFuryHapticPlug plug) =>
            cache.GetOrCreate(plug, () => {
                var size = sizes.GetWorldSize(plug);
                return receivers.Create(new SelfAndOthersReceiverService.Request {
                    frame = frames.Get(plug),
                    name = "SPS_",
                    objectName = (self, merged) => merged
                        ? self ? "Receiver - Sockets (Self Merged)" : "Receiver - Socket (Others Merged)"
                        : self ? "Receiver - Sockets (Self)" : "Receiver - Sockets (Others)",
                    worldRadius = size.worldLength * RangeInPlugLengths,
                    selfTags = GetSocketTags(plug, true).ToArray(),
                    othersTags = GetSocketTags(plug, false).ToArray()
                });
            });

        private HashSet<string> GetChannels(VRCFuryHapticPlug plug, bool self) =>
            configurer.GetPlugChannels(plug, self
                ? SpsConfigurer.ChannelMode.ReceiverSelf : SpsConfigurer.ChannelMode.ReceiverOthers);

        internal List<string> GetSocketTags(VRCFuryHapticPlug plug, bool self) {
            var tags = GetChannels(plug, self).OrderBy(c => c, StringComparer.Ordinal)
                .Select(SpsTags.FormatSocket).ToList();
            if (!self && plug.useSharedTag) {
                // Keep these immediately after the channels, in this order, for the shader.
                tags.Add(SpsTags.LegacySocketHole);
                tags.Add(SpsTags.LegacySocketRing);
            }
            return tags;
        }

        internal List<string> GetPathTags(VRCFuryHapticPlug plug) => GetChannels(plug, true)
            .Union(GetChannels(plug, false)).OrderBy(c => c, StringComparer.Ordinal)
            .Select(SpsTags.FormatSocketPath).ToList();

        internal static void RequireMaterialContacts() {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            throw new Exception("SPS deformation requires a VRCSDK with material contact support. Update the VRCSDK to use SPS.");
#endif
        }

        public void Create(VRCFuryHapticPlug plug, IEnumerable<Renderer> renderers) {
            RequireMaterialContacts();
#if VRCSDK_HAS_MATERIAL_CONTACTS
            var targets = renderers.Where(r => r != null).Distinct().ToArray();
            var receivers = GetReceivers(plug);
            var frame = frames.Get(plug);
            receivers.SetMaterials(targets, self: true, needsContactTags: false, shaderParameter: "SPS_Self");
            receivers.SetMaterials(targets, self: false, needsContactTags: true, shaderParameter: "SPS_Others");
            // Guides can extend beyond the entrance-selection range.
            var tags = GetPathTags(plug);
            if (tags.Count == 0) return;
            if (tags.Count > 16) throw new Exception("SPS plug receiver exceeded the material contact tag limit.");
            for (var i = 0; i < targets.Length; i += ContactReceiver.SHADER_MAX_UPDATED_RENDERERS) {
                var obj = GameObjects.Create("Receiver - Socket Paths" + (i == 0 ? "" : " " + i / ContactReceiver.SHADER_MAX_UPDATED_RENDERERS), frame);
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.shapeType = ContactBase.ShapeType.Sphere;
                receiver.radius = 100 / frame.worldScale.x;
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                receiver.allowSelf = true;
                receiver.allowOthers = true;
                receiver.contentTypes = DynamicsUsageFlags.Everything;
                receiver.parameter = "";
                receiver.separateShaderParameter = "SPS_Path";
                receiver.writeToMaterials = true;
                receiver.writeToMaterialsContactTags = false;
                receiver.collisionTags = new List<string>(tags);
                receiver.shaderTargetRenderers = targets.Skip(i).Take(ContactReceiver.SHADER_MAX_UPDATED_RENDERERS).ToArray();
            }
#endif
        }

        public void ConfigureMaterial(Material material, VRCFuryHapticPlug plug) {
            material.SetFloat("_SPS_OthersChannelTagCount", GetChannels(plug, false).Count);
            material.SetFloat("_SPS_OthersLegacyTags", plug.useSharedTag ? 1 : 0);
            material.SetFloat("_SPS_UseLegacyLights", plug.useSharedTag ? 1 : 0);
        }
    }
}
