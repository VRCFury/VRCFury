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
            var output = msg;
            output += " Usually this means you have an error in one of your other non-vrcfury packages. Delete or upgrade it to resolve the issue.";
            output += "\n\n";
            var errors = ErrorCatcher.Errors;
            if (errors.Count > 0) {
                output += "Detected error:\n";
                output += errors.First();
            } else {
                output += "No errors were detected. You may need to restart unity.";
            }

            output += "\n\n";
            output += "If nothing fixes it, report on https://vrcfury.com/discord";

            throw new VRCFBuilderException(output);
        }
    }
}
