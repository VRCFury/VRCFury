using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {
    [Serializable]
    internal class State : IEquatable<State> {
        [SerializeReference] public List<Action> actions = new List<Action>();
        public override bool Equals(object other) => Equals(other as State);
        public bool Equals(State other) { 
            if (other == null) return false;
            return actions.Count == other.actions.Count && actions.All(other.actions.Contains);
        }
        public override int GetHashCode() { return 0; }
    }
}