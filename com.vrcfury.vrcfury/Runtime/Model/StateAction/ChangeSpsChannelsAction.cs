using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ChangeSpsChannelsAction : Action {
        public GameObject target;
        public SpsChannelSettings channelSettings = new();
    }
}
