using System;
using UnityEngine;
using System.Collections.Generic;

namespace VRCF.Model {

[Serializable]
public class VRCFuryState {
    public AnimationClip clip;
    public List<VRCFuryAction> actions = new List<VRCFuryAction>();
    public bool isEmpty() {
        return clip == null && actions.Count == 0;
    }
}

}
