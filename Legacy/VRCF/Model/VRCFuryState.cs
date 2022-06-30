using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;
using VF.Model.StateAction;

namespace VRCF.Model {

[Serializable]
public class VRCFuryState {
    public AnimationClip clip;
    public List<VRCFuryAction> actions = new List<VRCFuryAction>();
    public bool isEmpty() {
        return clip == null && actions.Count == 0;
    }

    public State Upgrade() {
        var newState = new State();
        if (clip) {
            newState.actions.Add(new AnimationClipAction { clip = clip });
        }

        foreach (var oldAction in actions) {
            switch (oldAction.type) {
                case VRCFuryAction.TOGGLE:
                    newState.actions.Add(new ObjectToggleAction { obj = oldAction.obj });
                    break;
                case VRCFuryAction.BLENDSHAPE:
                    newState.actions.Add(new BlendShapeAction { blendShape = oldAction.blendShape });
                    break;
            }
        }

        return newState;
    }
}

}
