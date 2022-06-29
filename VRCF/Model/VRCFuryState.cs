using System;
using System.Collections.Generic;
using UnityEngine;

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
