using System;
using System.Collections.Generic;

namespace VF.Model.StateAction {
    [Serializable]
    internal class FlipBookBuilderAction : Action {
        [Obsolete] public List<State> states;
        public List<FlipBookPage> pages;

        [Serializable]
        public class FlipBookPage {
            public State state;
        }

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                pages.Clear();
                foreach (var state in states) {
                    pages.Add(new FlipBookPage() { state = state });
                }
                states.Clear();
            }

            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
}