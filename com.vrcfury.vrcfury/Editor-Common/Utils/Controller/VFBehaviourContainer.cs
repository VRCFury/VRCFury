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
        }

        public VFBehaviourContainer() {
        }

        public VFBehaviourContainer(IEnumerable<VFBehaviour> behaviours) : base(
            (behaviours ?? Enumerable.Empty<VFBehaviour>()).Where(behaviour => behaviour != null)
        ) {
        }

        public static VFBehaviourContainer Load(Object obj, VFLoadContext context) {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (obj == null) {
                return new VFBehaviourContainer();
            }

            var field = GetBehavioursInternalProperty(obj);
            if (field != null) {
                var raw = field.GetValue(obj) as ScriptableObject[];
                if (raw != null) {
                    return new VFBehaviourContainer(raw.OfType<StateMachineBehaviour>().Select(behaviour => VFBehaviour.Load(behaviour, context)));
                }
            }

            var oldField = GetBehavioursProperty(obj);
            if (oldField != null) {
                var raw = oldField.GetValue(obj) as StateMachineBehaviour[];
                if (raw != null) {
                    return new VFBehaviourContainer(raw.Select(behaviour => VFBehaviour.Load(behaviour, context)));
                }
            }

            return new VFBehaviourContainer();
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
