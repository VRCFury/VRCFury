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
        
        public static Tuple<Vector3> MaterialIsDps(Material mat) {
            if (mat == null) return null;
            if (!mat.shader) return null;
            if (mat.shader.name == "Raliv/Penetrator") return Tuple.Create(Vector3.forward); // Raliv
            if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return Tuple.Create(Vector3.forward); // XSToon w/ Raliv
            if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return Tuple.Create(Vector3.forward); // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return Tuple.Create(Vector3.forward); // UnityChanToonShader w/ Raliv
            if (mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0) {
                // Poiyomi 8 w/ TPS
                var forward = Vector3.forward;
                if (mat.HasProperty("_TPS_PenetratorForward")) {
                    var c = mat.GetVector("_TPS_PenetratorForward");
                    forward = new Vector3(c.x, c.y, c.z).normalized;
                }
                return Tuple.Create(forward);
            }
            return null;
        }

        public static Tuple<float, float, Vector3> GetAutoSize(GameObject obj, bool directOnly = false) {
            foreach (var skin in obj.GetComponents<SkinnedMeshRenderer>()) {
                var auto = GetAutoSize(skin, true);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponents<MeshRenderer>()) {
                var auto = GetAutoSize(renderer, true);
                if (auto != null) return auto;
            }
            if (directOnly) return null;
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var auto = GetAutoSize(skin, true);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true)) {
                var auto = GetAutoSize(renderer, true);
                if (auto != null) return auto;
            }
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var auto = GetAutoSize(skin, false);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true)) {
                var auto = GetAutoSize(renderer, false);
                if (auto != null) return auto;
            }
            return null;
        }
        
        private static Tuple<float, float, Vector3> GetAutoSize(MeshRenderer renderer, bool useMaterials) {
            var forward = Vector3.forward;
            if (useMaterials) {
                var m = renderer.sharedMaterials.Select(MaterialIsDps).FirstOrDefault(c => c != null);
                if (m == null) return null;
                forward = m.Item1;
            }
            
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (!meshFilter || !meshFilter.sharedMesh) return null;
            return GetAutoSize(renderer.gameObject, meshFilter.sharedMesh, forward);
        }
        
        private static Tuple<float, float, Vector3> GetAutoSize(SkinnedMeshRenderer skin, bool useMaterials) {
            var forward = Vector3.forward;
            if (useMaterials) {
                var m = skin.sharedMaterials.Select(MaterialIsDps).FirstOrDefault(c => c != null);
                if (m == null) return null;
                forward = m.Item1;
            }
            
            // If the skinned mesh doesn't have any bones attached, it's treated like a regular mesh and BakeMesh applies no transforms
            // So we have to skip calling BakeMesh, because otherwise we'd apply the inverse scale inappropriately and it would be too small.
            bool actuallySkinned = skin.bones.Any(b => b != null);
            Mesh mesh;
            if (actuallySkinned) {
                var temporaryMesh = new Mesh();
                skin.BakeMesh(temporaryMesh);
                var verts = temporaryMesh.vertices;
                var scale = skin.transform.lossyScale;
                var inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                for (var i = 0; i < verts.Length; i++) {
                    verts[i].Scale(inverseScale);
                }
                temporaryMesh.vertices = verts;
                mesh = temporaryMesh;
            } else {
                mesh = skin.sharedMesh;
            }

            if (!mesh) return null;
            return GetAutoSize(skin.gameObject, mesh, forward);
        }

        private static Tuple<float, float, Vector3> GetAutoSize(GameObject obj, Mesh mesh, Vector3 forward) {
            forward = forward.normalized;
            var worldScale = obj.transform.lossyScale.x;
            var length = mesh.vertices
                .Select(v => Vector3.Dot(v, forward))
                .DefaultIfEmpty(0)
                .Max() * worldScale;
            var verticesInFront = mesh.vertices
                .Where(v => Vector3.Dot(v, forward) > 0);
            var verticesInFrontCount = verticesInFront.Count();
            float radius = verticesInFront
                .Select(v => Vector3.Cross(v, forward).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= verticesInFrontCount*0.75)
                .DefaultIfEmpty(0)
                .Max() * worldScale;

            if (length <= 0 || radius <= 0) return null;

            return Tuple.Create(length, radius, forward);
        }

        private static Tuple<float, float, Vector3> GetSize(OGBPenetrator pen) {
            var length = pen.length;
            var radius = pen.radius;
            var forward = Vector3.forward;
            if (!pen.unitsInMeters) {
                length *= pen.transform.lossyScale.x;
                radius *= pen.transform.lossyScale.x;
            }

            var autoSize = GetAutoSize(pen.gameObject);
            if (autoSize != null) {
                if (length == 0) length = autoSize.Item1;
                if (radius == 0) radius = autoSize.Item2;
                forward = autoSize.Item3;
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
            
            DestroyImmediate(pen);
        }
    }
}
