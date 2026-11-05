using UdonSharp;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace com.vrcfury.udon {
    [AddComponentMenu("VRCFury/SPS Plug Udon Support (VRCFury)")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SpsPlugUdonSupport : UdonSharpBehaviour {
        [SerializeField, HideInInspector] internal VRCContactReceiver[] socketRoots;
        [SerializeField, HideInInspector] internal Transform plugRoot;
        [SerializeField, HideInInspector] internal float plugLengthLocal;
        [SerializeField, HideInInspector] internal float plugWidthLocal;
        internal ContactEnterInfo contactEnterInfo;
        internal ContactExitInfo contactExitInfo;
        private Component[] callbacks = new Component[0];
        private ContactSenderProxy[] contacts = new ContactSenderProxy[0];
        private int lastMeasurementFrame = -1;
        private float cachedProximity = float.PositiveInfinity;

        public static SpsPlugUdonSupport Get(GameObject root) {
            return root.GetComponent<SpsPlugUdonSupport>();
        }

        public void _Register(Component callback) {
            if (callback == null) return;
            VrcfuryUtils.AddToList(ref callbacks, callback);
        }

        public void _OnEnter() {
            VrcfuryUtils.AddToList(ref contacts, contactEnterInfo.contactSender);
            lastMeasurementFrame = -1;
        }

        public void _OnExit() {
            contacts.RemoveFromList(contacts.IndexOf(contactExitInfo.contactSender));
            lastMeasurementFrame = -1;
        }

        public float _GetPlugLength() {
            return plugLengthLocal * Mathf.Abs(plugRoot.lossyScale.x);
        }

        public float _GetPlugWidth() {
            return plugWidthLocal * Mathf.Abs(plugRoot.lossyScale.x);
        }

        public float _GetProximity() {
            if (lastMeasurementFrame == Time.frameCount) return cachedProximity;
            lastMeasurementFrame = Time.frameCount;

            var nearestSocket = VrcfuryUtils.GetNearestSender(socketRoots, contacts);
            cachedProximity = nearestSocket == null
                ? float.PositiveInfinity
                : Vector3.Distance(plugRoot.position, nearestSocket.position) - _GetPlugLength();
            return cachedProximity;
        }
    }
}
