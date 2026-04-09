using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class Visemes : NewFeatureModel {
        [Obsolete] public float transitionTime = -1;
        [Obsolete] public State state_sil;
        public bool instant = false;
        public State state_PP;
        public State state_FF;
        public State state_TH;
        public State state_DD;
        public State state_kk;
        public State state_CH;
        public State state_SS;
        public State state_nn;
        public State state_RR;
        public State state_aa;
        public State state_E;
        public State state_I;
        public State state_O;
        public State state_U;

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                instant = transitionTime == 0;
            }
#pragma warning restore 0612
            return false;
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
}