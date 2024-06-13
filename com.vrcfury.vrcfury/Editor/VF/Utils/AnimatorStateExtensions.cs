using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Feature;
using VF.Utils;

namespace VF.Builder {
    internal static class AnimatorStateExtensions {
        public static StateMachineBehaviour VAddStateMachineBehaviour(this AnimatorState state, Type type) {
            // Unity 2019 and lower log an error if this isn't persistent
            StateMachineBehaviour added = null;
            CleanupLegacyBuilder.WithTemporaryPersistence(state, () => {
                added = state.AddStateMachineBehaviour(type);
            });
            if (added == null) {
                AnimatorStateMachineExtensions.ThrowProbablyCompileErrorException($"Failed to create state behaviour of type {type.Name}.");
            }
            VrcfObjectFactory.Register(added);
            return added;
        }

        public static T VAddStateMachineBehaviour<T>(this AnimatorState state) where T : StateMachineBehaviour =>
            VAddStateMachineBehaviour(state, typeof (T)) as T;
    }
}
