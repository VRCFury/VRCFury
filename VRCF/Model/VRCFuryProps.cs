using System;
using UnityEngine;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCF.Model {

[Serializable]
public class VRCFuryProp {
    public const string TOGGLE = "toggle";
    public const string MODES = "modes";
    public const string PUPPET = "puppet";
    public const string CONTROLLER = "controller";

    public string type;
    public string name;
    public VRCFuryState state;
    public bool saved;
    public bool slider;
    public bool securityEnabled;
    public bool defaultOn;
    public List<VRCFuryPropPuppetStop> puppetStops = new List<VRCFuryPropPuppetStop>();
    public List<VRCFuryPropMode> modes = new List<VRCFuryPropMode>();
    public List<GameObject> resetPhysbones = new List<GameObject>();
    
    public RuntimeAnimatorController controller;
    public VRCExpressionsMenu controllerMenu;
    public VRCExpressionParameters controllerParams;

    public bool ResetMePlease;
}

[Serializable]
public class VRCFuryPropPuppetStop {
    public float x;
    public float y;
    public VRCFuryState state;
    public VRCFuryPropPuppetStop(float x, float y, VRCFuryState state) {
        this.x = x;
        this.y = y;
        this.state = state;
    }
}

[Serializable]
public class VRCFuryPropMode {
    public VRCFuryState state;
    public VRCFuryPropMode(VRCFuryState state) {
        this.state = state;
    }
}

[Serializable]
public class VRCFuryProps {
    public List<VRCFuryProp> props = new List<VRCFuryProp>();
}

}
