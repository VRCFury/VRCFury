using UdonSharp;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace com.vrcfury.udon {
    [AddComponentMenu("VRCFury/SPS Socket Udon Support (VRCFury)")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SpsSocketUdonSupport : UdonSharpBehaviour {
        [SerializeField, HideInInspector] internal VRCContactReceiver tip;
        [SerializeField, HideInInspector] internal VRCContactReceiver root;
        [SerializeField, HideInInspector] internal VRCContactReceiver width;
        private Component[] callbacks = new Component[0];
        private ContactSenderProxy[] tipContacts = new ContactSenderProxy[0];
        private ContactSenderProxy[] rootContacts = new ContactSenderProxy[0];
        private ContactSenderProxy[] widthContacts = new ContactSenderProxy[0];
        private float[] cachedPlugLengths = new float[0];
        private float[] cachedPlugWidths = new float[0];
        private int lastMeasurementFrame = -1;
        internal ContactEnterInfo contactEnterInfo;
        internal ContactExitInfo contactExitInfo;

        public static SpsSocketUdonSupport Get(GameObject root) {
            return root.GetComponent<SpsSocketUdonSupport>();
        }

        public void _Register(Component callback) {
            if (callback == null) return;
            VrcfuryUtils.AddToList(ref callbacks, callback);
        }

        public void _OnTipEnter() {
            if (tipContacts.IndexOf(contactEnterInfo.contactSender) >= 0) return;
            var id = VrcfuryUtils.AddToList(ref tipContacts, contactEnterInfo.contactSender);
            if (id >= cachedPlugLengths.Length) {
                var newLengths = new float[id + 1];
                var newWidths = new float[id + 1];
                for (var i = 0; i < cachedPlugLengths.Length; i++) {
                    newLengths[i] = cachedPlugLengths[i];
                    newWidths[i] = cachedPlugWidths[i];
                }
                cachedPlugLengths = newLengths;
                cachedPlugWidths = newWidths;
            }
            cachedPlugLengths[id] = 0.3f;
            cachedPlugWidths[id] = 0.03f;
        }

        public void _OnTipExit() {
            tipContacts.RemoveFromList(tipContacts.IndexOf(contactExitInfo.contactSender));
        }

        public void _OnWidthEnter() {
            VrcfuryUtils.AddToList(ref widthContacts, contactEnterInfo.contactSender);
        }

        public void _OnWidthExit() {
            widthContacts.RemoveFromList(widthContacts.IndexOf(contactExitInfo.contactSender));
        }

        public void _OnRootEnter() {
            VrcfuryUtils.AddToList(ref rootContacts, contactEnterInfo.contactSender);
        }

        public void _OnRootExit() {
            rootContacts.RemoveFromList(rootContacts.IndexOf(contactExitInfo.contactSender));
        }

        public float _GetPlugLength() {
            UpdateMeasurements();
            var nearestTip = VrcfuryUtils.GetNearestSender(tip, tipContacts);
            if (nearestTip == null) return 0;
            var nearestTipIndex = tipContacts.IndexOf(nearestTip);
            return cachedPlugLengths[nearestTipIndex];
        }

        public float _GetPlugWidth() {
            UpdateMeasurements();
            var nearestTip = VrcfuryUtils.GetNearestSender(tip, tipContacts);
            if (nearestTip == null) return 0;
            var nearestTipIndex = tipContacts.IndexOf(nearestTip);
            return cachedPlugWidths[nearestTipIndex];
        }

        public float _GetProximity() {
            UpdateMeasurements();
            var tipContact = VrcfuryUtils.GetNearestSender(tip, tipContacts);
            if (tipContact == null) return float.PositiveInfinity;
            var nearestTipIndex = tipContacts.IndexOf(tipContact);
            var tipProximity = tip.CalculateProximity(tipContact);
            if (tipProximity < 1) return tip.radius * (1 - tipProximity);

            var matchingRoot = VrcfuryUtils.GetMatchingContact(tipContact, rootContacts);
            if (matchingRoot == null) return 0;
            return root.radius * (1 - root.CalculateProximity(matchingRoot))
                   - cachedPlugLengths[nearestTipIndex]
                   + 0.01f;
        }

        private void UpdateMeasurements() {
            if (lastMeasurementFrame == Time.frameCount) return;
            lastMeasurementFrame = Time.frameCount;
            for (var i = 0; i < tipContacts.Length; i++) {
                if (tipContacts[i] != null && tipContacts[i].isValid) UpdateMeasurement(i);
            }
        }

        private void UpdateMeasurement(int tipIndex) {
            var tipContact = tipContacts[tipIndex];
            var tipProximity = tip.CalculateProximity(tipContact);
            if (tipProximity >= 1) return;

            var matchingRoot = VrcfuryUtils.GetMatchingContact(tipContact, rootContacts);

            if (matchingRoot != null) {
                var rootProximity = root.CalculateProximity(matchingRoot);
                cachedPlugLengths[tipIndex] = tip.radius * (tipProximity - rootProximity) + 0.01f;
            }

            var matchingWidth = VrcfuryUtils.GetMatchingContact(tipContact, widthContacts);

            if (matchingWidth == null) return;
            var widthProximity = width.CalculateProximity(matchingWidth);
            cachedPlugWidths[tipIndex] = tip.radius / 2 * (tipProximity - widthProximity);
        }

    }
}
