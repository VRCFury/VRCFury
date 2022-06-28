using System;
using UnityEngine;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCF.Model.Feature {

[Serializable]
public abstract class FeatureModel {}

[Serializable]
public class AvatarScale : FeatureModel {
}

[Serializable]
public class Blinking : FeatureModel {
    public VRCFuryState state;
}

[Serializable]
public class Breathing : FeatureModel {
    public GameObject obj;
    public string blendshape;
    public float scaleMin;
    public float scaleMax;
}

[Serializable]
public class FullController : FeatureModel {
    public RuntimeAnimatorController controller;
    public VRCExpressionsMenu menu;
    public VRCExpressionParameters parameters;
    [NonSerialized] public string submenu;
    [NonSerialized] public GameObject rootObj;
    [NonSerialized] public bool ignoreSaved;
}

[Serializable]
public class LegacyPrefabSupport : FeatureModel {
}

[Serializable]
public class Modes : FeatureModel {
    public string name;
    public bool saved;
    public bool securityEnabled;
    public List<VRCFuryPropMode> modes = new List<VRCFuryPropMode>();
    public List<GameObject> resetPhysbones = new List<GameObject>();
}

[Serializable]
public class Toggle : FeatureModel {
    public string name;
    public VRCFuryState state;
    public bool saved;
    public bool slider;
    public bool securityEnabled;
    public bool defaultOn;
    public List<GameObject> resetPhysbones = new List<GameObject>();
}

[Serializable]
public class Puppet : FeatureModel {
    public string name;
    public bool saved;
    public bool slider;
    public List<VRCFuryPropPuppetStop> stops = new List<VRCFuryPropPuppetStop>();
}

[Serializable]
public class SecurityLock : FeatureModel {
    public int leftCode;
    public int rightCode;
}

[Serializable]
public class SenkyGestureDriver : FeatureModel {
    public VRCFuryState eyesClosed;
    public VRCFuryState eyesHappy;
    public VRCFuryState eyesSad;
    public VRCFuryState eyesAngry;

    public VRCFuryState mouthBlep;
    public VRCFuryState mouthSuck;
    public VRCFuryState mouthSad;
    public VRCFuryState mouthAngry;
    public VRCFuryState mouthHappy;

    public VRCFuryState earsBack;
}

[Serializable]
public class Talking : FeatureModel {
    public VRCFuryState state;
}

[Serializable]
public class Toes : FeatureModel {
    public VRCFuryState down;
    public VRCFuryState up;
    public VRCFuryState splay;
}

[Serializable]
public class Visemes : FeatureModel {
    public AnimationClip oneAnim;
}

}
