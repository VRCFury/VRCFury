using System;
using System.Collections.Generic;
using System.Linq;
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
            var size = GetSize(scr);
            if (size == null) {
                VRCFuryGizmoUtils.DrawText(scr.transform.position, "Invalid Penetrator Size", Color.white);
                return;
            }
            
            var worldLength = size.Item1;
            var worldRadius = size.Item2;
            var forward = size.Item3;
            var tightPos = forward * (worldLength / 2);
            var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);

            var worldPosTip = scr.transform.TransformPoint(forward * worldLength / scr.transform.lossyScale.x);

            DrawCapsule(scr.gameObject, tightPos, tightRot, worldLength, worldRadius);
            VRCFuryGizmoUtils.DrawText(worldPosTip, "Tip", Color.white);
        }

        public static void DrawCapsule(
            GameObject obj,
            Vector3 localPositionInWorldScale,
            Quaternion localRotation,
            float worldLength,
            float worldRadius
        ) {
            var worldPos = obj.transform.TransformPoint(localPositionInWorldScale / obj.transform.lossyScale.x);
            var worldRot = obj.transform.rotation * localRotation;
            VRCFuryGizmoUtils.DrawCapsule(worldPos, worldRot, worldLength, worldRadius, Color.red);
        }

        public static Tuple<float, float, Vector3> GetSize(OGBPenetrator pen) {
            var length = pen.length;
            var radius = pen.radius;
            Vector3 forward;
            if (pen.configureTps) {
                forward = Vector3.forward;
            } else {
                forward = OGBPenetratorSizeDetector.GetAutoForward(pen.gameObject) ?? Vector3.forward;
            }
            if (!pen.unitsInMeters) {
                length *= pen.transform.lossyScale.x;
                radius *= pen.transform.lossyScale.x;
            }

            var autoSize = OGBPenetratorSizeDetector.GetAutoSize(pen.gameObject, false, forward);
            if (autoSize != null) {
                if (length == 0) length = autoSize.Item1;
                if (radius == 0) radius = autoSize.Item2;
            }

            if (length <= 0 || radius <= 0) return null;
            if (radius > length / 2) radius = length / 2;
            return Tuple.Create(length, radius, forward);
        }

        public static void Bake(OGBPenetrator pen, List<string> usedNames = null, bool onlySenders = false) {
            if (usedNames == null) usedNames = new List<string>();
            var obj = pen.gameObject;
            OGBUtils.RemoveTPSSenders(obj);

            OGBUtils.AssertValidScale(obj, "penetrator");

            var size = GetSize(pen);
            if (size == null) return;
            var length = size.Item1;
            var radius = size.Item2;
            var forward = size.Item3;

            var name = pen.name;
            if (string.IsNullOrWhiteSpace(name)) {
                name = obj.name;
            }

            var tightPos = forward * (length / 2);
            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var tightRot = Quaternion.LookRotation(forward) * Quaternion.Euler(90,0,0);

            var extraRadiusForTouch = Math.Min(radius, 0.08f /* 8cm */);
            
            // Extra frot radius should always match for everyone, so when two penetrators collide, both parties experience at the same time
            var extraRadiusForFrot = 0.08f;
            
            Debug.Log("Baking OGB " + obj + " as " + name);

            // Senders
            OGBUtils.AddSender(obj, Vector3.zero, "Length", length, OGBUtils.CONTACT_PEN_MAIN);
            OGBUtils.AddSender(obj, Vector3.zero, "WidthHelper", Mathf.Max(0.01f/obj.transform.lossyScale.x, length - radius*2), OGBUtils.CONTACT_PEN_WIDTH);
            OGBUtils.AddSender(obj, tightPos, "Envelope", radius, OGBUtils.CONTACT_PEN_CLOSE, rotation: tightRot, height: length);
            OGBUtils.AddSender(obj, Vector3.zero, "Root", 0.01f, OGBUtils.CONTACT_PEN_ROOT);
            
            var paramPrefix = OGBUtils.GetNextName(usedNames, "OGB/Pen/" + name.Replace('/','_'));

            if (onlySenders) {
                var bake = new GameObject("OGB_Baked_Pen");
                bake.transform.SetParent(obj.transform, false);
                if (!string.IsNullOrWhiteSpace(pen.name)) {
                    var nameObj = new GameObject("name=" + pen.name);
                    nameObj.transform.SetParent(bake.transform, false);
                }
                if (pen.length != 0 || pen.radius != 0) {
                    var sizeObj = new GameObject("size");
                    sizeObj.transform.SetParent(bake.transform, false);
                    sizeObj.transform.localScale = new Vector3(pen.length, pen.radius, 0);
                }
            } else {
                // Receivers
                OGBUtils.AddReceiver(obj, tightPos, paramPrefix + "/TouchSelfClose", "TouchSelfClose", radius+extraRadiusForTouch, OGBUtils.SelfContacts, allowOthers:false, localOnly:true, rotation: tightRot, height: length+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", length+extraRadiusForTouch, OGBUtils.SelfContacts, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, tightPos, paramPrefix + "/TouchOthersClose", "TouchOthersClose", radius+extraRadiusForTouch, OGBUtils.BodyContacts, allowSelf:false, localOnly:true, rotation: tightRot, height: length+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", length+extraRadiusForTouch, OGBUtils.BodyContacts, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", length, new []{OGBUtils.CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", length, new []{OGBUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", length, new []{OGBUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, tightPos, paramPrefix + "/FrotOthersClose", "FrotOthersClose", radius+extraRadiusForFrot, new []{OGBUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: tightRot, height: length, type: ContactReceiver.ReceiverType.Constant);
            }
            
            OGBUtils.AddVersionContacts(obj, paramPrefix, onlySenders, true);
        }
    }
}
