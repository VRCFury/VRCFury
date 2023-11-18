using System;
using UnityEngine;
using UnityEngine.Serialization;
using VF.Component;
using VF.Upgradeable;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Model.StateAction {
    [Serializable]
    public class Action : VrcfUpgradeable {
        public bool desktopActive = false;
        public bool androidActive = false;
    }

    [Serializable]
    public class ObjectToggleAction : Action {
        public GameObject obj;
        public Mode mode = Mode.TurnOn;

        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                mode = Mode.Toggle;
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 1;
        }

        public enum Mode {
            TurnOn,
            TurnOff,
            Toggle
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
    public class SpsOnAction : Action {
        public VRCFuryHapticPlug target;
    }
    
    [Serializable]
    public class FxFloatAction : Action {
        public string name;
        public float value = 1;
    }
    
    [Serializable]
    public class AnimationClipAction : Action {
        public GuidAnimationClip clip;
    }

    [Serializable]
    public class ShaderInventoryAction : Action {
        public Renderer renderer;
        public int slot = 1;
    }

    [Serializable]
    public class PoiyomiUVTileAction : Action {
        public Renderer renderer;
        public int row = 0;
        public int column = 0;
        public bool dissolve = false;
        public string renamedMaterial = "";
    }
    
    [Serializable]
    public class MaterialPropertyAction : Action {
        public Renderer renderer;
        public bool affectAllMeshes;
        public string propertyName;
        public float value;
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
    
    [Serializable]
    public class BlockBlinkingAction : Action {
    }
    
    [Serializable]
    public class ResetPhysboneAction : Action {
        public VRCPhysBone physBone;
    }

    [Serializable]
    public class SetGlobalParamAction: Action {
        public string param;
        public string value;
    }

}
