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
using VF.Utils;
using VRC.Dynamics;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticPlug), true)]
    public class VRCFuryHapticPlugEditor : VRCFuryComponentEditor<VRCFuryHapticPlug> {
        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticPlug target) {
            var container = new VisualElement();
            var configureTps = serializedObject.FindProperty("configureTps");
            var enableSps = serializedObject.FindProperty("enableSps");
            
            container.Add(ConstraintWarning(target.gameObject));
            
            var boneWarning = VRCFuryEditorUtils.Warn(
                "WARNING: This renderer is rigged with bones, but you didn't put the Haptic Plug inside a bone! When SPS is used" +
                " with rigged meshes, you should put the Haptic Plug inside the bone nearest the 'base'!");
            boneWarning.style.display = DisplayStyle.None;
            container.Add(boneWarning);

            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("name"), "Name in connected apps"));

            var sizeSection = VRCFuryEditorUtils.Section("Size and Masking");
            container.Add(sizeSection);
            
            var autoMesh = serializedObject.FindProperty("autoRenderer");
            sizeSection.Add(VRCFuryEditorUtils.BetterCheckbox(autoMesh, "Automatically find mesh"));
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
                    c.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("autoPosition"),
                        "Detect position/rotation from mesh"));
                }
                return c;
            }, configureTps, enableSps));

            var autoLength = serializedObject.FindProperty("autoLength");
            sizeSection.Add(VRCFuryEditorUtils.BetterCheckbox(autoLength, "Detect length from mesh"));
            sizeSection.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoLength.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("length"), "Length"));
                }
                return c;
            }, autoLength));

            var autoRadius = serializedObject.FindProperty("autoRadius");
            sizeSection.Add(VRCFuryEditorUtils.BetterCheckbox(autoRadius, "Detect radius from mesh"));
            sizeSection.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoRadius.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));
                }
                return c;
            }, autoRadius));
            
            sizeSection.Add(VRCFuryEditorUtils.BetterCheckbox(
                serializedObject.FindProperty("useBoneMask"),
                "Automatically mask using bone weights"
            ));

            sizeSection.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("textureMask"),
                "Optional additional texture mask (white = 'do not deform or use in length calculations')"
            ));
            
            sizeSection.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var size = PlugSizeDetector.GetWorldSize(target);
                var text = new List<string> {
                    "Attached renderers: " + string.Join(", ", size.renderers.Select(r => r.owner().name)),
                    $"Detected Length: {size.worldLength}m",
                    $"Detected Radius: {size.worldRadius}m"
                };

                var bones = size.renderers.OfType<SkinnedMeshRenderer>()
                    .SelectMany(skin => skin.bones)
                    .Where(bone => bone != null)
                    .ToArray();
                var isInsideBone = bones.Any(bone => target.transform.IsChildOf(bone));
                var displayWarning = bones.Length > 0 && !isInsideBone;
                boneWarning.style.display = displayWarning ? DisplayStyle.Flex : DisplayStyle.None;
                
                return string.Join("\n", text);
            }));
            
            container.Add(VRCFuryEditorUtils.BetterCheckbox(enableSps, "Enable SPS (Super Plug Shader)"));
                        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!enableSps.boolValue) return c;
                var spsBox = VRCFuryEditorUtils.Section("SPS (Super Plug Shader)", "This plug will deform toward SPS/TPS/DPS sockets\nCheck out vrcfury.com/sps for details");
                c.Add(spsBox);
                spsBox.Add(VRCFuryEditorUtils.BetterCheckbox(
                    serializedObject.FindProperty("spsAutorig"),
                    "Auto-Rig (If mesh is static, add bones and a physbone to make it sway)",
                    style: style => { style.paddingBottom = 5; }
                ));
                    
                spsBox.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("postBakeActions"),
                    "Post-Bake Actions",
                    tooltip: "Haptic Plug meshes should be posed 'straight' so that length and pose calculations" +
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
                             " turn SPS off during certain situations."
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
                return c;
            }, enableSps));

            var enableDepthAnimationsProp = serializedObject.FindProperty("enableDepthAnimations");
            container.Add(VRCFuryEditorUtils.BetterProp(
                enableDepthAnimationsProp,
                "Enable Depth Animations",
                tooltip: "Allows you to animate anything based on the proximity of a socket near this plug"
            ));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!enableDepthAnimationsProp.boolValue) return new VisualElement();
                var da = VRCFuryEditorUtils.Section("Depth Animations");
                
                da.Add(VRCFuryEditorUtils.Info(
                    "If you provide a non-static (moving) animation clip, the clip will run from start " +
                    "to end depending on penetration depth. Otherwise, it will animate from 'off' to 'on' depending on depth."));
                da.Add(VRCFuryEditorUtils.Info(
                    "Distance = 0 : Plug is entirely inside socket\n" +
                    "Distance = 1 : Tip of plug is touching socket\n" +
                    "Distance > 1 : Plug is approaching socket\n" +
                    "1 Unit is the length of the plug"
                ));
                
                da.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("depthActions")));

                return da;
            }, enableDepthAnimationsProp));

            var haptics = VRCFuryEditorUtils.Section("Haptics", "OGB haptic support is enabled on this plug by default");
            container.Add(haptics);

            var adv = new Foldout {
                text = "Advanced Plug Options",
                value = false
            };
            container.Add(adv);
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("unitsInMeters"), "(Deprecated) Units are in world-space"));
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("useLegacyRendererFinder"), "(Deprecated) Use legacy renderer search"));
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(configureTps, "(Deprecated) Auto-configure Poiyomi TPS"));
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("addDpsTipLight"), "(Deprecated) Add legacy DPS tip light (must enable in menu)"));
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("spsKeepImports"), "(Developer) Do not flatten SPS imports"));

            return container;
        }
        
        [CustomPropertyDrawer(typeof(VRCFuryHapticPlug.PlugDepthAction))]
        public class PlugDepthActionDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("state")));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("startDistance"), "Distance when animation begins"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("endDistance"), "Distance when animation is maxed"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("enableSelf"), "Allow avatar to trigger its own animation?"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds", tooltip: "It will take approximately this many seconds to smoothly blend to the target depth. Beware that this smoothing is based on framerate, so higher FPS will result in faster smoothing."));
                return c;
            }
        }

        public static VisualElement ConstraintWarning(VFGameObject obj, bool isSocket = false) {
            var output = new VisualElement();
            var warning = VRCFuryEditorUtils.Warn(
                "This SPS component is used within a Constraint! " +
                "AVOID using SPS within constraints if at all possible. " +
                (isSocket
                    ? "Sharing one socket in multiple locations will make your avatar LESS performant, not more! "
                    : "") +
                "\n\n" +
                "Check out https://vrcfury.com/sps/constraints for details");
            warning.style.display = DisplayStyle.None;
            output.Add(warning);
            VRCFuryEditorUtils.RefreshOnInterval(output, () => {
                var found = obj.GetComponentsInSelfAndParents<IConstraint>().Length > 0;
                warning.style.display = found ? DisplayStyle.Flex : DisplayStyle.None;
            });
            return output;
        }
        
        public class GizmoCache {
            public double time = 0;
            public PlugSizeDetector.SizeResult size;
            public string error;
            public Vector3 position;
            public Quaternion rotation;
        }

        private static ConditionalWeakTable<VRCFuryHapticPlug, GizmoCache> gizmoCache
            = new ConditionalWeakTable<VRCFuryHapticPlug, GizmoCache>();
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFuryHapticPlug plug, GizmoType gizmoType) {
            var transform = plug.transform;
            
            var cache = gizmoCache.GetOrCreateValue(plug);
            if (cache.time == 0 || transform.position != cache.position || transform.rotation != cache.rotation || EditorApplication.timeSinceStartup > cache.time + 1) {
                cache.time = EditorApplication.timeSinceStartup;
                cache.error = "";
                cache.position = transform.position;
                cache.rotation = transform.rotation;
                try {
                    cache.size = PlugSizeDetector.GetWorldSize(plug);
                } catch (Exception e) {
                    cache.error = e.Message;
                }
            }

            if (!string.IsNullOrEmpty(cache.error)) {
                VRCFuryGizmoUtils.DrawText(transform.position, cache.error, Color.white, true);
                return;
            }

            var size = cache.size;
            var lossyScale = transform.lossyScale;
            var localLength = size.worldLength / lossyScale.x;
            var localRadius = size.worldRadius / lossyScale.x;
            var localForward = size.localRotation * Vector3.forward;
            var localHalfway = localForward * (localLength / 2);
            var localCapsuleRotation = size.localRotation * Quaternion.Euler(90,0,0);

            var worldPosTip = transform.TransformPoint(size.localPosition + localForward * localLength);

            DrawCapsule(transform, size.localPosition + localHalfway, localCapsuleRotation, size.worldLength, size.worldRadius);
            VRCFuryGizmoUtils.DrawText(worldPosTip, "Tip", Color.white, true);
        }

        public static void DrawCapsule(
            Transform obj,
            Vector3 localPosition,
            Quaternion localRotation,
            float worldLength,
            float worldRadius
        ) {
            var worldPos = obj.TransformPoint(localPosition);
            var worldRot = obj.rotation * localRotation;
            VRCFuryGizmoUtils.DrawCapsule(worldPos, worldRot, worldLength, worldRadius, Color.red);
        }

        public static ICollection<Renderer> GetRenderers(VRCFuryHapticPlug plug) {
            var renderers = new List<Renderer>();
            renderers.AddRange(plug.autoRenderer
                ? PlugRendererFinder.GetAutoRenderer(plug.gameObject, !plug.useLegacyRendererFinder)
                : plug.configureTpsMesh.Where(r => r != null));
            return renderers;

        }

        [CanBeNull]
        public static BakeResult Bake(
            VRCFuryHapticPlug plug,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            MutableManager mutableManager = null,
            bool deferMaterialConfig = false
        ) {
            var transform = plug.transform;
            HapticUtils.RemoveTPSSenders(transform);
            HapticUtils.AssertValidScale(transform, "plug");

            PlugSizeDetector.SizeResult size;
            try {
                size = PlugSizeDetector.GetWorldSize(plug);
            } catch (Exception) {
                return null;
            }

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
                            "Multiple VRCFury Haptic Plugs target the same renderer. This is probably a mistake. " +
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

            var bakeRoot = GameObjects.Create("BakedHapticPlug", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var senders = GameObjects.Create("Senders", bakeRoot);
            HapticUtils.AddSender(senders, Vector3.zero, "Length", worldLength, new [] { HapticUtils.CONTACT_PEN_MAIN });
            HapticUtils.AddSender(senders, Vector3.zero, "WidthHelper", Mathf.Max(0.01f, worldLength - worldRadius*2), new [] { HapticUtils.CONTACT_PEN_WIDTH });
            HapticUtils.AddSender(senders, halfWay, "Envelope", worldRadius, new [] { HapticUtils.CONTACT_PEN_CLOSE }, rotation: capsuleRotation, height: worldLength);
            HapticUtils.AddSender(senders, Vector3.zero, "Root", 0.01f, new [] { HapticUtils.CONTACT_PEN_ROOT });
            
            // TODO: Check if there are 0 renderers,
            // or if there are 0 materials on any of the renderers

            RendererResult[] rendererResults;

            if (mutableManager != null && (plug.configureTps || plug.enableSps)) {
                var checkboxName = plug.enableSps ? "Enable SPS" : "Auto-Configure TPS";
                if (renderers.Count == 0) {
                    throw new Exception(
                        $"VRCFury Haptic Plug has '{checkboxName}' checked, but no renderer was found.");
                }

                rendererResults = renderers.Select(renderer => {
                    var owner = renderer.owner();
                    try {
                        var skin = TpsConfigurer.NormalizeRenderer(renderer, bakeRoot, mutableManager, worldLength);

                        if (plug.enableSps && plug.spsAutorig) {
                            SpsAutoRigger.AutoRig(skin, worldLength, worldRadius, mutableManager);
                        }
                        
                        var spsBlendshapes = plug.spsBlendshapes
                            .Where(b => skin.sharedMesh.HasBlendshape(b))
                            .Distinct()
                            .Take(16)
                            .ToArray();

                        var activeFromMask = PlugMaskGenerator.GetMask(skin, plug);
                        var spsBaked = plug.enableSps ? SpsBaker.Bake(skin, mutableManager.GetTmpDir(), activeFromMask, false, spsBlendshapes) : null;

                        var finishedCopies = new HashSet<Material>();
                        Material ConfigureMaterial(Material mat) {
                            try {
                                if (mat == null) return null;
                                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return mat;

                                if (plug.enableSps) {
                                    var copy = MutableManager.MakeMutable(mat);
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    SpsConfigurer.ConfigureSpsMaterial(skin, copy, worldLength,
                                        spsBaked,
                                        mutableManager, plug, bakeRoot, spsBlendshapes);
                                    return copy;
                                }
                                if (plug.configureTps && TpsConfigurer.IsTps(mat)) {
                                    var copy = MutableManager.MakeMutable(mat);
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    TpsConfigurer.ConfigureTpsMaterial(skin, copy, worldLength,
                                        activeFromMask,
                                        mutableManager);
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
                    configureMaterial = m => m
                }).ToArray();
            }

            if (EditorApplication.isPlaying) {
                foreach (var light in bakeRoot.GetComponentsInSelfAndChildren<Light>()) {
                    light.hideFlags |= HideFlags.HideInHierarchy;
                }
                foreach (var contact in bakeRoot.GetComponentsInSelfAndChildren<ContactBase>()) {
                    contact.hideFlags |= HideFlags.HideInHierarchy;
                }
            }

            if (!deferMaterialConfig) {
                foreach (var r in rendererResults) {
                    r.renderer.sharedMaterials = r.renderer.sharedMaterials.Select(r.configureMaterial).ToArray();
                }
            }

            return new BakeResult {
                bakeRoot = bakeRoot,
                renderers = rendererResults,
                worldLength = worldLength,
                worldRadius = worldRadius,
            };
        }

        public class BakeResult {
            public VFGameObject bakeRoot;
            public ICollection<RendererResult> renderers;
            public float worldLength;
            public float worldRadius;
        }

        public class RendererResult {
            public Renderer renderer;
            public Func<Material, Material> configureMaterial;
            public IList<string> spsBlendshapes;
        }
    }
}
