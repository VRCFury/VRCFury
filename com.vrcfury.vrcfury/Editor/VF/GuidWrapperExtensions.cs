using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Model;
using VF.Utils;

namespace VF {
    [InitializeOnLoad]
    public static class GuidWrapperExtensions {
        [CanBeNull]
        public static T Get<T>(this GuidWrapper<T> wrapper) where T : Object {
            if (wrapper == null) return null;
            if (wrapper.objRef is T t) return t;
            return VrcfObjectId.IdToObject<T>(wrapper.id);
        }

        static GuidWrapperExtensions() {
            GuidWrapper.SyncExt = (wrapper,type) => {
                var changed = false;
                
                // Sometimes when assets disappear then reappear, objRef will be a UnityEngine.Object instead
                // of the proper type. If this happens, we must throw it away, then restore from the ID.
                if (wrapper.objRef != null && wrapper.objRef.GetType() == typeof(Object)) {
                    wrapper.objRef = null;
                    changed = true;
                }

                if (wrapper.objRef != null) {
                    var newId = VrcfObjectId.ObjectToId(wrapper.objRef);
                    if (wrapper.id != newId) {
                        wrapper.id = newId;
                        changed = true;
                    }
                } else {
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
