using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Model;
using VRC.Dynamics;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticPlug), true)]
    public class VRCFuryHapticPlugEditor : VRCFuryComponentEditor<VRCFuryHapticPlug> {
        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticPlug target) {
            var container = new VisualElement();
            var configureTps = serializedObject.FindProperty("configureTps");
            var configureSps = serializedObject.FindProperty("enableSps");
            
            container.Add(new PropertyField(serializedObject.FindProperty("name"), "Name in connected apps"));
            
            var autoMesh = serializedObject.FindProperty("autoRenderer");
            container.Add(VRCFuryEditorUtils.Prop(autoMesh, "Automatically find mesh"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoMesh.boolValue) {
                    c.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("configureTpsMesh")));
                }
                return c;
            }, autoMesh));

            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!configureTps.boolValue && !configureSps.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("autoPosition"),
                        "Detect position/rotation from mesh"));
                }
                return c;
            }, configureTps, configureSps));

            var autoLength = serializedObject.FindProperty("autoLength");
            container.Add(VRCFuryEditorUtils.Prop(autoLength, "Detect length from mesh"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoLength.boolValue) {
                    c.Add(new PropertyField(serializedObject.FindProperty("length"), "Length"));
                }
                return c;
            }, autoLength));

            var autoRadius = serializedObject.FindProperty("autoRadius");
            container.Add(VRCFuryEditorUtils.Prop(autoRadius, "Detect radius from mesh"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (!autoRadius.boolValue) {
                    c.Add(new PropertyField(serializedObject.FindProperty("radius"), "Radius"));
                }
                return c;
            }, autoRadius));

            container.Add(VRCFuryEditorUtils.Prop(configureSps, "Enable SPS (Super Plug Shader) (BETA)"));

            var adv = new Foldout {
                text = "Advanced",
                value = false
            };
            container.Add(adv);
            adv.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("unitsInMeters"), "Size unaffected by scale (Legacy Mode)"));
            
            adv.Add(VRCFuryEditorUtils.Prop(configureTps, "Auto-configure Poiyomi TPS (Deprecated)"));
            adv.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (configureTps.boolValue) {
                    c.Add(VRCFuryEditorUtils.Info(
                        "Auto-configure TPS will cause this VRCFury Haptic Plug to automatically configure Poiyomi TPS on the associated renderer." +
                        " When using this option:\n" +
                        "1. The mesh should be posed in the editor in the 'erect' state. It can still use physbones or be animated to different poses, but will return to this resting editor pose when penetrating.\n" +
                        "2. This component should be placed on an EMPTY OBJECT (not on the renderer).\n" +
                        "3. Move the empty object to where you want deformation to start.\n" +
                        "4. Enable gizmos and rotate the empty object so the tip is aligned properly, and the red gizmo capsule fits the proper shape.\n" +
                        "5. If this mesh is rigged (with bones), the empty object should be a child of the nearest bone on the rig (don't forget to exclude it from physbones if needed).\n" +
                        "6. The material must be using Poiyomi Pro 8.1+ with the 'penetrator' checkbox enabled.\n" +
                        "7. DO NOT run the TPS setup wizard, this checkbox will do everything needed."));
                    c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("configureTpsMask"), "Optional mask for TPS"));
                }
                return c;
            }, configureTps));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var (renderers, worldLength, worldRadius, localRotation, localPosition) = GetWorldSize(target);
                var text = new List<string>();
                text.Add("Attached renderers: " + string.Join(", ", renderers.Select(r => r.gameObject.name)));
                text.Add($"Detected Length: {worldLength}m");
                text.Add($"Detected Radius: {worldRadius}m");
                return string.Join("\n", text);
            }));

            return container;
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFuryHapticPlug scr, GizmoType gizmoType) {
            (ICollection<Renderer>, float, float, Quaternion, Vector3) size;
            try {
                size = GetWorldSize(scr);
            } catch (Exception e) {
                VRCFuryGizmoUtils.DrawText(scr.transform.position, e.Message, Color.white);
                return;
            }
            
            var (renderers, worldLength, worldRadius, localRotation, localPosition) = size;
            var localLength = worldLength / scr.transform.lossyScale.x;
            var localRadius = worldRadius / scr.transform.lossyScale.x;
            var localForward = localRotation * Vector3.forward;
            var localHalfway = localForward * (localLength / 2);
            var localCapsuleRotation = localRotation * Quaternion.Euler(90,0,0);

            var worldPosTip = scr.transform.TransformPoint(localPosition + localForward * localLength);

            DrawCapsule(scr.gameObject, localPosition + localHalfway, localCapsuleRotation, worldLength, worldRadius);
            VRCFuryGizmoUtils.DrawText(worldPosTip, "Tip", Color.white);
        }

        public static void DrawCapsule(
            GameObject obj,
            Vector3 localPosition,
            Quaternion localRotation,
            float worldLength,
            float worldRadius
        ) {
            var worldPos = obj.transform.TransformPoint(localPosition);
            var worldRot = obj.transform.rotation * localRotation;
            VRCFuryGizmoUtils.DrawCapsule(worldPos, worldRot, worldLength, worldRadius, Color.red);
        }

        public static ICollection<Renderer> GetRenderers(VRCFuryHapticPlug pen) {
            var renderers = new List<Renderer>();
            if (pen.autoRenderer) {
                var autoParams = new PlugRendererFinder.Params();
                if (pen.enableSps) {
                    autoParams.PreferDpsOrTps = false;
                    autoParams.SearchChildren = false;
                    autoParams.PreferWeightedToBone = true;
                    autoParams.EmptyIfMultiple = true;
                }
                renderers.AddRange(PlugRendererFinder.GetAutoRenderer(pen.gameObject, autoParams));
            } else {
                renderers.AddRange(pen.configureTpsMesh.Where(r => r != null));
            }
            return renderers;
        }

        public static (ICollection<Renderer>, float, float, Quaternion, Vector3) GetWorldSize(VRCFuryHapticPlug pen) {

            var renderers = GetRenderers(pen);

            Quaternion worldRotation = pen.transform.rotation;
            Vector3 worldPosition = pen.transform.position;
            if (!pen.configureTps && !pen.enableSps && pen.autoPosition && renderers.Count > 0) {
                var firstRenderer = renderers.First();
                worldRotation = PlugSizeDetector.GetAutoWorldRotation(firstRenderer);
                worldPosition = PlugSizeDetector.GetAutoWorldPosition(firstRenderer);
            }
            var testBase = pen.transform.Find("OGBTestBase");
            if (testBase != null) {
                worldPosition = testBase.position;
                worldRotation = testBase.rotation;
            }

            float worldLength = 0;
            float worldRadius = 0;
            if (pen.autoRadius || pen.autoLength) {
                if (renderers.Count == 0) {
                    throw new VRCFBuilderException("Failed to find plug renderer");
                }
                foreach (var renderer in renderers) {
                    var autoSize = PlugSizeDetector.GetAutoWorldSize(renderer, worldPosition, worldRotation);
                    if (autoSize == null) continue;
                    if (pen.autoLength) worldLength = autoSize.Item1;
                    if (pen.autoRadius) worldRadius = autoSize.Item2;
                    break;
                }
            }

            if (!pen.autoLength) {
                worldLength = pen.length;
                if (!pen.unitsInMeters) worldLength *= pen.transform.lossyScale.x;
            }
            if (!pen.autoRadius) {
                worldRadius = pen.radius;
                if (!pen.unitsInMeters) worldRadius *= pen.transform.lossyScale.x;
            }

            if (worldLength <= 0) throw new VRCFBuilderException("Failed to detect plug length");
            if (worldRadius <= 0) throw new VRCFBuilderException("Failed to detect plug radius");
            if (worldRadius > worldLength / 2) worldRadius = worldLength / 2;
            var localRotation = Quaternion.Inverse(pen.transform.rotation) * worldRotation;
            var localPosition = pen.transform.InverseTransformPoint(worldPosition);
            return (renderers, worldLength, worldRadius, localRotation, localPosition);
        }

        public static Tuple<string, GameObject, ICollection<Renderer>, float, float> Bake(VRCFuryHapticPlug pen, List<string> usedNames = null, bool onlySenders = false, MutableManager mutableManager = null) {
            var obj = pen.gameObject;
            HapticUtils.RemoveTPSSenders(obj);

            HapticUtils.AssertValidScale(obj, "plug");

            (ICollection<Renderer>, float, float, Quaternion, Vector3) size;
            try {
                size = GetWorldSize(pen);
            } catch (Exception) {
                return null;
            }

            var (renderers, worldLength, worldRadius, localRotation, localPosition) = size;

            var name = pen.name;
            if (string.IsNullOrWhiteSpace(name)) {
                name = obj.name;
            }
            if (usedNames != null) name = HapticUtils.GetNextName(usedNames, name);
            
            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var capsuleRotation = Quaternion.Euler(90,0,0);

            var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
            
            // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
            var extraRadiusForRub = 0.08f;
            
            Debug.Log("Baking haptic component in " + obj + " as " + name);
            
            var bakeRoot = new GameObject("BakedHapticPlug");
            bakeRoot.transform.SetParent(pen.transform, false);
            bakeRoot.transform.localPosition = localPosition;
            bakeRoot.transform.localRotation = localRotation;

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var senders = new GameObject("Senders");
            senders.transform.SetParent(bakeRoot.transform, false);
            HapticUtils.AddSender(senders, Vector3.zero, "Length", worldLength, HapticUtils.CONTACT_PEN_MAIN);
            HapticUtils.AddSender(senders, Vector3.zero, "WidthHelper", Mathf.Max(0.01f, worldLength - worldRadius*2), HapticUtils.CONTACT_PEN_WIDTH);
            HapticUtils.AddSender(senders, halfWay, "Envelope", worldRadius, HapticUtils.CONTACT_PEN_CLOSE, rotation: capsuleRotation, height: worldLength);
            HapticUtils.AddSender(senders, Vector3.zero, "Root", 0.01f, HapticUtils.CONTACT_PEN_ROOT);
            
            var paramPrefix = "OGB/Pen/" + name.Replace('/','_');

            if (onlySenders) {
                var info = new GameObject("Info");
                info.transform.SetParent(bakeRoot.transform, false);
                if (!string.IsNullOrWhiteSpace(pen.name)) {
                    var nameObj = new GameObject("name=" + pen.name);
                    nameObj.transform.SetParent(info.transform, false);
                }
                if (pen.length != 0 || pen.radius != 0) {
                    var sizeObj = new GameObject("size");
                    sizeObj.transform.SetParent(info.transform, false);
                    sizeObj.transform.localScale = new Vector3(pen.length, pen.radius, 0);
                }
            } else {
                // Receivers
                var receivers = new GameObject("Receivers");
                receivers.transform.SetParent(bakeRoot.transform, false);
                HapticUtils.AddReceiver(receivers, halfWay, paramPrefix + "/TouchSelfClose", "TouchSelfClose", worldRadius+extraRadiusForTouch, HapticUtils.SelfContacts, allowOthers:false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", worldLength+extraRadiusForTouch, HapticUtils.SelfContacts, allowOthers:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, halfWay, paramPrefix + "/TouchOthersClose", "TouchOthersClose", worldRadius+extraRadiusForTouch, HapticUtils.BodyContacts, allowSelf:false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", worldLength+extraRadiusForTouch, HapticUtils.BodyContacts, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", worldLength, new []{HapticUtils.CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", worldLength, new []{HapticUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", worldLength, new []{HapticUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, halfWay, paramPrefix + "/FrotOthersClose", "FrotOthersClose", worldRadius+extraRadiusForRub, new []{HapticUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: capsuleRotation, height: worldLength, type: ContactReceiver.ReceiverType.Constant);
            }
            
            if ((pen.configureTps || pen.enableSps) && mutableManager != null) {
                if (pen.configureTps && pen.enableSps) {
                    throw new VRCFBuilderException(
                        "Auto-Configure TPS and Auto-Configure SPS cannot be enabled at the same time.");
                }
                if (renderers.Count == 0) {
                    throw new VRCFBuilderException(
                        "VRCFury Haptic Plug has 'auto-configure SPS' or 'auto-configure TPS' enabled, but no renderer was found.");
                }

                foreach (var renderer in renderers) {
                    TpsConfigurer.ConfigureRenderer(renderer, bakeRoot.transform, worldLength, pen.configureTpsMask, mutableManager, pen.enableSps);
                }
            }
            
            HapticUtils.AddVersionContacts(bakeRoot, paramPrefix, onlySenders, true);

            return Tuple.Create(name, bakeRoot, renderers, worldLength, worldRadius);
        }
    }
}
