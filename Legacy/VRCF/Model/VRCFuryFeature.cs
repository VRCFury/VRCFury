using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCF.Model.Feature {

    [Serializable]
    public abstract class FeatureModel {
        public abstract VF.Model.Feature.FeatureModel Upgrade();
    }

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class AvatarScale : FeatureModel {
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.AvatarScale();
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Blinking : FeatureModel {
    public VRCFuryState state;
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Blinking {
            state = state.Upgrade()
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Breathing : FeatureModel {
    public GameObject obj;
    public string blendshape;
    public float scaleMin;
    public float scaleMax;
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Breathing {
            obj = obj,
            blendshape = blendshape,
            scaleMin = scaleMin,
            scaleMax = scaleMax
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class FullController : FeatureModel {
    public RuntimeAnimatorController controller;
    public VRCExpressionsMenu menu;
    public VRCExpressionParameters parameters;
    [NonSerialized] public string submenu;
    [NonSerialized] public GameObject rootObj;
    [NonSerialized] public bool ignoreSaved;
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.FullController {
            controller = controller,
            menu = menu,
            parameters = parameters,
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class LegacyPrefabSupport : FeatureModel {
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.LegacyPrefabSupport();
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Modes : FeatureModel {
    public string name;
    public bool saved;
    public bool securityEnabled;
    public List<VRCFuryPropMode> modes = new List<VRCFuryPropMode>();
    public List<GameObject> resetPhysbones = new List<GameObject>();
    
    public static List<VF.Model.Feature.Modes.Mode> UpgradeModes(List<VRCFuryPropMode> oldModes) {
        var newModes = new List<VF.Model.Feature.Modes.Mode>();
        foreach (var oldMode in oldModes) {
            newModes.Add(new VF.Model.Feature.Modes.Mode(oldMode.state.Upgrade()));
        }
        return newModes;
    }
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Modes {
            name = name,
            saved = saved,
            securityEnabled = securityEnabled,
            modes = UpgradeModes(modes),
            resetPhysbones = resetPhysbones
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Toggle : FeatureModel {
    public string name;
    public VRCFuryState state;
    public bool saved;
    public bool slider;
    public bool securityEnabled;
    public bool defaultOn;
    public List<GameObject> resetPhysbones = new List<GameObject>();
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Toggle {
            name = name,
            state = state.Upgrade(),
            saved = saved,
            slider = slider,
            securityEnabled = securityEnabled,
            defaultOn = defaultOn,
            resetPhysbones = resetPhysbones
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Puppet : FeatureModel {
    public string name;
    public bool saved;
    public bool slider;
    public List<VRCFuryPropPuppetStop> stops = new List<VRCFuryPropPuppetStop>();

    public static List<VF.Model.Feature.Puppet.Stop> UpgradeStops(List<VRCFuryPropPuppetStop> oldStops) {
        var newStops = new List<VF.Model.Feature.Puppet.Stop>();
        foreach (var oldStop in oldStops) {
            newStops.Add(new VF.Model.Feature.Puppet.Stop(oldStop.x, oldStop.y, oldStop.state.Upgrade()));
        }
        return newStops;
    }
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Puppet {
            name = name,
            saved = saved,
            slider = slider,
            stops = UpgradeStops(stops)
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class SecurityLock : FeatureModel {
    public int leftCode;
    public int rightCode;
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.SecurityLock {
            leftCode = leftCode,
            rightCode = rightCode,
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
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
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.SenkyGestureDriver {
            eyesClosed = eyesClosed.Upgrade(),
            eyesHappy = eyesHappy.Upgrade(),
            eyesSad = eyesSad.Upgrade(),
            eyesAngry = eyesAngry.Upgrade(),
            mouthBlep = mouthBlep.Upgrade(),
            mouthSuck = mouthSuck.Upgrade(),
            mouthSad = mouthSad.Upgrade(),
            mouthAngry = mouthAngry.Upgrade(),
            mouthHappy = mouthHappy.Upgrade(),
            earsBack = earsBack.Upgrade(),
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Talking : FeatureModel {
    public VRCFuryState state;
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Talking {
            state = state.Upgrade(),
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Toes : FeatureModel {
    public VRCFuryState down;
    public VRCFuryState up;
    public VRCFuryState splay;
    
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Toes {
            down = down.Upgrade(),
            up = up.Upgrade(),
            splay = splay.Upgrade(),
        };
    }
}

[MovedFrom(false, sourceAssembly: "Assembly-CSharp-firstpass")]
[Serializable]
public class Visemes : FeatureModel {
    public AnimationClip oneAnim;
    public override VF.Model.Feature.FeatureModel Upgrade() {
        return new VF.Model.Feature.Visemes {
            oneAnim = oneAnim,
        };
    }
}

}
