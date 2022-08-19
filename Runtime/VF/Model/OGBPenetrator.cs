using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Dynamics;

namespace VF.Model {
    public class OGBPenetrator : MonoBehaviour {

        public float length;
        public float radius;
        public String name;
        
        public static bool MaterialIsDps(Material mat) {
            if (mat == null) return false;
            if (!mat.shader) return false;
            if (mat.shader.name == "Raliv/Penetrator") return true; // Raliv
            if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return true; // XSToon w/ Raliv
            if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return true; // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return true; // UnityChanToonShader w/ DPS
            if (mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0) return true; // Poiyomi 8 w/ TPS
            return false;
        }
        public static Tuple<float, float> GetAutoSize(GameObject obj, bool directOnly = false) {
            foreach (var skin in obj.GetComponents<SkinnedMeshRenderer>()) {
                if (skin.sharedMaterials.Any(MaterialIsDps)) {
                    var auto = GetAutoSize(skin);
                    if (auto != null) return auto;
                }
            }
            foreach (var renderer in obj.GetComponents<MeshRenderer>()) {
                if (renderer.sharedMaterials.Any(MaterialIsDps)) {
                    var auto = GetAutoSize(renderer);
                    if (auto != null) return auto;
                }
            }
            if (directOnly) return null;
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (skin.sharedMaterials.Any(MaterialIsDps)) {
                    var auto = GetAutoSize(skin);
                    if (auto != null) return auto;
                }
            }
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true)) {
                if (renderer.sharedMaterials.Any(MaterialIsDps)) {
                    var auto = GetAutoSize(renderer);
                    if (auto != null) return auto;
                }
            }
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var auto = GetAutoSize(skin);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true)) {
                var auto = GetAutoSize(renderer);
                if (auto != null) return auto;
            }
            return null;
        }
        
        public static Tuple<float, float> GetAutoSize(MeshRenderer renderer) {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (!meshFilter || !meshFilter.sharedMesh) return null;
            return GetAutoSize(renderer.gameObject, meshFilter.sharedMesh);
        }
        
        public static Tuple<float, float> GetAutoSize(SkinnedMeshRenderer skin) {
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
            return GetAutoSize(skin.gameObject, mesh);
        }

        private static Tuple<float, float> GetAutoSize(GameObject obj, Mesh mesh) {
            var worldScale = obj.transform.lossyScale.x;
            var forward = new Vector3(0, 0, 1);
            var length = mesh.vertices
                .Select(v => Vector3.Dot(v, forward)).Max() * worldScale;
            var verticesInFront = mesh.vertices.Where(v => v.z > 0);
            var verticesInFrontCount = verticesInFront.Count();
            float radius = verticesInFront
                .Select(v => new Vector2(v.x, v.y).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= verticesInFrontCount*0.75)
                .Max() * worldScale;

            return Tuple.Create(length, radius);
        }

        public Tuple<float, float> GetSize() {
            var length = this.length;
            var radius = this.radius;
            if (length == 0 || radius == 0) {
                var autoSize = GetAutoSize(gameObject);
                if (autoSize != null) {
                    if (length == 0) length = autoSize.Item1;
                    if (radius == 0) radius = autoSize.Item2;
                }
            }
            return Tuple.Create(length, radius);
        }

        public void Bake(List<string> usedNames = null, bool onlySenders = false) {
            if (usedNames == null) usedNames = new List<string>();
            var obj = this.gameObject;
            OGBUtils.RemoveTPSSenders(obj);

            var size = GetSize();
            var length = size.Item1;
            var radius = size.Item2;

            var name = this.name;
            if (string.IsNullOrWhiteSpace(name)) {
                name = obj.name;
            }

            var forward = new Vector3(0, 0, 1);
            var tightPos = forward * (length / 2);
            var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);

            var extraRadiusForTouch = Math.Min(radius, 0.08f /* 8cm */);
            
            // Extra frot radius should always match for everyone, so when two penetrators collide, both parties experience at the same time
            var extraRadiusForFrot = 0.08f;
            
            Debug.Log("Baking OGB " + obj + " as " + name);

            // Senders
            OGBUtils.AddSender(obj, Vector3.zero, "Length", length, OGBUtils.CONTACT_PEN_MAIN);
            OGBUtils.AddSender(obj, Vector3.zero, "WidthHelper", Mathf.Max(0.01f/obj.transform.lossyScale.x, length - radius*2), OGBUtils.CONTACT_PEN_WIDTH);
            OGBUtils.AddSender(obj, tightPos, "Envelope", radius, OGBUtils.CONTACT_PEN_CLOSE, rotation: tightRot, height: length);
            OGBUtils.AddSender(obj, Vector3.zero, "Root", 0.01f, OGBUtils.CONTACT_PEN_ROOT);

            if (!onlySenders) {
                // Receivers
                var paramPrefix = OGBUtils.GetNextName(usedNames, "OGB/Pen/" + name.Replace('/','_'));
                OGBUtils.AddReceiver(obj, tightPos, paramPrefix + "/TouchSelfClose", "TouchSelfClose", radius+extraRadiusForTouch, OGBUtils.SelfContacts, allowOthers:false, localOnly:true, rotation: tightRot, height: length+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", length+extraRadiusForTouch, OGBUtils.SelfContacts, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, tightPos, paramPrefix + "/TouchOthersClose", "TouchOthersClose", radius+extraRadiusForTouch, OGBUtils.BodyContacts, allowSelf:false, localOnly:true, rotation: tightRot, height: length+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", length+extraRadiusForTouch, OGBUtils.BodyContacts, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", length, new []{OGBUtils.CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", length, new []{OGBUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", length, new []{OGBUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, tightPos, paramPrefix + "/FrotOthersClose", "FrotOthersClose", radius+extraRadiusForFrot, new []{OGBUtils.CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: tightRot, height: length, type: ContactReceiver.ReceiverType.Constant);

                // Version Contacts
                var versionLocalTag = OGBUtils.RandomTag();
                var versionBeaconTag = "OGB_VERSION_" + OGBUtils.beaconVersion;
                OGBUtils.AddSender(obj, Vector3.zero, "Version", 0.01f, versionLocalTag);
                // The "TPS_" + versionTag one is there so that the TPS wizard will delete this version flag if someone runs it
                OGBUtils.AddReceiver(obj, Vector3.one * 0.01f, paramPrefix + "/Version/" + OGBUtils.penVersion, "Version", 0.01f, new []{versionLocalTag, "TPS_" + OGBUtils.RandomTag()}, allowOthers:false, localOnly:true);
                OGBUtils.AddSender(obj, Vector3.zero, "VersionBeacon", 1f, versionBeaconTag);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/VersionMatch", "VersionBeacon", 1f, new []{versionBeaconTag, "TPS_" + OGBUtils.RandomTag()}, allowSelf:false, localOnly:true);
            }
            
            DestroyImmediate(this);
        }
    }
}
