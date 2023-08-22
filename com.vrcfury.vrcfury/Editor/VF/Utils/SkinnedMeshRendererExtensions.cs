using System;
using UnityEngine;

namespace VF.Utils {
    public static class SkinnedMeshRendererExtensions {
        public static float GetBlendShapeWeight(this SkinnedMeshRenderer skin, string name) {
            var id = skin.sharedMesh.GetBlendShapeIndex(name);
            if (id < 0) throw new Exception("Blendshape does not exist");
            return skin.GetBlendShapeWeight(id);
        }
        
        public static void SetBlendShapeWeight(this SkinnedMeshRenderer skin, string name, float value) {
            var id = skin.sharedMesh.GetBlendShapeIndex(name);
            if (id < 0) throw new Exception("Blendshape does not exist");
            skin.SetBlendShapeWeight(id, value);
        }
    }
}
