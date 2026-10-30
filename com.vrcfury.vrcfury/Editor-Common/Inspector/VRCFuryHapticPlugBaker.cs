using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
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

        [CanBeNull]
        public BakeResult Bake(
            VRCFuryHapticPlug plug,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            bool deferMaterialConfig = false
        ) {
            var transform = plug.owner();
            if (!HapticUtils.AssertValidScale(transform, "plug", shouldThrow: !plug.fromSpsForAll)) {
                return null;
            }

            var size = PlugSizeDetector.GetWorldSize(plug);
            var renderers = size.renderers;
            var worldLength = size.worldLength;
            var worldRadius = size.worldRadius;
            var localRotation = size.localRotation;
            var localPosition = size.localPosition;

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

            var localSpace = GameObjects.Create("BakedSpsPlug", transform);
            localSpace.localPosition = localPosition;
            localSpace.localRotation = localRotation;

            var oneSpace = GameObjects.Create("OneSpace", localSpace);
            oneSpace.worldScale = Vector3.one;

            var worldSpace = GameObjects.Create("WorldSpace", localSpace);
            ConstraintUtils.MakeWorldSpace(worldSpace);

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var senders = GameObjects.Create("Senders", localSpace);
            var channels = spsConfigurer.GetPlugChannels(plug, SpsConfigurer.ChannelMode.Sender);
            var legacySuffixes = new List<string>();
            if (channels.Contains(SpsTags.PublicChannel)) legacySuffixes.Add("");
            if (channels.Contains(SpsTags.NotHipsChannel)) legacySuffixes.Add("_SelfNotOnHips");
            var mainSender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = senders,
                objName = "Length",
                radius = worldLength,
                worldScale = HapticUtils.EncodePlugScale(worldLength, worldRadius),
                tags = channels.Select(SpsTags.FormatPlug)
                    .Append(SpsTags.ScaleV1)
                    .Concat(legacySuffixes.Select(suffix => SpsTags.LegacyPlugSphere + suffix)).ToArray()
            });
            if (mainSender != null) {
                VFConstraint.CreateScale(mainSender, new Vector3(1, HapticUtils.ScaleVersion, 1), Axis.Y)
                    .AddSource(worldSpace, 1);
            }
            if (legacySuffixes.Count > 0) {
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = senders,
                    objName = "WidthHelper",
                    radius = Mathf.Max(0.01f, worldLength - worldRadius*2),
                    tags = legacySuffixes.Select(suffix => SpsTags.LegacyPlugWidth + suffix).ToArray()
                });
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = worldSpace,
                    objName = "Sender - Root",
                    radius = 0.01f,
                    tags = legacySuffixes.Select(suffix => SpsTags.LegacyPlugRoot + suffix).ToArray()
                });
            }
            HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = senders,
                pos = halfWay,
                objName = "Envelope",
                radius = worldRadius,
                tags = new[] { SpsTags.PlugClose },
                rotation = capsuleRotation,
                height = worldLength
            });

            // TODO: Check if there are 0 renderers,
            // or if there are 0 materials on any of the renderers

            RendererResult[] rendererResults;

            if (plug.configureTps || plug.enableSps) {
                var checkboxName = plug.enableSps ? "Enable Deformation" : "Auto-Configure TPS";
                if (renderers.Count == 0) {
                    throw new Exception(
                        $"VRCFury SPS Plug has '{checkboxName}' checked, but no renderer was found.");
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
                        if (plug.enableSps && plug.spsAutorig) {
                            renderer = SpsAutoRigger.AutoRig(renderer, localSpace, worldLength, worldRadius, activeFromMask);
                        }
                        if (plug.enableSps) {
                            SpsConfigurer.ExtendBoundsForSps(renderer, localSpace, worldLength);
                        }

                        var spsBaked = plug.enableSps ? SpsBaker.Bake(renderer, localSpace, activeFromMask, false, spsBlendshapes) : null;

                        var finishedCopies = new HashSet<Material>();
                        Material ConfigureMaterial(int slotNum, Material mat) {
                            var shouldPatch = size.matSlots.Get(renderer.owner()).Contains(slotNum);
                            if (!shouldPatch) return mat;

                            try {
                                if (mat == null) return null;
                                if (!BuildTargetUtils.IsDesktop()) return mat;

                                if (plug.enableSps) {
                                    var copy = mat.Clone("Needed to swap shader to SPS");
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    SpsConfigurer.ConfigureSpsMaterial(renderer, copy, worldLength,
                                        spsBaked,
                                        plug, localSpace, spsBlendshapes);
                                    return copy;
                                }
                                if (plug.configureTps && TpsConfigurer.IsTps(mat)) {
                                    var copy = mat.Clone("Needed to change properties for TPS autoconfiguration");
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    TpsConfigurer.ConfigureTpsMaterial(renderer, localSpace, copy, worldLength, activeFromMask);
                                    return copy;
                                }

                                return mat;
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

            if (!deferMaterialConfig) {
                foreach (var r in rendererResults) {
                    r.renderer.sharedMaterials = r.renderer.sharedMaterials.Select((mat,slotNum) => r.configureMaterial(slotNum, mat)).ToArray();
                }
            }

            var oscId = VRCFuryHapticPlugEditor.GetOscId(plug);

            return new BakeResult {
                bakeRoot = localSpace,
                oneSpace = oneSpace,
                worldSpace = worldSpace,
                renderers = rendererResults,
                worldLength = worldLength,
                worldRadius = worldRadius,
                oscId = oscId,
            };
        }

    }
}
