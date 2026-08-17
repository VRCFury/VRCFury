using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFBehaviourContainer : List<VFBehaviour> {
        private abstract class Reflection : ReflectionHelper {
            public static readonly PropertyInfo AnimatorStateBehavioursInternal =
                typeof(AnimatorState).VFProperty("behaviours_Internal");
            public static readonly PropertyInfo AnimatorStateMachineBehavioursInternal =
                typeof(AnimatorStateMachine).VFProperty("behaviours_Internal");
            public static readonly PropertyInfo AnimatorStateBehaviours =
                typeof(AnimatorState).VFProperty("behaviours");
            public static readonly PropertyInfo AnimatorStateMachineBehaviours =
                typeof(AnimatorStateMachine).VFProperty("behaviours");
            public static readonly FieldInfo AnimatorControllerLayerBehaviours =
                typeof(AnimatorControllerLayer).VFField("m_Behaviours");
            private static readonly System.Type StateBehavioursPair =
                AnimatorControllerLayerBehaviours?.FieldType.GetElementType();
            public static readonly FieldInfo StateBehavioursPairState =
                StateBehavioursPair?.VFField("m_State");
            public static readonly FieldInfo StateBehavioursPairBehaviours =
                StateBehavioursPair?.VFField("m_Behaviours");
        }

        public VFBehaviourContainer() {
        }

        public VFBehaviourContainer(IEnumerable<VFBehaviour> behaviours) : base(
            (behaviours ?? Enumerable.Empty<VFBehaviour>()).Where(behaviour => behaviour != null)
        ) {
        }

        public static VFBehaviourContainer Load(Object obj, VFLoadContext context) {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            return new VFBehaviourContainer(
                GetRawBehaviours(obj).Select(behaviour => VFBehaviour.Load(behaviour, context))
            );
        }

        public static StateMachineBehaviour[] GetRawBehaviours(Object obj) {
            if (obj == null) return System.Array.Empty<StateMachineBehaviour>();

            var field = GetBehavioursInternalProperty(obj);
            if (field != null) {
                if (field.GetValue(obj) is ScriptableObject[] raw) {
                    return raw.OfType<StateMachineBehaviour>().ToArray();
                }
            }

            var oldField = GetBehavioursProperty(obj);
            if (oldField != null) {
                if (oldField.GetValue(obj) is StateMachineBehaviour[] raw) {
                    return raw;
                }
            }

            return System.Array.Empty<StateMachineBehaviour>();
        }

        public static StateMachineBehaviour[] GetRawOverrideBehaviours(
            AnimatorControllerLayer layer,
            AnimatorState state
        ) {
            if (layer == null || state == null) return System.Array.Empty<StateMachineBehaviour>();
            var pairsField = Reflection.AnimatorControllerLayerBehaviours;
            var stateField = Reflection.StateBehavioursPairState;
            var behavioursField = Reflection.StateBehavioursPairBehaviours;
            if (pairsField == null || stateField == null || behavioursField == null) {
                return Fallback();
            }
            var pairs = pairsField.GetValue(layer) as System.Array;
            if (pairs == null) return Fallback();
            foreach (var pair in pairs) {
                if (pair == null || stateField.GetValue(pair) as AnimatorState != state) continue;
                var raw = behavioursField.GetValue(pair) as ScriptableObject[];
                return raw?.OfType<StateMachineBehaviour>().ToArray()
                       ?? System.Array.Empty<StateMachineBehaviour>();
            }
            return System.Array.Empty<StateMachineBehaviour>();

            StateMachineBehaviour[] Fallback() =>
                layer.GetOverrideBehaviours(state) ?? System.Array.Empty<StateMachineBehaviour>();
        }

        public VFBehaviourContainer Clone() {
            return new VFBehaviourContainer(this.Select(behaviour => behaviour.Clone()));
        }

        public VFBehaviour AddBehaviour<T>(System.Action<T> init = null) where T : StateMachineBehaviour {
            var added = VrcfObjectFactory.Create<T>();
            init?.Invoke(added);
            var behaviour = new VFBehaviour(added);
            Add(behaviour);
            return behaviour;
        }

        public IEnumerable<T> GetBehaviours<T>() where T : StateMachineBehaviour {
            return this
                .Select(behaviour => behaviour.Read<T>())
                .Where(behaviour => behaviour != null);
        }

        public VFBehaviour FindBehaviour<T>(System.Func<T, bool> predicate = null) where T : StateMachineBehaviour {
            return this.FirstOrDefault(behaviour => {
                var typed = behaviour.Read<T>();
                if (typed == null) return false;
                return predicate?.Invoke(typed) ?? true;
            });
        }

        public bool HasBehaviour<T>() where T : StateMachineBehaviour {
            return this.Any(behaviour => behaviour.Read<T>() != null);
        }

        public void ReplaceWith(IEnumerable<VFBehaviour> behaviours) {
            Clear();
            AddRange((behaviours ?? Enumerable.Empty<VFBehaviour>()).Where(behaviour => behaviour != null));
        }

        private static PropertyInfo GetBehavioursInternalProperty(Object obj) {
            if (obj is AnimatorState) {
                return Reflection.AnimatorStateBehavioursInternal;
            }
            if (obj is AnimatorStateMachine) {
                return Reflection.AnimatorStateMachineBehavioursInternal;
            }
            return null;
        }

        private static PropertyInfo GetBehavioursProperty(Object obj) {
            if (obj is AnimatorState) {
                return Reflection.AnimatorStateBehaviours;
            }
            if (obj is AnimatorStateMachine) {
                return Reflection.AnimatorStateMachineBehaviours;
            }
            return null;
        }
    }
}
