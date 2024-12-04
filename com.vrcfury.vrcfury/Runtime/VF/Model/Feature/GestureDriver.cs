using System;
using System.Collections.Generic;
using System.Linq;
using VF.Model.StateAction;
using VF.Upgradeable;

namespace VF.Model.Feature {
    [Serializable]
    internal class GestureDriver : NewFeatureModel {
        public List<Gesture> gestures = new List<Gesture>();

        [Serializable]
        public class Gesture : VrcfUpgradeable {
            public Hand hand;
            public HandSign sign;
            public HandSign comboSign;
            public State state = new State();
            public State exitState = new State();
            [Obsolete] public bool disableBlinking;
            public bool customTransitionTime;
            public float transitionTime = 0;
            public bool enableLockMenuItem;
            public string lockMenuItem;
            public bool enableExclusiveTag;
            public string exclusiveTag;
            public bool enableWeight;
            public bool enableExit;

            public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
                if (fromVersion < 1) {
                    if (disableBlinking && !state.actions.Any(a => a is BlockBlinkingAction)) {
                        state.actions.Add(new BlockBlinkingAction());
                    }
                }
                return false;
#pragma warning restore 0612
            }

            public override int GetLatestVersion() {
                return 1;
            }
        }

        public enum Hand {
            EITHER,
            LEFT,
            RIGHT,
            COMBO
        }

        public enum HandSign {
            NEUTRAL,
            FIST,
            HANDOPEN,
            FINGERPOINT,
            VICTORY,
            ROCKNROLL,
            HANDGUN,
            THUMBSUP
        }
    }
}
