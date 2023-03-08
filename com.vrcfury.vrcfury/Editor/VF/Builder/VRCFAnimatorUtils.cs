using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder {
    public class VRCFAnimatorUtils {
        public static StateMachineBehaviour AddStateMachineBehaviour(AnimatorStateMachine machine, Type type) {
            var added = machine.AddStateMachineBehaviour(type);
            if (added == null) {
                ThrowProbablyCompileErrorException($"Failed to create state behaviour of type {type.Name}.");
            }
            return added;
        }

        public static T AddStateMachineBehaviour<T>(AnimatorStateMachine machine) where T : StateMachineBehaviour =>
            AddStateMachineBehaviour(machine, typeof (T)) as T;

        public static StateMachineBehaviour AddStateMachineBehaviour(AnimatorState state, Type type) {
            var added = state.AddStateMachineBehaviour(type);
            if (added == null) {
                ThrowProbablyCompileErrorException($"Failed to create state behaviour of type {type.Name}.");
            }
            return added;
        }

        public static T AddStateMachineBehaviour<T>(AnimatorState state) where T : StateMachineBehaviour =>
            AddStateMachineBehaviour(state, typeof (T)) as T;

        public static void ThrowProbablyCompileErrorException(string msg) {
            throw new VRCFBuilderException(
                msg +
                " Usually this means you have unresolved script compilation errors. Click 'Clear' on the" +
                " top left of the unity log, and fix any red errors that remain after clearing." +
                " If there are no errors, try restarting unity. If nothing fixes it, report on" +
                " https://vrcfury.com/discord"
            );
        }
    }
}
