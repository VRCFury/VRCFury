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
        [VFAutowired] private readonly SpsChannelsService channels;
        [VFAutowired] private readonly SelfAndOthersReceiverService receivers;
        [VFAutowired] private readonly PlugSizeService sizes;
        [VFAutowired] private readonly PlugFrameService frames;
        private readonly Dictionary<VRCFuryHapticPlug, SelfAndOthersReceiverService.Receivers> cache = new();

        public SelfAndOthersReceiverService.Receivers GetReceivers(VRCFuryHapticPlug plug, bool legacySocketCompatibility = true) =>
            cache.GetOrCreate(plug, () => {
                var frame = frames.Get(plug);
                var data = channels.Get(plug);
                var legacyTags = legacySocketCompatibility
                    ? new[] { SpsTags.LegacySocketHole, SpsTags.LegacySocketRing, SpsTags.LegacySocket } : Array.Empty<string>();
                IEnumerable<string> Tags(SpsChannelsService.ChannelSet set, bool self) =>
                    (self ? set.self : set.others).Select(SpsTags.FormatSocket)
                        .Concat(!self && set.isPublic ? legacyTags : Array.Empty<string>());
                return receivers.Create(new SelfAndOthersReceiverService.Request {
                    frame = frame, name = "SPS_", channels = data,
                    objectName = (self, merged) => "Receiver - Sockets (" + (self ? "Self" : "Others") + (merged ? " Merged" : "") + ")",
                    worldRadius = sizes.GetWorldSize(plug).worldLength * RangeInPlugLengths,
                    supportsLegacy = legacySocketCompatibility,
                    getTags = Tags
                });
            });

        internal static void RequireMaterialContacts() {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            throw new Exception(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#endif
        }

        public void Create(VRCFuryHapticPlug plug, IEnumerable<Renderer> renderers, bool legacySocketCompatibility = true) {
            RequireMaterialContacts();
#if VRCSDK_HAS_MATERIAL_CONTACTS
            var targets = renderers.Where(r => r != null).Distinct().ToArray();
            var main = GetReceivers(plug, legacySocketCompatibility);
            var data = main.request.channels;
            var frame = main.request.frame;
            data.OnAllSetsAvailable(sets => {
                foreach (var renderer in targets) {
                    foreach (var set in sets) {
                        var legacy = set.isPublic && legacySocketCompatibility;
                        set.clip.SetCurve(renderer, "material._SPS_OthersChannelTagCount", set.others.Length);
                        set.clip.SetCurve(renderer, "material._SPS_OthersLegacyTags", legacy ? 1 : 0);
                        set.clip.SetCurve(renderer, "material._SPS_UseLegacyLights", legacy ? 1 : 0);
                    }
                }
            });
            main.SetMaterials(targets, self: true, needsContactTags: false, shaderParameter: "SPS_Self");
            main.SetMaterials(targets, self: false, needsContactTags: true, shaderParameter: "SPS_Others");
            for (var i = 0; i < targets.Length; i += ContactReceiver.SHADER_MAX_UPDATED_RENDERERS) {
                var name = "Receiver - Socket Paths" + (i == 0 ? "" : " " + i / ContactReceiver.SHADER_MAX_UPDATED_RENDERERS);
                var pathTargets = targets.Skip(i).Take(ContactReceiver.SHADER_MAX_UPDATED_RENDERERS).ToArray();
                var localRadius = 100 / frame.worldScale.x;
                data.OnAllSetsAvailable(sets => {
                    if (sets.All(s => !s.paths.Any())) return;
                    var variants = new Dictionary<string, VFGameObject>();
                    foreach (var set in sets) {
                        var tags = set.paths.Select(SpsTags.FormatSocketPath).ToArray();
                        var owner = variants.GetOrCreate(string.Join("\n", tags), () => {
                            var obj = GameObjects.Create(sets.Count == 1 ? name
                                : name + " (" + string.Join(", ", set.paths.DefaultIfEmpty("none")) + ")", frame);
                            var receiver = obj.AddComponent<VRCContactReceiver>();
                            receiver.shapeType = ContactBase.ShapeType.Sphere;
                            receiver.radius = localRadius;
                            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                            receiver.allowSelf = receiver.allowOthers = true;
                            receiver.contentTypes = DynamicsUsageFlags.Everything;
                            receiver.parameter = "";
                            receiver.separateShaderParameter = "SPS_Path";
                            receiver.writeToMaterials = true;
                            receiver.writeToMaterialsContactTags = false;
                            receiver.collisionTags = tags.ToList();
                            receiver.shaderTargetRenderers = pathTargets;
                            return obj;
                        });
                        set.Register(owner);
                    }
                });
            }
#endif
        }

        public void ConfigureMaterial(Material material, VRCFuryHapticPlug plug, bool legacySocketCompatibility = true) {
            var set = channels.Get(plug).Default;
            material.SetFloat("_SPS_OthersChannelTagCount", set.others.Length);
            material.SetFloat("_SPS_OthersLegacyTags", set.isPublic && legacySocketCompatibility ? 1 : 0);
            material.SetFloat("_SPS_UseLegacyLights", set.isPublic && legacySocketCompatibility ? 1 : 0);
        }
    }
}
