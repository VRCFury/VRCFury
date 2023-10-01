using UnityEditor;
using UnityEngine;
using VF.Model;
using VF.Utils;

namespace VF {
    [InitializeOnLoad]
    public static class GuidWrapperExtensions {
        public static T Get<T>(this GuidWrapper<T> wrapper) where T : Object {
            if (wrapper == null) return null;
            if (wrapper.objOverride != null) return wrapper.objOverride;
            return VrcfObjectId.IdToObject<T>(wrapper.id);
        }

        static GuidWrapperExtensions() {
            GuidWrapper.UpdateIdIfPossible = wrapper => {
                var changed = false;
                
                var obj = VrcfObjectId.IdToObject<Object>(wrapper.id);
                if (obj != null) {
                    if (obj != wrapper.objRef) {
                        wrapper.objRef = obj;
                        changed = true;
                    }
                    var newId = VrcfObjectId.ObjectToId(obj);
                    if (wrapper.id != newId) {
                        wrapper.id = newId;
                        changed = true;
                    }
                }

                return changed;
            };
        }
    }
}
