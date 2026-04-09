using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ObjectToggleAction : Action {
        public GameObject obj;
        public Mode mode = Mode.TurnOn;

        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                mode = Mode.Toggle;
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 1;
        }

        public enum Mode {
            TurnOn,
            TurnOff,
            Toggle
        }
    }
}