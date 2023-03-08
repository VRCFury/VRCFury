using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    public class Action {
        public virtual bool IsEmpty() {
            return false;
        }
    }
    
    [Serializable]
    public class ObjectToggleAction : Action {
        public GameObject obj;

        public override bool IsEmpty() {
            return obj == null || obj.CompareTag("EditorOnly");
        }
    }
    
    [Serializable]
    public class BlendShapeAction : Action {
        public string blendShape;
        public float blendShapeValue = 100;
    }
    
    [Serializable]
    public class MaterialAction : Action {
        public GameObject obj;
        public int materialIndex = 0;
        public GuidMaterial mat = null;
    }
    
    [Serializable]
    public class AnimationClipAction : Action {
        public GuidAnimationClip clip;
    }

    [Serializable]
    public class FlipbookAction : Action {
        public GameObject obj;
        public int frame;
    }
    
    [Serializable]
    public class ScaleAction : Action {
        public GameObject obj;
        public float scale = 1;
    }

}
