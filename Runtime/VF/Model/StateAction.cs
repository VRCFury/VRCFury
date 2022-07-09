using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    public class Action {
    }
    
    [Serializable]
    public class ObjectToggleAction : Action {
        public GameObject obj;
    }
    
    [Serializable]
    public class BlendShapeAction : Action {
        public string blendShape;
    }
    
    [Serializable]
    public class AnimationClipAction : Action {
        public AnimationClip clip;
    }

    [Serializable]
    public class FlipbookAction : Action {
        public GameObject obj;
        public int frame;
    }

}
