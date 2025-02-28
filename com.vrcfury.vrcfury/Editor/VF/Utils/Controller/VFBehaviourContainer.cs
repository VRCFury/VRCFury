using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VF.Utils.Controller {
    internal interface VFBehaviourContainer : VFPrettyNamed {
        StateMachineBehaviour[] behaviours { get; set; }
        Object behaviourContainer { get; }
    }

    internal static class VFBehaviourContainerExtensions {
        public static T AddBehaviour<T>(this VFBehaviourContainer c) where T : StateMachineBehaviour {
            var added = VrcfObjectFactory.Create<T>();
            c.behaviours = c.behaviours.Append(added).ToArray();
            return added;
        }
        
        public static void RemoveBadBehaviours(this VFBehaviourContainer c) {
            var obj = c.behaviourContainer;
            var location = c.prettyName;

            var field = obj.GetType()
                .GetProperty("behaviours_Internal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) {
                // 2022+
                var raw = field.GetValue(obj) as ScriptableObject[];
                if (raw == null) return;
                var clean = raw.OfType<StateMachineBehaviour>().Cast<ScriptableObject>().ToArray();
                if (raw.Length != clean.Length) {
                    field.SetValue(obj, clean);
                    Debug.LogWarning($"{location} contained a corrupt behaviour. It has been removed.");
                }
            } else {
                // 2019
                var oldField = obj.GetType().GetProperty("behaviours", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (oldField == null) return;
                var raw = oldField.GetValue(obj) as StateMachineBehaviour[];
                if (raw == null) return;
                var clean = raw.Cast<object>().OfType<StateMachineBehaviour>().ToArray();
                if (raw.Length != clean.Length) {
                    oldField.SetValue(obj, clean);
                    Debug.LogWarning($"{location} contained a corrupt behaviour. It has been removed.");
                }
            }
        }
    }
}
