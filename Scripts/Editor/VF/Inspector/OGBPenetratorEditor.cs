using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VRC.Dynamics;

namespace VF.Inspector {
    [CustomEditor(typeof(OGBPenetrator), true)]
    public class OGBPenetratorEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            var self = (OGBPenetrator)target;

            var container = new VisualElement();
            
            container.Add(new PropertyField(serializedObject.FindProperty("name"), "Name Override"));
            container.Add(new PropertyField(serializedObject.FindProperty("length"), "Length Override"));
            container.Add(new PropertyField(serializedObject.FindProperty("radius"), "Radius Override"));
            
            var adv = new Foldout {
                text = "Advanced",
                value = false
            };
            container.Add(adv);
            adv.Add(new PropertyField(serializedObject.FindProperty("unitsInMeters"), "Size unaffected by scale (Legacy Mode)"));
            adv.Add(new PropertyField(serializedObject.FindProperty("configureTps"), "Auto-configure TPS (extremely experimental)"));

            return container;
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(OGBPenetrator scr, GizmoType gizmoType) {
            var size = GetWorldSize(scr);
            if (size == null) {
                VRCFuryGizmoUtils.DrawText(scr.transform.position, "Invalid Penetrator Size", Color.white);
                return;
            }
            
            var (worldLength, worldRadius, localRotation, localPosition) = size;
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

        public static Tuple<float, float, Quaternion, Vector3> GetWorldSize(OGBPenetrator pen) {

            Quaternion worldRotation = pen.transform.rotation;
            Vector3 worldPosition = pen.transform.position;
            if (!pen.configureTps) {
                worldRotation = OGBPenetratorSizeDetector.GetAutoWorldRotation(pen.gameObject) ?? worldRotation;
                worldPosition = OGBPenetratorSizeDetector.GetAutoWorldPosition(pen.gameObject) ?? worldPosition;
            }
            var testBase = pen.transform.Find("OGBTestBase");
            if (testBase != null) {
                worldPosition = testBase.position;
                worldRotation = testBase.rotation;
            }
            
            var worldLength = pen.length;
            var worldRadius = pen.radius;
            if (!pen.unitsInMeters) {
                worldLength *= pen.transform.lossyScale.x;
                worldRadius *= pen.transform.lossyScale.x;
            }
            if (worldLength <= 0 || worldRadius <= 0) {
                var autoSize = OGBPenetratorSizeDetector.GetAutoWorldSize(pen.gameObject, false, worldPosition, worldRotation);
                if (autoSize != null) {
                    if (worldLength <= 0) worldLength = autoSize.Item1;
                    if (worldRadius <= 0) worldRadius = autoSize.Item2;
                }
            }

            if (worldLength <= 0 || worldRadius <= 0) return null;
            if (worldRadius > worldLength / 2) worldRadius = worldLength / 2;
            var localRotation = Quaternion.Inverse(pen.transform.rotation) * worldRotation;
            var localPosition = pen.transform.InverseTransformPoint(worldPosition);
            return Tuple.Create(worldLength, worldRadius, localRotation, localPosition);
        }

        public static Tuple<string, GameObject, float, float> Bake(OGBPenetrator pen, List<string> usedNames = null, bool onlySenders = false) {
            var obj = pen.gameObject;
            OGBUtils.RemoveTPSSenders(obj);

            OGBUtils.AssertValidScale(obj, "penetrator");

            var size = GetWorldSize(pen);
            if (size == null) return null;
            var (worldLength, worldRadius, localRotation, localPosition) = size;

            var name = pen.name;
            if (string.IsNullOrWhiteSpace(name)) {
                name = obj.name;
            }
            if (usedNames != null) name = OGBUtils.GetNextName(usedNames, name);
            
            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var capsuleRotation = Quaternion.Euler(90,0,0);

            var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
            
            // Extra frot radius should always match for everyone, so when two penetrators collide, both parties experience at the same time
            var extraRadiusForFrot = 0.08f;
            
            Debug.Log("Baking OGB " + obj + " as " + name);
            
            var bakeRoot = new GameObject("BakedOGBPenetrator");
            bakeRoot.transform.SetParent(pen.transform, false);
            bakeRoot.transform.localPosition = localPosition;
            bakeRoot.transform.localRotation = localRotation;

            // Senders
            var halfWay = Vector3.forward * (worldLength / 2);
            var senders = new GameObject("Senders");
            senders.transform.SetParent(bakeRoot.transform, false);
            OGBUtils.AddSender(senders, Vector3.zero, "Length", worldLength, OGBUtils.CONTACT_PEN_MAIN);
            OGBUtils.AddSender(senders, Vector3.zero, "WidthHelper", Mathf.Max(0.01f, worldLength - worldRadius*2), OGBUtils.CONTACT_PEN_WIDTH);
            OGBUtils.AddSender(senders, halfWay, "Envelope", worldRadius, OGBUtils.CONTACT_PEN_CLOSE, rotation: capsuleRotation, height: worldLength);
            OGBUtils.AddSender(senders, Vector3.zero, "Root", 0.01f, OGBUtils.CONTACT_PEN_ROOT);
            
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
                OGBUtils.AddReceiver(receivers, halfWay, paramPrefix + "/TouchSelfClose", "TouchSelfClose", worldRadius+extraRadiusForTouch, OGBUtils.SelfContacts, allowOthers:false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", worldLength+extraRadiusForTouch, OGBUtils.SelfContacts, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(receivers, halfWay, paramPrefix + "/TouchOthersClose", "TouchOthersClose", worldRadius+extraRadiusForTouch, OGBUtils.BodyContacts, allowSelf:false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", worldLength+extraRadiusForTouch, OGBUtils.BodyContacts, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", worldLength, new []{OGBUtils.CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", worldLength, new []{OGBUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", worldLength, new []{OGBUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(receivers, halfWay, paramPrefix + "/FrotOthersClose", "FrotOthersClose", worldRadius+extraRadiusForFrot, new []{OGBUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: capsuleRotation, height: worldLength, type: ContactReceiver.ReceiverType.Constant);
            }
            
            OGBUtils.AddVersionContacts(bakeRoot, paramPrefix, onlySenders, true);

            return Tuple.Create(name, bakeRoot, worldLength, worldRadius);
        }
    }
}
