using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Feature;
using VF.Utils;

namespace VF.Builder {
    public static class AnimatorStateMachineExtensions {
        public static StateMachineBehaviour VAddStateMachineBehaviour(this AnimatorStateMachine machine, Type type) {
            // Unity 2019 and lower log an error if this isn't persistent
            StateMachineBehaviour added = null;
            CleanupLegacyBuilder.WithTemporaryPersistence(machine, () => {
                added = machine.AddStateMachineBehaviour(type);
            });
            if (added == null) {
                ThrowProbablyCompileErrorException($"Failed to create state behaviour of type {type.Name}.");
            }
            return added;
        }

        public static T VAddStateMachineBehaviour<T>(this AnimatorStateMachine machine) where T : StateMachineBehaviour =>
            VAddStateMachineBehaviour(machine, typeof (T)) as T;

        public static void ThrowProbablyCompileErrorException(string msg) {
            var errors = ErrorCatcher.Errors;
            if (errors.Count > 0) {
                throw new SneakyException(
                    "One of the scripts in your project failed to compile. You should either remove, upgrade, or reinstall the broken plugin:\n\n" +
                    errors.First());
            }

            throw new SneakyException(
                "One of the scripts in your project failed to compile. Press Ctrl+Shift+C, click Clear, then fix the errors that remain (do not run another build first)");
        }
    }
}
