using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder {
    public static class AnimatorStateMachineExtensions {
        public static StateMachineBehaviour VAddStateMachineBehaviour(this AnimatorStateMachine machine, Type type) {
            var added = machine.AddStateMachineBehaviour(type);
            if (added == null) {
                ThrowProbablyCompileErrorException($"Failed to create state behaviour of type {type.Name}.");
            }
            return added;
        }

        public static T VAddStateMachineBehaviour<T>(this AnimatorStateMachine machine) where T : StateMachineBehaviour =>
            VAddStateMachineBehaviour(machine, typeof (T)) as T;

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
