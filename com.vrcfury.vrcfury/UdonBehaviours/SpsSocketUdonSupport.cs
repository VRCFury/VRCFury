using UdonSharp;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace com.vrcfury.udon {
    [AddComponentMenu("VRCFury/SPS Socket Udon Support (VRCFury)")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SpsSocketUdonSupport : UdonSharpBehaviour {
        [SerializeField, HideInInspector] internal VRCContactReceiver plugRoot;
        [SerializeField, HideInInspector] internal VRCContactReceiver width;
        [SerializeField, HideInInspector] internal string[] nativeTags;
        private Component[] callbacks = new Component[0];
        private ContactSenderProxy[] plugContacts = new ContactSenderProxy[0];
        private ContactSenderProxy[] widthContacts = new ContactSenderProxy[0];
        private Vector2[] cachedPlugSizes = new Vector2[0];
        private bool[] nativePlugs = new bool[0];
        private bool[] measuredLengths = new bool[0];
        private bool[] measuredWidths = new bool[0];
        private int lastMeasurementFrame = -1;
        private int nearestPlug = -1;
        private float cachedProximity = float.PositiveInfinity;
        internal ContactEnterInfo contactEnterInfo;
        internal ContactExitInfo contactExitInfo;

        public static SpsSocketUdonSupport Get(GameObject root) {
            return root.GetComponent<SpsSocketUdonSupport>();
        }

        public void _Register(Component callback) {
            if (callback == null) return;
            VrcfuryUtils.AddToList(ref callbacks, callback);
        }

        public void _OnEnter() {
            var id = VrcfuryUtils.AddToList(ref plugContacts, contactEnterInfo.contactSender);
            lastMeasurementFrame = -1;
            cachedPlugSizes = VrcfuryUtils.Resize(cachedPlugSizes, plugContacts.Length);
            nativePlugs = VrcfuryUtils.Resize(nativePlugs, plugContacts.Length);
            measuredLengths = VrcfuryUtils.Resize(measuredLengths, plugContacts.Length);
            measuredWidths = VrcfuryUtils.Resize(measuredWidths, plugContacts.Length);
            cachedPlugSizes[id] = new Vector2(0.3f, 0.03f);
            measuredLengths[id] = false;
            measuredWidths[id] = false;
            nativePlugs[id] = false;
            var matchingTags = contactEnterInfo.matchingTags;
            if (matchingTags == null) return;
            for (var t = 0; t < matchingTags.Length; t++) {
                for (var n = 0; n < nativeTags.Length; n++) {
                    if (matchingTags[t] == nativeTags[n]) nativePlugs[id] = true;
                }
            }
        }

        public void _OnExit() {
            plugContacts.RemoveFromList(plugContacts.IndexOf(contactExitInfo.contactSender));
            lastMeasurementFrame = -1;
        }

        public void _OnWidthEnter() {
            VrcfuryUtils.AddToList(ref widthContacts, contactEnterInfo.contactSender);
            lastMeasurementFrame = -1;
        }

        public void _OnWidthExit() {
            widthContacts.RemoveFromList(widthContacts.IndexOf(contactExitInfo.contactSender));
            lastMeasurementFrame = -1;
        }

        public float _GetPlugLength() {
            UpdateMeasurements();
            return nearestPlug < 0 ? 0 : cachedPlugSizes[nearestPlug].x;
        }

        public float _GetPlugWidth() {
            UpdateMeasurements();
            // The existing API's "width" is a radius. Native SPS uses overall radius X.
            return nearestPlug < 0 ? 0 : cachedPlugSizes[nearestPlug].y;
        }

        public float _GetProximity() {
            UpdateMeasurements();
            return cachedProximity;
        }

        private float GetReceiverRadius() {
            return plugRoot.radius * Mathf.Abs(plugRoot.transform.lossyScale.x);
        }

        private float GetProximity(ContactSenderProxy sender) {
            return plugRoot.isActiveAndEnabled ? plugRoot.CalculateProximity(sender) : 0;
        }

        private void UpdateMeasurements() {
            if (lastMeasurementFrame == Time.frameCount) return;
            lastMeasurementFrame = Time.frameCount;
            for (var i = 0; i < plugContacts.Length; i++) {
                var sender = plugContacts[i];
                if (sender == null || !sender.isValid) continue;
                if (nativePlugs[i]) {
                    var scale = sender.scale;
                    if (scale.z > 0) cachedPlugSizes[i].x = scale.z;
                    if (scale.x > 0) cachedPlugSizes[i].y = scale.x;
                    continue;
                }

                var proximity = GetProximity(sender);
                if (proximity <= 0) continue;
                var radius = GetReceiverRadius();
                if (proximity < 1 || !measuredLengths[i]) {
                    var distance = Vector3.Distance(plugRoot.transform.position, sender.position);
                    var length = distance - radius * (1 - proximity);
                    if (length > 0 && length <= 3) {
                        cachedPlugSizes[i].x = length;
                        measuredLengths[i] = true;
                    }
                }

                // A saturated main sphere underestimates width. Only use it to seed a missing measurement.
                if (width == null || (proximity >= 1 && measuredWidths[i])) continue;
                var helper = VrcfuryUtils.GetMatchingContact(sender, widthContacts);
                if (helper == null) continue;
                var helperProximity = width.CalculateProximity(helper);
                if (helperProximity <= 0 || helperProximity >= 1) continue;
                var helperRadius = Vector3.Distance(width.transform.position, helper.position)
                    - radius * (1 - helperProximity);
                // Legacy width spheres clamp to 1 cm; that clamp does not encode a width.
                if (helperRadius <= 0.01001f) continue;
                var plugRadius = radius * (proximity - helperProximity) / 2;
                if (plugRadius > 0 && plugRadius <= 3) {
                    cachedPlugSizes[i].y = plugRadius;
                    measuredWidths[i] = true;
                }
            }
            var nearest = VrcfuryUtils.GetNearestSender(plugRoot, plugContacts);
            nearestPlug = nearest == null ? -1 : plugContacts.IndexOf(nearest);
            cachedProximity = float.PositiveInfinity;
            if (nearestPlug < 0) return;
            var nearestProximity = GetProximity(nearest);
            cachedProximity = !nativePlugs[nearestPlug] && nearestProximity < 1
                ? GetReceiverRadius() * (1 - nearestProximity)
                : Vector3.Distance(plugRoot.transform.position, nearest.position) - cachedPlugSizes[nearestPlug].x;
        }
    }
}
