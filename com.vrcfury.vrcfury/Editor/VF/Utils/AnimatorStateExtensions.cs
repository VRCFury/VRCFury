using System;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Builder {
    static internal class AnimatorStateExtensions {
        public static StateMachineBehaviour VAddStateMachineBehaviour(this AnimatorState state, Type type) {
            var added = state.AddStateMachineBehaviour(type);
            if (added == null) {
                AnimatorStateMachineExtensions.ThrowProbablyCompileErrorException($"Failed to create state behaviour of type {type.Name}.");
            }
            return added;
        }

        public static T VAddStateMachineBehaviour<T>(this AnimatorState state) where T : StateMachineBehaviour =>
            VAddStateMachineBehaviour(state, typeof (T)) as T;
    }
}
