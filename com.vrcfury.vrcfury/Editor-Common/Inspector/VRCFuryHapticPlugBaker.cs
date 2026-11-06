using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Injector;
using VF.Utils;
using BakeResult = VF.Inspector.VRCFuryHapticPlugEditor.BakeResult;
using RendererResult = VF.Inspector.VRCFuryHapticPlugEditor.RendererResult;

namespace VF.Inspector {
    [VFService]
    internal class VRCFuryHapticPlugBaker {
        [VFAutowired] private readonly SpsConfigurer spsConfigurer;
        [VFAutowired] private readonly SpsPlugReceiverService spsPlugReceiverService;
        [VFAutowired] private readonly PlugSizeService plugSizes;
        [VFAutowired] private readonly PlugFrameService plugFrames;

        public BakeResult Bake(
            VRCFuryHapticPlug plug,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            bool deferMaterialConfig = false,
            bool legacySocketCompatibility = true
        ) {
            var transform = plug.owner();
            HapticUtils.AssertValidScale(transform);

            var size = plugSizes.GetWorldSize(plug);
            var renderers = size.renderers;
            var worldLength = size.worldLength;
            var worldRadius = size.worldRadius;

            if (usedRenderers != null) {
                foreach (var r in renderers) {
                    var rendererObject = r.owner();
                    if (usedRenderers.TryGetValue(rendererObject, out var otherPlug)) {
                        throw new Exception(
                            "Multiple SPS Plugs target the same renderer. This is probably a mistake. " +
                            "Maybe there was an extra created by accident?\n\n" +
                            $"Renderer: {r.owner().GetDebugPath()}\n\n" +
                            $"Plug 1: {otherPlug.owner().GetDebugPath()}\n\n" +
                            $"Plug 2: {plug.owner().GetDebugPath()}");
                    }
                    usedRenderers[rendererObject] = plug;
                }
            }

            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var capsuleRotation = Quaternion.Euler(90,0,0);

            var localSpace = plugFrames.Get(plug);
            var scale = localSpace.worldScale.x;
            var localLength = worldLength / scale;
            // Measure before auto-rigging or post-bake actions change the renderer.
            var radiusCurve = PlugRadiusCurve.GetRadiusCurve(plug, size, scale);
            var socketReceivers = spsPlugReceiverService.GetReceivers(plug);

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var channels = spsConfigurer.GetPlugChannels(plug, SpsConfigurer.ChannelMode.Sender);
            var legacySuffixes = new List<string>();
            if (legacySocketCompatibility && channels.Contains(SpsTags.PublicChannel)) legacySuffixes.Add("");
            var mainSender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = localSpace,
                objName = "Sender - Sphere",
                radius = worldLength,
                worldScale = HapticUtils.EncodePlugScale(worldLength, worldRadius),
                tags = channels.Select(SpsTags.FormatPlug)
                    .Concat(legacySuffixes.Select(suffix => SpsTags.LegacyPlugSphere + suffix)).ToArray()
            });
            if (legacySuffixes.Count > 0) {
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = localSpace,
                    objName = "Sender - Legacy Width",
                    radius = Mathf.Max(0.01f, worldLength - worldRadius*2),
                    tags = legacySuffixes.Select(suffix => SpsTags.LegacyPlugWidth + suffix).ToArray()
                });
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = localSpace,
                    objName = "Sender - Legacy Root",
                    radius = 0,
                    tags = legacySuffixes.Select(suffix => SpsTags.LegacyPlugRoot + suffix).ToArray()
                });
            }
            HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = localSpace,
                pos = halfWay,
                objName = "Sender - Envelope",
                radius = worldRadius,
                tags = new[] { SpsTags.PlugClose },
                rotation = capsuleRotation,
                height = worldLength
            });

            // TODO: Check if there are 0 renderers,
            // or if there are 0 materials on any of the renderers

            RendererResult[] rendererResults;

            if (plug.enableSps) {
                if (renderers.Count == 0) {
                    throw new Exception(
                        "VRCFury SPS Plug has 'Enable Deformation' checked, but no renderer was found.");
                }

                rendererResults = renderers.Select(renderer => {
                    var owner = renderer.owner();
                    try {
                        var spsBlendshapes = plug.spsBlendshapes
                            .Where(renderer.HasBlendshape)
                            .Distinct()
                            .Take(16)
                            .ToArray();

                        var activeFromMask = PlugMaskGenerator.GetMask(renderer, plug);
                        if (plug.spsAutorig) {
                            renderer = SpsAutoRigger.AutoRig(renderer, localSpace, worldLength, worldRadius, activeFromMask);
                        }
                        SpsConfigurer.ExtendBoundsForSps(renderer, localSpace, worldLength);

                        var spsBaked = SpsBaker.Bake(renderer, localSpace, activeFromMask, spsBlendshapes);

                        var spsCopies = new Dictionary<Material, Material>();
                        Material ConfigureMaterial(int slotNum, Material mat) {
                            var shouldPatch = size.matSlots.Get(renderer.owner()).Contains(slotNum);
                            if (!shouldPatch) return mat;

                            try {
                                if (mat == null) return null;
                                if (!BuildTargetUtils.IsDesktop()) return mat;

                                if (spsCopies.TryGetValue(mat, out var configured)) return configured;
                                // Bake data belongs to this renderer, even if another
                                // feature already made the source material mutable.
                                var copy = mat.Clone("Needed to swap shader to SPS", forceClone: true);
                                SpsConfigurer.ConfigureSpsMaterial(renderer, copy, worldLength, worldRadius,
                                    spsBaked,
                                    plug, localSpace, spsBlendshapes);
                                spsPlugReceiverService.ConfigureMaterial(copy, plug, legacySocketCompatibility);
                                spsCopies[mat] = copy;
                                return copy;
                            } catch (Exception e) {
                                throw new ExceptionWithCause($"Failed to configure material: {mat.name}", e);
                            }
                        }

                        return new RendererResult {
                            renderer = renderer,
                            configureMaterial = ConfigureMaterial,
                            spsBlendshapes = spsBlendshapes,
                            activeFromMask = activeFromMask
                        };
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to configure renderer: {owner.GetDebugPath()}", e);
                    }
                }).ToArray();
            } else {
                rendererResults = renderers.Select(r => new RendererResult {
                    renderer = r,
                    configureMaterial = (slotNum,m) => m,
                    activeFromMask = null
                }).ToArray();
            }

            if (plug.enableSps && BuildTargetUtils.IsDesktop()) {
                spsPlugReceiverService.Create(plug, rendererResults.Select(r => r.renderer));
            }

            if (!deferMaterialConfig) {
                foreach (var r in rendererResults) {
                    r.renderer.sharedMaterials = r.renderer.sharedMaterials.Select((mat,slotNum) => r.configureMaterial(slotNum, mat)).ToArray();
                }
            }

            var oscId = VRCFuryHapticPlugEditor.GetOscId(plug);

            return new BakeResult {
                bakeRoot = localSpace,
                renderers = rendererResults,
                localLength = localLength,
                localRadius = worldRadius / scale,
                // A 1mm floor at bake time, capped by length, follows subsequent resizing.
                localMinimumRadius = Mathf.Min(0.001f, worldLength) / scale,
                mainSender = mainSender,
                radiusCurve = radiusCurve,
                socketReceivers = socketReceivers,
                oscId = oscId,
            };
        }

    }
}
