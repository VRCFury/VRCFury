using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
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

                    var shaderAdv = new Foldout {
                        text = "Advanced SPS Options",
                        value = false,
                        style = { paddingLeft = 13 }
                    };
                    shaderAdv.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("spsAnimatedEnabled"),
                        "Deformation Enabled",
                        tooltip: "This field can range from 0 (SPS fully disabled) to 1 (SPS fully enabled)." +
                                 " You can ANIMATE this field to disable deformation during animations when plug should not be attracted toward sockets." +
                                 " You can smoothly animate from 0 to 1 to smoothly activate deformation, just in case plug is near a socket while activated."
                    ));
                    shaderAdv.Add(VRCFuryEditorUtils.BetterProp(
                        serializedObject.FindProperty("spsOverrun"),
                        "Allow Hole Overrun",
                        tooltip: "This allows the plug to extend very slightly past holes to improve collapse visuals." +
                                 " Beware that disabling this may cause plug to appear to 'fold in' near holes like a map, which may be strange."
                    ));
                    spsBox.Add(shaderAdv);
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
                var (renderers, worldLength, worldRadius, localRotation, localPosition) = PlugSizeDetector.GetWorldSize(target);
                var text = new List<string>();
                text.Add("Attached renderers: " + string.Join(", ", renderers.Select(r => r.owner().name)));
                text.Add($"Detected Length: {worldLength}m");
                text.Add($"Detected Radius: {worldRadius}m");
                return string.Join("\n", text);
            }));

            return container;
        }
        
        public class GizmoCache {
            public double time = 0;
            public (ICollection<Renderer>, float, float, Quaternion, Vector3) size;
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

            var (renderers, worldLength, worldRadius, localRotation, localPosition) = cache.size;
            var localLength = worldLength / transform.lossyScale.x;
            var localRadius = worldRadius / transform.lossyScale.x;
            var localForward = localRotation * Vector3.forward;
            var localHalfway = localForward * (localLength / 2);
            var localCapsuleRotation = localRotation * Quaternion.Euler(90,0,0);

            var worldPosTip = transform.TransformPoint(localPosition + localForward * localLength);

            DrawCapsule(transform, localPosition + localHalfway, localCapsuleRotation, worldLength, worldRadius);
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

        public static Tuple<string, VFGameObject, ICollection<Renderer>, float, float> Bake(
            VRCFuryHapticPlug plug,
            List<string> usedNames = null,
            Dictionary<VFGameObject, VRCFuryHapticPlug> usedRenderers = null,
            bool onlySenders = false,
            MutableManager mutableManager = null
        ) {
            var transform = plug.transform;
            HapticUtils.RemoveTPSSenders(transform);
            HapticUtils.AssertValidScale(transform, "plug");

            (ICollection<Renderer>, float, float, Quaternion, Vector3) size;
            try {
                size = PlugSizeDetector.GetWorldSize(plug);
            } catch (Exception) {
                return null;
            }

            var (renderers, worldLength, worldRadius, localRotation, localPosition) = size;

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
                name = plug.owner().name;
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
            
            if ((plug.configureTps || plug.enableSps) && mutableManager != null) {
                var checkboxName = plug.enableSps ? "Enable SPS" : "Auto-Configure TPS";
                if (renderers.Count == 0) {
                    throw new VRCFBuilderException(
                        $"VRCFury Haptic Plug has '{checkboxName}' checked, but no renderer was found.");
                }

                renderers = renderers.Select(renderer => {
                    var skin = TpsConfigurer.NormalizeRenderer(renderer, bakeRoot, mutableManager, worldLength);

                    if (plug.enableSps && plug.spsAutorig) {
                        SpsAutoRigger.AutoRig(skin, worldLength, mutableManager);
                    }

                    var activeFromMask = PlugMaskGenerator.GetMask(skin, plug);

                    var configuredOne = false;
                    skin.sharedMaterials = skin.sharedMaterials
                        .Select(mat => {
                            if (mat == null) return null;
                            if (plug.enableSps) {
                                configuredOne = true;
                                return SpsConfigurer.ConfigureSpsMaterial(skin, mat, worldLength, activeFromMask, mutableManager, plug);
                            } else if (TpsConfigurer.IsTps(mat)) {
                                configuredOne = true;
                                return TpsConfigurer.ConfigureTpsMaterial(skin, mat, worldLength, activeFromMask, mutableManager);
                            }

                            return mat;
                        })
                        .ToArray();

                    if (!configuredOne) {
                        throw new VRCFBuilderException(
                            $"VRCFury Haptic Plug has '{checkboxName}' checked, but there no valid material was on the linked renderer.");
                    }

                    VRCFuryEditorUtils.MarkDirty(skin);
                    return skin;
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

            return Tuple.Create(name, bakeRoot, renderers, worldLength, worldRadius);
        }
    }
}
