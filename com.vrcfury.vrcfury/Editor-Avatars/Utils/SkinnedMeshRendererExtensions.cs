using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    internal static class SkinnedMeshRendererExtensions {
        public static float GetBlendShapeWeight(this SkinnedMeshRenderer skin, string name) {
            var id = skin.GetBlendShapeIndex(name);
            if (id < 0) throw new Exception("Blendshape does not exist");
            return skin.GetBlendShapeWeight(id);
        }
        
        public static void SetBlendShapeWeight(this SkinnedMeshRenderer skin, string name, float value) {
            var id = skin.GetBlendShapeIndex(name);
            if (id < 0) throw new Exception("Blendshape does not exist");
            skin.SetBlendShapeWeight(id, value);
        }
    }
}
