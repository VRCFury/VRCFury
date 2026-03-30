using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Model;
using VF.Utils;

namespace VF {
    internal static class GuidWrapperExtensions {
        [CanBeNull]
        public static T Get<T>([CanBeNull] this GuidWrapper<T> wrapper) where T : Object {
            return wrapper?.objRef as T;
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            GuidWrapper.SyncExt = (wrapper,type) => {
                var changed = false;
                
                // Sometimes when assets disappear then reappear, objRef will be a UnityEngine.Object instead
                // of the proper type. If this happens, we must throw it away, then restore from the ID.
                if (wrapper.objRef != null && wrapper.objRef.GetType() == typeof(Object)) {
                    wrapper.objRef = null;
                    changed = true;
                }

                if (wrapper.objRef != null) {
                    // Object is set and available
                    var newId = VrcfObjectId.ObjectToId(wrapper.objRef);
                    if (wrapper.id != newId) {
                        wrapper.id = newId;
                        changed = true;
                    }
                } else {
                    // Object is either:
                    // * missing (asset was deleted)
                    // * totally unset (user cleared the field or never put anything in it)
                    // * reference came from an old version of unity which stored "missing" as "unset" and we need to restore the reference from id
                    var newObjRef = VrcfObjectId.IdToObject<Object>(wrapper.id);
                    if (newObjRef != wrapper.objRef) {
                        wrapper.objRef = newObjRef;
                        changed = true;
                    }
                }

                if (wrapper.objRef != null && !type.IsInstanceOfType(wrapper.objRef)) {
                    // objRef is the wrong type somehow
                    wrapper.objRef = null;
                    wrapper.id = "";
                    changed = true;
                }

                return changed;
            };
        }
    }
}
