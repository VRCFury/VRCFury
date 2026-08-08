using UdonSharp;
using UnityEngine;
using VRC.Dynamics;

namespace com.vrcfury.udon {
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    internal class SpsContactForwarder : UdonSharpBehaviour {
        [SerializeField, HideInInspector] internal UdonSharpBehaviour target;
        [SerializeField, HideInInspector] internal string enterField;
        [SerializeField, HideInInspector] internal string exitField;
        [SerializeField, HideInInspector] internal string onEnter;
        [SerializeField, HideInInspector] internal string onExit;

        public override void OnContactEnter(ContactEnterInfo contactInfo) {
            target.SetProgramVariable(enterField, contactInfo);
            target.SendCustomEvent(onEnter);
        }

        public override void OnContactExit(ContactExitInfo contactInfo) {
            target.SetProgramVariable(exitField, contactInfo);
            target.SendCustomEvent(onExit);
        }
    }
}
