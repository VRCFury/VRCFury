using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Menu;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDKBase.Validation.Performance;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticPlug), true)]
    internal class VRCFuryHapticPlugEditor : VRCFuryComponentEditor<VRCFuryHapticPlug> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticPlug target) {
            var container = new VisualElement();
            var configureTps = serializedObject.FindProperty("configureTps");
            var enableSps = serializedObject.FindProperty("enableSps");
            
            container.Add(ConstraintWarning(target));
            
            var boneWarning = VRCFuryEditorUtils.Warn(
                "WARNING: This renderer is rigged with bones, but you didn't put the SPS Plug inside a bone! When SPS is used" +
                " with rigged meshes, you should put the SPS Plug inside the bone nearest the 'base'!");
            boneWarning.SetVisible(false);
            container.Add(boneWarning);

            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("name"), "Name in connected apps"));

            var sizeSection = VRCFuryEditorUtils.Section("Size and Masking");
            container.Add(sizeSection);
            
            var autoMesh = serializedObject.FindProperty("autoRenderer");
            sizeSection.Add(VRCFuryEditorUtils.BetterProp(autoMesh, "Automatically find mesh"));
            sizeSection.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoMesh.boolValue) {
                    c.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("configureTpsMesh")));
                }
                return c;
            }, autoMesh));

            sizeSection.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!configureTps.boolValue && !enableSps.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("autoPosition"),
                        "Detect position/rotation from mesh"));
                }
                return c;
            }, configureTps, enableSps));

            var autoLength = serializedObject.FindProperty("autoLength");
            sizeSection.Add(VRCFuryEditorUtils.BetterProp(autoLength, "Detect length from mesh"));
            sizeSection.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoLength.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("length"), "Length"));
                }
                return c;
            }, autoLength));

            var autoRadius = serializedObject.FindProperty("autoRadius");
            sizeSection.Add(VRCFuryEditorUtils.BetterProp(autoRadius, "Detect radius from mesh"));
            sizeSection.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoRadius.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));
                }
                return c;
            }, autoRadius));
            
            sizeSection.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("useBoneMask"),
                "Automatically mask using bone weights"
            ));

            sizeSection.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("textureMask"),
                "Optional additional texture mask (white = 'do not deform or use in length calculations')"
            ));
            
            sizeSection.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var size = PlugSizeDetector.GetWorldSize(target);
                var text = new List<string>();
                text.Add("Attached renderers: " + size.renderers.Select(r => r.owner().name).Join(", "));
                text.Add($"Detected Length: {size.worldLength}m");
                text.Add($"Detected Radius: {size.worldRadius}m");

                text.Add("Patching Material Slots:");
                foreach (var renderer in size.matSlots.GetKeys()) {
                    text.Add($"  {renderer.name}");
                    foreach (var slot in size.matSlots.Get(renderer)) {
                        var matName = renderer.GetComponent<Renderer>()?.sharedMaterials[slot]?.name ?? "Unset";
                        text.Add($"    #{slot} (currently {matName})");
                    }
                }

                var bones = size.renderers.OfType<SkinnedMeshRenderer>()
                    .SelectMany(skin => skin.bones)
                    .Where(bone => bone != null)
                    .ToArray();
                var isInsideBone = bones.Any(bone => target.owner().IsChildOf(bone));
                var displayWarning = bones.Length > 0 && !isInsideBone;
                boneWarning.SetVisible(displayWarning);
                
                return text.Join('\n');
            }));
            
            container.Add(VRCFuryEditorUtils.BetterProp(enableSps, "Enable Deformation"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableSps.boolValue) {
                    var spsBox = VRCFuryEditorUtils.Section("Deformation (Super Plug Shader)", "This plug will deform toward SPS/TPS/DPS sockets\nCheck out vrcfury.com/sps for details");
                    c.Add(spsBox);
                    spsBox.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("spsAutorig"),
                        "Auto-Rig (If mesh is static, add bones and a physbone to make it sway)"
                    ));
                    
                    spsBox.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("postBakeActions"),
                        "Post-Bake Actions",
                        tooltip: "SPS Plug meshes should be posed 'straight' so that length and pose calculations" +
                                 " can be performed. If you'd like it to appear in a different way by default in game, you can add actions here" +
                                 " which will be applied to the avatar after the calculations are finished."
                    ));

                    var animatedProp = serializedObject.FindProperty("spsAnimatedEnabled");
                    var animatedField = new Toggle();
                    animatedField.SetValueWithoutNotify(animatedProp.floatValue > 0);
                    animatedField.RegisterValueChangedCallback(cb => {
                        animatedProp.floatValue = cb.newValue ? 1 : 0;
                        animatedProp.serializedObject.ApplyModifiedProperties();
                    });
                    spsBox.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("spsAnimatedEnabled"),
                        "Animated Toggle",
                        fieldOverride: animatedField,
                        tooltip: "You can ANIMATE this box on and off with an animation clip, in order to" +
                                 " turn deformation off during certain situations."
                    ));
                    spsBox.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("spsBlendshapes"),
                        "Animated blendshapes to keep while deforming",
                        fieldOverride: VRCFuryEditorUtils.List(serializedObject.FindProperty("spsBlendshapes")),
                        tooltip: "Usually, SPS penetrators revert blendshapes back to exactly the way they look in the editor while deforming toward a socket." +
                                 " You can specify up to 16 blendshapes in this list which can still be animated while deforming."
                    ));
                    spsBox.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("spsOverrun"),
                        "Allow Hole Overrun",
                        tooltip: "This allows the plug to extend very slightly past holes to improve collapse visuals." +
                                 " Beware that disabling this may cause plug to appear to 'fold in' near holes like a map, which may be strange."
                    ));
                }
                return c;
            }, enableSps));

            // Depth Animations
            var list = serializedObject.FindProperty("depthActions2");
            container.Add(VRCFuryEditorUtils.CheckboxList(
                list,
                "Enable Depth Animations",
                "Allows you to animate anything based on the proximity of a socket near this plug",
                "Depth Animations",
                VRCFuryEditorUtils.List(list, () => {
                    VRCFuryEditorUtils.AddToList(list, newAction => {
                        newAction.FindPropertyRelative("range").vector2Value = new Vector2(-1, 0);
                        newAction.FindPropertyRelative("units").enumValueIndex =
                            (int)VRCFuryHapticSocket.DepthActionUnits.Plugs;
                    });
                })
            ));

            container.Add(GetHapticsSection());

            var adv = new Foldout {
                text = "Advanced Plug Options",
                value = false
            };
            container.Add(adv);
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("useHipAvoidance"), "Use hip avoidance",
                tooltip: "If this plug is placed on the hip bone, this option will prevent triggering or receiving haptics or depth animations from other sockets on the hip bone."));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("unitsInMeters"), "(Deprecated) Units are in world-space"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("useLegacyRendererFinder"), "(Deprecated) Use legacy renderer search"));
            adv.Add(VRCFuryEditorUtils.BetterProp(configureTps, "(Deprecated) Auto-configure Poiyomi TPS"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("addDpsTipLight"), "(Deprecated) Add legacy DPS tip light (must enable in menu)"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("spsKeepImports"), "(Developer) Do not flatten SPS imports"));

            return container;
        }

        public static VisualElement GetHapticsSection() {
            if (HapticsToggleMenuItem.Get()) {
                return VRCFuryEditorUtils.Section("Haptics", "OGB haptic support is enabled on this plug by default");
            }
            var el = VRCFuryEditorUtils.Section("Haptics");
            el.Add(VRCFuryEditorUtils.Error("Haptics have been disabled in the VRCFury unity settings"));
            return el;
        }

        public static VisualElement ConstraintWarning(UnityEngine.Component c, bool isSocket = false) {
            var reg = new VrcRegistryConfig();
            
            return VRCFuryEditorUtils.Debug(refreshElement: () => {
                var output = new VisualElement();
                var legacyRendererPaths = new List<string>();
                var lightPaths = new List<string>();
                var tipLightPaths = new List<string>();
                var orificeLightPaths = new List<string>();
                var avatar = VRCAvatarUtils.GuessAvatarObject(c);
                if (avatar != null) {
                    foreach (var light in avatar.GetComponentsInSelfAndChildren<Light>()) {
                        if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                        var path = light.owner().GetPath(avatar, true);
                        var type = VRCFuryHapticSocketEditor.GetLegacyDpsLightType(light);
                        if (type == VRCFuryHapticSocketEditor.LegacyDpsLightType.Tip)
                            tipLightPaths.Add(path);
                        else if (type == VRCFuryHapticSocketEditor.LegacyDpsLightType.Hole ||
                                 type == VRCFuryHapticSocketEditor.LegacyDpsLightType.Ring ||
                                 type == VRCFuryHapticSocketEditor.LegacyDpsLightType.Front)
                            orificeLightPaths.Add(path);
                        else
                            lightPaths.Add(path);
                    }
                    foreach (var renderer in avatar.GetComponentsInSelfAndChildren<Renderer>()) {
                        foreach (var m in renderer.sharedMaterials) {
                            if (DpsConfigurer.IsDps(m) || TpsConfigurer.IsTps(m)) {
                                legacyRendererPaths.Add($"{m.name} in {renderer.owner().GetPath(avatar)}");
                            }
                        }
                    }
                }

                output.Clear();
                if (tipLightPaths.Any()) {
                    var warning = VRCFuryEditorUtils.Warn(
                        "This avatar still contains a DPS tip light! This means your avatar has not been fully converted to SPS," +
                        " and your DPS penetrator may cause issues if too many sockets are on nearby." +
                        " Check out https://vrcfury.com/sps for details about how to fully upgrade a DPS penetrator to an SPS plug.\n\n" +
                        tipLightPaths.Join('\n')
                    );
                    output.Add(warning);
                }
                if (orificeLightPaths.Any()) {
                    var warning = VRCFuryEditorUtils.Warn(
                        "This avatar still contains un-upgraded DPS orifice lights! This means your avatar has not been fully converted to SPS," +
                        " and your DPS orifices may cause issues if too many are active at the same time." +
                        " Check out https://vrcfury.com/sps for details about how to upgrade DPS orifices to SPS sockets.\n\n" +
                        orificeLightPaths.Join('\n')
                    );
                    output.Add(warning);
                }
                if (lightPaths.Any()) {
                    var warning = VRCFuryEditorUtils.Warn(
                        "This avatar contains point or spot lights! Beware that these lights may interfere with SPS if they are enabled at the same time.\n\n" +
                        lightPaths.Join('\n')
                    );
                    output.Add(warning);
                }
                if (legacyRendererPaths.Any()) {
                    var warning = VRCFuryEditorUtils.Warn(
                        "This avatar still contains a legacy DPS or TPS penetrator! This means your avatar has not been fully converted to SPS," +
                        " and your legacy penetrator may cause issues if too many sockets are on nearby." +
                        " Check out https://vrcfury.com/sps for details about how to fully upgrade a DPS penetrator to an SPS plug.\n\n" +
                        legacyRendererPaths.Join('\n')
                    );
                    output.Add(warning);
                }

                var inConstraints = c.owner().GetConstraints(true).Any();
                if (inConstraints) {
                    var warning = VRCFuryEditorUtils.Warn(
                        "This SPS component is used within a Constraint! " +
                        "AVOID using SPS within constraints if at all possible. " +
                        (isSocket
                            ? "Sharing one socket in multiple locations will make your avatar LESS performant, not more! "
                            : "") +
                        " Check out https://vrcfury.com/sps/constraints for details.");
                    output.Add(warning);
                }

                if (reg.TryGet("VRC_AV_INTERACT_SELF", out var val) && val != 1) {
                    output.Add(VRCFuryEditorUtils.Error(
                        "You must enable 'Settings > Avatar > Avatar Interactions > Avatar Self Interact' in the VRChat settings" +
                        " for SPS to work properly."
                    ));
                }
                if (reg.TryGet("VRC_AV_INTERACT_LEVEL", out var val2) && val2 != 2) {
                    output.Add(VRCFuryEditorUtils.Warn(
                        "You do not have 'Settings > Avatar > Avatar Interactions > Avatar Allowed to Interact' set to 'Everyone' in the VRChat settings." +
                        " This may prevent SPS from working properly with other players."
                    ));
                }
                if (reg.TryGet("PIXEL_LIGHT_COUNT", out var val3) && val3 != 3) {
                    output.Add(VRCFuryEditorUtils.Warn(
                        "Your VRChat 'Pixel Light Count' setting is not set to HIGH. This may cause SPS to work improperly in some worlds." +
                        " Please set 'Settings > Graphics > Advanced > Pixel Light Count' to 'High' in the VRChat settings."
                    ));
                }

                return output;
            });
        }

        private class GizmoCache {
            public double time = 0;
            public PlugSizeDetector.SizeResult size;
            public string error;
            public Vector3 position;
            public Quaternion rotation;
        }

        private static readonly ConditionalWeakTable<VRCFuryHapticPlug, GizmoCache> gizmoCache
            = new ConditionalWeakTable<VRCFuryHapticPlug, GizmoCache>();
        
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        //[DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFuryHapticPlug plug, GizmoType gizmoType) {
            var transform = plug.owner();
            
            var cache = gizmoCache.GetOrCreateValue(plug);
            if (cache.time == 0 || transform.worldPosition != cache.position || transform.worldRotation != cache.rotation || EditorApplication.timeSinceStartup > cache.time + 1) {
                cache.time = EditorApplication.timeSinceStartup;
                cache.position = transform.worldPosition;
                cache.rotation = transform.worldRotation;
                cache.size = null;
                cache.error = null;
                try {
                    cache.size = PlugSizeDetector.GetWorldSize(plug);
                } catch (Exception e) {
                    cache.error = e.Message;
                }
            }

            var size = cache.size;
            var worldRoot = transform.TransformPoint(Vector3.zero);
            Vector3 worldForward;
            float worldLength;
            float worldRadius;
            Color color;
            string error = null;
            if (size == null) {
                worldForward = transform.TransformDirection(Vector3.forward);
                worldLength = 0.3f;
                worldRadius = 0.05f;
                color = Color.red;
                error = cache.error;
            } else {
                worldForward = transform.TransformDirection(size.localRotation * Vector3.forward);
                worldLength = size.worldLength;
                worldRadius = size.worldRadius;
                color = new Color(1f, 0.5f, 0);
            }

            var worldEnd = worldRoot + worldForward * worldLength;
            VRCFuryGizmoUtils.DrawCappedCylinder(worldRoot, worldEnd, worldRadius, color);

            if (Selection.activeGameObject == plug.owner()) {
                VRCFuryGizmoUtils.DrawText(
                    worldRoot + (worldEnd - worldRoot) / 2,
                    "SPS Plug" + (error == null ? "" : $"\n({error})"),
                    Color.gray,
                    true
                );
            }

            Gizmos.color = Color.clear;
            var gizmoStart = worldRoot;
            var gizmoEnd = worldEnd - worldForward * worldRadius;
            var gizmoCount = 5;
            for (var i = 0; i < gizmoCount; i++) {
                Gizmos.DrawSphere(gizmoStart + (gizmoEnd - gizmoStart) * i / (gizmoCount-1), worldRadius);
            }
        }

        public static ICollection<Renderer> GetRenderers(VRCFuryHapticPlug plug) {
            var renderers = new List<Renderer>();
            if (plug.autoRenderer) {
                renderers.AddRange(PlugRendererFinder.GetAutoRenderer(plug.owner(), !plug.useLegacyRendererFinder));
            } else {
                renderers.AddRange(plug.configureTpsMesh.Where(r => r != null));
            }
            return renderers;
        }

        [CanBeNull]
        public static BakeResult Bake(
            VRCFuryHapticPlug plug,
            HapticContactsService hapticContactsService,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            bool deferMaterialConfig = false
        ) {
            var transform = plug.owner();
            if (!HapticUtils.AssertValidScale(transform, "plug", shouldThrow: !plug.sendersOnly)) {
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

            var worldSpace = GameObjects.Create("WorldSpace", localSpace);
            ConstraintUtils.MakeWorldSpace(worldSpace);

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var senders = GameObjects.Create("Senders", localSpace);
            hapticContactsService.AddSender(new HapticContactsService.SenderRequest() {
                obj = senders,
                objName = "Length",
                radius = worldLength,
                tags = new [] { HapticUtils.CONTACT_PEN_MAIN },
                useHipAvoidance = plug.useHipAvoidance
            });
            hapticContactsService.AddSender(new HapticContactsService.SenderRequest() {
                obj = senders,
                objName = "WidthHelper",
                radius = Mathf.Max(0.01f, worldLength - worldRadius*2),
                tags = new [] { HapticUtils.CONTACT_PEN_WIDTH },
                useHipAvoidance = plug.useHipAvoidance
            });
            hapticContactsService.AddSender(new HapticContactsService.SenderRequest() {
                obj = senders,
                pos = halfWay,
                objName = "Envelope",
                radius = worldRadius,
                tags = new [] { HapticUtils.CONTACT_PEN_CLOSE },
                rotation = capsuleRotation,
                height = worldLength,
                useHipAvoidance = plug.useHipAvoidance
            });
            hapticContactsService.AddSender(new HapticContactsService.SenderRequest() {
                obj = worldSpace,
                objName = "Sender - Root",
                radius = 0.01f,
                tags = new [] { HapticUtils.CONTACT_PEN_ROOT },
                useHipAvoidance = plug.useHipAvoidance
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
                        var skin = TpsConfigurer.NormalizeRenderer(renderer, localSpace, worldLength);

                        var spsBlendshapes = plug.spsBlendshapes
                            .Where(b => skin.HasBlendshape(b))
                            .Distinct()
                            .Take(16)
                            .ToArray();

                        var activeFromMask = PlugMaskGenerator.GetMask(skin, plug);
                        if (plug.enableSps && plug.spsAutorig) {
                            SpsAutoRigger.AutoRig(skin, localSpace, worldLength, worldRadius, activeFromMask);
                        }

                        var spsBaked = plug.enableSps ? SpsBaker.Bake(skin, activeFromMask, false, spsBlendshapes) : null;

                        var finishedCopies = new HashSet<Material>();
                        Material ConfigureMaterial(int slotNum, Material mat) {
                            var shouldPatch = size.matSlots.Get(skin.owner()).Contains(slotNum);
                            if (!shouldPatch) return mat;

                            try {
                                if (mat == null) return null;
                                if (!BuildTargetUtils.IsDesktop()) return mat;

                                if (plug.enableSps) {
                                    var copy = mat.Clone("Needed to swap shader to SPS");
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    SpsConfigurer.ConfigureSpsMaterial(skin, copy, worldLength,
                                        spsBaked,
                                        plug, localSpace, spsBlendshapes);
                                    return copy;
                                }
                                if (plug.configureTps && TpsConfigurer.IsTps(mat)) {
                                    var copy = mat.Clone("Needed to change properties for TPS autoconfiguration");
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    TpsConfigurer.ConfigureTpsMaterial(skin, copy, worldLength, activeFromMask);
                                    return copy;
                                }

                                return mat;
                            } catch (Exception e) {
                                throw new ExceptionWithCause($"Failed to configure material: {mat.name}", e);
                            }
                        }

                        return new RendererResult {
                            renderer = skin,
                            configureMaterial = ConfigureMaterial,
                            spsBlendshapes = spsBlendshapes
                        };
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to configure renderer: {owner.GetPath()}", e);
                    }
                }).ToArray();
            } else {
                rendererResults = renderers.Select(r => new RendererResult {
                    renderer = r,
                    configureMaterial = (slotNum,m) => m
                }).ToArray();
            }

            if (!deferMaterialConfig) {
                foreach (var r in rendererResults) {
                    r.renderer.sharedMaterials = r.renderer.sharedMaterials.Select((mat,slotNum) => r.configureMaterial(slotNum, mat)).ToArray();
                }
            }

            var name = plug.name;
            if (string.IsNullOrWhiteSpace(name)) {
                if (renderers.Count > 0) {
                    name = HapticUtils.GetName(rendererResults.First().renderer.owner());
                } else {
                    name = HapticUtils.GetName(plug.owner());
                }
            }

            return new BakeResult {
                bakeRoot = localSpace,
                worldSpace = worldSpace,
                renderers = rendererResults,
                worldLength = worldLength,
                worldRadius = worldRadius,
                name = name,
            };
        }

        public class BakeResult {
            public VFGameObject bakeRoot;
            public VFGameObject worldSpace;
            public ICollection<RendererResult> renderers;
            public float worldLength;
            public float worldRadius;
            public string name;
        }

        public class RendererResult {
            public Renderer renderer;
            public Func<int, Material, Material> configureMaterial;
            public IList<string> spsBlendshapes;
        }
    }
}
