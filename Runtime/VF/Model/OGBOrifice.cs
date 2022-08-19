using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace VF.Model {
    public enum AddLight {
        None,
        Hole,
        Ring
    }
    
    public class OGBOrifice : MonoBehaviour {
        public AddLight addLight;
        public String name;

        private static bool IsHole(Light light) {
            var rangeId = light.range % 0.1;
            return rangeId >= 0.005f && rangeId < 0.015f;
        }
        private static bool IsRing(Light light) {
            var rangeId = light.range % 0.1;
            return rangeId >= 0.015f && rangeId < 0.025f;
        }
        private static bool IsNormal(Light light) {
            var rangeId = light.range % 0.1;
            return rangeId >= 0.045f && rangeId <= 0.055f;
        }
        
        public static Tuple<Vector3, bool> GetInfoFromLights(GameObject obj) {
            var isRing = false;
            Light main = null;
            Light normal = null;
            foreach (Transform child in obj.transform) {
                var light = child.gameObject.GetComponent<Light>();
                if (light != null) {
                    if (main == null) {
                        if (IsHole(light)) {
                            main = light;
                        } else if (IsRing(light)) {
                            main = light;
                            isRing = true;
                        }
                    }
                    if (normal == null && IsNormal(light)) {
                        normal = light;
                    }
                }
            }

            if (main == null || normal == null) return null;

            var forward = Vector3.forward;
            if (normal != null) {
                forward = (normal.transform.localPosition - main.transform.localPosition).normalized;
            }

            return Tuple.Create(forward, isRing);
        }
        
        public void Bake(List<string> usedNames = null, bool onlySenders = false) {
            if (usedNames == null) usedNames = new List<string>();
            var obj = gameObject;

            var autoInfo = GetInfoFromLights(obj);

            var forward = Vector3.forward;
            if (autoInfo != null) {
                forward = autoInfo.Item1;
            }

            var name = this.name;
            if (string.IsNullOrWhiteSpace(name)) {
                name = obj.name;
            }

            Debug.Log("Baking OGB " + obj + " as " + name);

            // Senders
            OGBUtils.AddSender(obj, Vector3.zero, "Root", 0.01f, OGBUtils.CONTACT_ORF_MAIN);
            OGBUtils.AddSender(obj, forward * 0.01f, "Front", 0.01f, OGBUtils.CONTACT_ORF_NORM);

            if (!onlySenders) {
                // Receivers
                var oscDepth = 0.25f;
                var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);
                var frotRadius = 0.1f;
                var frotPos = 0.05f;
                var closeRadius = 0.1f;
                var paramPrefix = OGBUtils.GetNextName(usedNames, "OGB/Orf/" + name.Replace('/','_'));
                OGBUtils.AddReceiver(obj, forward * -oscDepth, paramPrefix + "/TouchSelf", "TouchSelf", oscDepth, OGBUtils.SelfContacts, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, forward * -(oscDepth/2), paramPrefix + "/TouchSelfClose", "TouchSelfClose", closeRadius, OGBUtils.SelfContacts, allowOthers:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, forward * -oscDepth, paramPrefix + "/TouchOthers", "TouchOthers", oscDepth, OGBUtils.BodyContacts, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, forward * -(oscDepth/2), paramPrefix + "/TouchOthersClose", "TouchOthersClose", closeRadius, OGBUtils.BodyContacts, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{OGBUtils.CONTACT_PEN_ROOT}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, forward * -oscDepth, paramPrefix + "/PenOthers", "PenOthers", oscDepth, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, forward * -(oscDepth/2), paramPrefix + "/PenOthersClose", "PenOthersClose", closeRadius, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{OGBUtils.CONTACT_PEN_ROOT}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, forward * frotPos, paramPrefix + "/FrotOthers", "FrotOthers", frotRadius, new []{OGBUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);

                // Version Contacts
                var versionLocalTag = OGBUtils.RandomTag();
                var versionBeaconTag = "OGB_VERSION_" + OGBUtils.beaconVersion;
                OGBUtils.AddSender(obj, Vector3.zero, "Version", 0.01f, versionLocalTag);
                // The "TPS_" + versionTag one is there so that the TPS wizard will delete this version flag if someone runs it
                OGBUtils.AddReceiver(obj, Vector3.one * 0.01f, paramPrefix + "/Version/" + OGBUtils.orfVersion, "Version", 0.01f, new []{versionLocalTag, "TPS_" + OGBUtils.RandomTag()}, allowOthers:false, localOnly:true);
                OGBUtils.AddSender(obj, Vector3.zero, "VersionBeacon", 1f, versionBeaconTag);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/VersionMatch", "VersionBeacon", 1f, new []{versionBeaconTag, "TPS_" + OGBUtils.RandomTag()}, allowSelf:false, localOnly:true);
            }

            if (autoInfo == null && addLight != AddLight.None) {
                foreach (var light in obj.GetComponentsInChildren<Light>(true)) {
                    OGBUtils.RemoveComponent(light);
                }

                var main = new GameObject("Root");
                main.transform.SetParent(obj.transform, false);
                var mainLight = main.AddComponent<Light>();
                mainLight.type = LightType.Point;
                mainLight.color = Color.black;
                mainLight.range = addLight == AddLight.Ring ? 0.42f : 0.41f;
                mainLight.shadows = LightShadows.None;
                mainLight.renderMode = LightRenderMode.ForceVertex;

                var front = new GameObject("Front");
                front.transform.SetParent(obj.transform, false);
                var frontLight = front.AddComponent<Light>();
                front.transform.localPosition = new Vector3(0, 0, 0.01f / obj.transform.lossyScale.x);
                frontLight.type = LightType.Point;
                frontLight.color = Color.black;
                frontLight.range = 0.45f;
                frontLight.shadows = LightShadows.None;
                frontLight.renderMode = LightRenderMode.ForceVertex;
            }
            
            DestroyImmediate(this);
        }
    }
}