using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class SenkyFXState {
    public AnimationClip clip;
    public List<SenkyFXAction> actions = new List<SenkyFXAction>();
    public bool isEmpty() {
        return clip == null && actions.Count == 0;
    }
}
