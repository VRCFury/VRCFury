using System;
using System.Collections.Generic;
using UnityEngine;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {
    [Serializable]
    internal class State {
        [SerializeReference] public List<Action> actions = new List<Action>();
    }
}