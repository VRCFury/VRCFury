using System;
using System.Collections.Generic;

namespace VF.Model.StateAction {
    [Serializable]
    internal class FlipBookBuilderAction : Action {
        [Obsolete] public List<State> states;
        public List<FlipBookPage> pages = new List<FlipBookPage>();

        [Serializable]
        public class FlipBookPage {
            public State state = new State();
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

        public override bool Equals(Action other) => Equals(other as FlipBookBuilderAction); 
        public bool Equals(FlipBookBuilderAction other) {
            if (other == null) return false;
            if (pages.Count != other.pages.Count) return false;
            for (int i = 0; i < pages.Count; i++) {
                if (!pages[i].state.Equals(other.pages[i].state)) return false; 
            }
            return true;
        }
    }
}