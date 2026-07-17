using System;
using UnityEngine.Serialization;
using VF.Upgradeable;

namespace VF.Model.StateAction {
    [Serializable]
    internal class Action : VrcfUpgradeable, IEquatable<Action> {
        public bool desktopActive = false;
        public bool androidActive = false;

        public bool localOnly = false;
        public bool remoteOnly = false;
        public bool friendsOnly = false;

        public override bool Equals(object other) => Equals(other as Action);
        public virtual bool Equals(Action other) { 
            return false; 
        }
        public override int GetHashCode() { return 0; }
    }
}
