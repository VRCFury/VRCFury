using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class SenkyFXProp {
    public const string TOGGLE = "toggle";
    public const string MODES = "modes";
    public const string PUPPET = "puppet";

    public string type;
    public string name;
    public SenkyFXState state;
    public bool saved;
    public bool slider;
    public bool lewdLocked;
    public bool defaultOn;
    public List<SenkyFXPropPuppetStop> puppetStops = new List<SenkyFXPropPuppetStop>();
    public List<SenkyFXPropMode> modes = new List<SenkyFXPropMode>();
    public List<GameObject> resetPhysbones = new List<GameObject>();

    public bool ResetMePlease;
}

[Serializable]
public class SenkyFXPropPuppetStop {
    public float x;
    public float y;
    public SenkyFXState state;
    public SenkyFXPropPuppetStop(float x, float y, SenkyFXState state) {
        this.x = x;
        this.y = y;
        this.state = state;
    }
}

[Serializable]
public class SenkyFXPropMode {
    public SenkyFXState state;
    public SenkyFXPropMode(SenkyFXState state) {
        this.state = state;
    }
}

[Serializable]
public class SenkyFXProps {
    public List<SenkyFXProp> props = new List<SenkyFXProp>();
}
