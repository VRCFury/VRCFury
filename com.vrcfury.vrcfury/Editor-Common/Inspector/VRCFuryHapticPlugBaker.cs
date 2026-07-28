using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
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
        [VFAutowired] private readonly SpsMarkersService spsMarkers;
        [VFAutowired] [CanBeNull] private readonly SpsAutoTagGenerator autoTagGenerator;
        [VFAutowired] private readonly SpsConfigurer spsConfigurer;

        [CanBeNull]
        public BakeResult Bake(
            VRCFuryHapticPlug plug,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            bool deferMaterialConfig = false
        ) {
            var isOnHips = autoTagGenerator?.GetClosestBone(plug.owner()) == HumanBodyBones.Hips;
            var transform = plug.owner();
            if (!HapticUtils.AssertValidScale(transform, "plug", shouldThrow: !plug.fromSpsForAll)) {
                return null;
            }

            var resolverHash = spsMarkers.NewMarkerId();

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
                            $"Renderer: {r.owner().GetPath()}\n\n" +
                            $"Plug 1: {otherPlug.owner().GetPath()}\n\n" +
                            $"Plug 2: {plug.owner().GetPath()}");
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
            HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = senders,
                objName = "Length",
                radius = worldLength,
                tags = new [] { HapticUtils.CONTACT_PEN_MAIN },
                useHipAvoidance = plug.useHipAvoidance,
                isOnHips = isOnHips
            });
            HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = senders,
                objName = "WidthHelper",
                radius = Mathf.Max(0.01f, worldLength - worldRadius*2),
                tags = new [] { HapticUtils.CONTACT_PEN_WIDTH },
                useHipAvoidance = plug.useHipAvoidance,
                isOnHips = isOnHips
            });
            HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = senders,
                pos = halfWay,
                objName = "Envelope",
                radius = worldRadius,
                tags = new [] { HapticUtils.CONTACT_PEN_CLOSE },
                rotation = capsuleRotation,
                height = worldLength,
                useHipAvoidance = plug.useHipAvoidance,
                isOnHips = isOnHips
            });
            HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                obj = worldSpace,
                objName = "Sender - Root",
                radius = 0.01f,
                tags = new [] { HapticUtils.CONTACT_PEN_ROOT },
                useHipAvoidance = plug.useHipAvoidance,
                isOnHips = isOnHips
            });

            // TODO: Check if there are 0 renderers,
            // or if there are 0 materials on any of the renderers

            RendererResult[] rendererResults;
            MeshRenderer resolverRenderer = null;
            List<SpsConfigurer.MaterialProperty> resolverMaterialProperties = null;

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
                                        plug, localSpace, spsBlendshapes, resolverHash);
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
                        throw new ExceptionWithCause($"Failed to configure renderer: {owner.GetPath()}", e);
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

            if (plug.enableSps) {
                var bakedRadiusSamples = SpsBaker.GetPackedResolverRadiusSamples(
                    rendererResults.Select(r => new SpsBaker.RendererBakeInput {
                        renderer = r.renderer,
                        activeFromMask = r.activeFromMask
                    }),
                    localSpace,
                    worldLength
                );
                var metadataColor = SpsColorSampler.GetColor(rendererResults.Select(r => r.renderer));
                var resolverObj = GameObjects.Create("SpsResolver", oneSpace);
                resolverObj.AddComponent<MeshFilter>();
                var meshRenderer = resolverObj.AddComponent<MeshRenderer>();
                spsMarkers.ConfigureResolverRenderer(meshRenderer);
                resolverMaterialProperties = spsConfigurer.GetResolverProperties(
                    meshRenderer,
                    worldLength,
                    worldRadius,
                    bakedRadiusSamples,
                    metadataColor,
                    resolverHash,
                    plug
                );
                resolverObj.AddComponent<VRCFuryHideGizmoUnlessSelected>();
                resolverObj.AddComponent<VRCFurySpsGreenScreenFix>();
                resolverRenderer = meshRenderer;
            }

            var oscId = VRCFuryHapticPlugEditor.GetOscId(plug);

            return new BakeResult {
                bakeRoot = localSpace,
                oneSpace = oneSpace,
                worldSpace = worldSpace,
                renderers = rendererResults,
                resolverRenderer = resolverRenderer,
                worldLength = worldLength,
                worldRadius = worldRadius,
                resolverMaterialProperties = resolverMaterialProperties,
                oscId = oscId,
            };
        }

    }
}
