using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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
            
            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("name"), "Name in connected apps"));
            
            var autoMesh = serializedObject.FindProperty("autoRenderer");
            container.Add(VRCFuryEditorUtils.BetterCheckbox(autoMesh, "Automatically find mesh"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoMesh.boolValue) {
                    c.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("configureTpsMesh")));
                }
                return c;
            }, autoMesh));

            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!configureTps.boolValue && !enableSps.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("autoPosition"),
                        "Detect position/rotation from mesh"));
                }
                return c;
            }, configureTps, enableSps));

            var autoLength = serializedObject.FindProperty("autoLength");
            container.Add(VRCFuryEditorUtils.BetterCheckbox(autoLength, "Detect length from mesh"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoLength.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("length"), "Length"));
                }
                return c;
            }, autoLength));

            var autoRadius = serializedObject.FindProperty("autoRadius");
            container.Add(VRCFuryEditorUtils.BetterCheckbox(autoRadius, "Detect radius from mesh"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoRadius.boolValue) {
                    c.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));
                }
                return c;
            }, autoRadius));
            
            container.Add(VRCFuryEditorUtils.BetterCheckbox(
                serializedObject.FindProperty("useBoneMask"),
                "Automatically mask using bone weights"
            ));

            container.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("textureMask"),
                "Optional additional texture mask (white = 'do not deform or use in length calculations')"
            ));
            
            container.Add(VRCFuryEditorUtils.WrappedLabel("Animations when socket is present"));
            container.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("depthActions"), (i, prop) => {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.Info(
                    "If you provide a non-static (moving) animation clip, the clip will run from start " +
                    "to end depending on penetration depth. Otherwise, it will animate from 'off' to 'on' depending on depth."));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("state"), "Penetrated state"));
                c.Add(VRCFuryEditorUtils.Info(
                    "Distance = 0 : Plug is entirely inside socket\n" +
                    "Distance = 1 : Tip of plug is touching socket\n" +
                    "Distance > 1 : Plug is approaching socket\n" +
                    "1 Unit is the length of the plug"
                ));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("startDistance"), "Distance when animation begins"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("endDistance"), "Distance when animation is maxed"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("enableSelf"), "Allow avatar to trigger its own animation?"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds", tooltip: "It will take approximately this many seconds to smoothly blend to the target depth. Beware that this smoothing is based on framerate, so higher FPS will result in faster smoothing."));
                return c;
            }));

            container.Add(VRCFuryEditorUtils.BetterCheckbox(enableSps, "Enable SPS (Super Plug Shader) (BETA)"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableSps.boolValue) {
                    var spsBox = new VisualElement() {
                        style = {
                            backgroundColor = new Color(0,0,0,0.1f),
                            marginTop = 5,
                            marginBottom = 10
                        }
                    };
                    VRCFuryEditorUtils.Padding(spsBox, 5);
                    VRCFuryEditorUtils.BorderRadius(spsBox, 5);
                    c.Add(spsBox);
                    spsBox.Add(VRCFuryEditorUtils.WrappedLabel("SPS (Super Plug Shader)", style => {
                        style.unityFontStyleAndWeight = FontStyle.Bold;
                        style.unityTextAlign = TextAnchor.MiddleCenter;
                    }));
                    spsBox.Add(VRCFuryEditorUtils.WrappedLabel("Check out vrcfury.com/sps for details", style => {
                        style.unityTextAlign = TextAnchor.MiddleCenter;
                        style.paddingBottom = 5;
                    }));
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
                }
                return c;
            }, enableSps));

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
            //adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("channel"), "Channel"));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var size = PlugSizeDetector.GetWorldSize(target);
                var text = new List<string>();
                text.Add("Attached renderers: " + string.Join(", ", size.renderers.Select(r => r.owner().name)));
                text.Add($"Detected Length: {size.worldLength}m");
                text.Add($"Detected Radius: {size.worldRadius}m");
                return string.Join("\n", text);
            }));

            return container;
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
            var localLength = size.worldLength / transform.lossyScale.x;
            var localRadius = size.worldRadius / transform.lossyScale.x;
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
            if (plug.autoRenderer) {
                renderers.AddRange(PlugRendererFinder.GetAutoRenderer(plug.gameObject, !plug.useLegacyRendererFinder));
            } else {
                renderers.AddRange(plug.configureTpsMesh.Where(r => r != null));
            }
            return renderers;
        }

        [CanBeNull]
        public static BakeResult Bake(
            VRCFuryHapticPlug plug,
            List<string> usedNames = null,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            bool onlySenders = false,
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

            var name = plug.name;
            if (string.IsNullOrWhiteSpace(name)) {
                if (renderers.Count > 0) {
                    name = HapticUtils.GetName(renderers.First().gameObject);
                } else {
                    name = HapticUtils.GetName(plug.owner());
                }
            }
            if (usedNames != null) name = HapticUtils.GetNextName(usedNames, name);
            
            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var capsuleRotation = Quaternion.Euler(90,0,0);

            var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
            
            // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
            var extraRadiusForRub = 0.08f;
            
            Debug.Log("Baking haptic component in " + transform + " as " + name);
            
            var bakeRoot = GameObjects.Create("BakedHapticPlug", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var senders = GameObjects.Create("Senders", bakeRoot);
            HapticUtils.AddSender(senders, Vector3.zero, "Length", worldLength, HapticUtils.CONTACT_PEN_MAIN);
            HapticUtils.AddSender(senders, Vector3.zero, "WidthHelper", Mathf.Max(0.01f, worldLength - worldRadius*2), HapticUtils.CONTACT_PEN_WIDTH);
            HapticUtils.AddSender(senders, halfWay, "Envelope", worldRadius, HapticUtils.CONTACT_PEN_CLOSE, rotation: capsuleRotation, height: worldLength);
            HapticUtils.AddSender(senders, Vector3.zero, "Root", 0.01f, HapticUtils.CONTACT_PEN_ROOT);
            
            var paramPrefix = "OGB/Pen/" + name.Replace('/','_');

            if (onlySenders) {
                var info = GameObjects.Create("Info", bakeRoot);
                if (!string.IsNullOrWhiteSpace(plug.name)) {
                    var nameObj = GameObjects.Create("name=" + plug.name, info);
                }
                if (plug.length != 0 || plug.radius != 0) {
                    var sizeObj = GameObjects.Create("size", info);
                    sizeObj.localScale = new Vector3(plug.length, plug.radius, 0);
                }
            } else {
                // Receivers
                var receivers = GameObjects.Create("Receivers", bakeRoot);
                HapticUtils.AddReceiver(receivers, halfWay, paramPrefix + "/TouchSelfClose", "TouchSelfClose", worldRadius+extraRadiusForTouch, HapticUtils.SelfContacts, allowOthers:false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", worldLength+extraRadiusForTouch, HapticUtils.SelfContacts, allowOthers:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, halfWay, paramPrefix + "/TouchOthersClose", "TouchOthersClose", worldRadius+extraRadiusForTouch, HapticUtils.BodyContacts, allowSelf:false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", worldLength+extraRadiusForTouch, HapticUtils.BodyContacts, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", worldLength, new []{HapticUtils.CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", worldLength, new []{HapticUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", worldLength, new []{HapticUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, halfWay, paramPrefix + "/FrotOthersClose", "FrotOthersClose", worldRadius+extraRadiusForRub, new []{HapticUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: capsuleRotation, height: worldLength, type: ContactReceiver.ReceiverType.Constant);
            }
            
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
                            SpsAutoRigger.AutoRig(skin, worldLength, mutableManager);
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

                                if (plug.enableSps) {
                                    var copy = mutableManager.MakeMutable(mat, skin.owner());
                                    if (finishedCopies.Contains(copy)) return copy;
                                    finishedCopies.Add(copy);
                                    SpsConfigurer.ConfigureSpsMaterial(skin, copy, worldLength,
                                        spsBaked,
                                        mutableManager, plug, bakeRoot, spsBlendshapes);
                                    return copy;
                                }
                                if (plug.configureTps && TpsConfigurer.IsTps(mat)) {
                                    var copy = mutableManager.MakeMutable(mat, skin.owner());
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

            HapticUtils.AddVersionContacts(bakeRoot, paramPrefix, onlySenders, true);
            
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
                name = name,
                bakeRoot = bakeRoot,
                renderers = rendererResults,
                worldLength = worldLength,
                worldRadius = worldRadius,
            };
        }

        public class BakeResult {
            public string name;
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
