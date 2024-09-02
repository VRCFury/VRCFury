using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using VF.Component;
using VF.Upgradeable;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Model.StateAction {
    /**
     * Some Actions contain an implicit "resting state" which is applied to the avatar during the upload automatically.
     * For instance, if you have a Turn On action somewhere, the object will automatically be "turned off" during the upload.
     * However, if the action is annotated with this attribute, this behaviour will be skipped.
     */
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
    internal class DoNotApplyRestingStateAttribute : Attribute {
    }
    
    [Serializable]
    // Temporarily public for SPS Configurator
    public class Action : VrcfUpgradeable {
        public bool desktopActive = false;
        public bool androidActive = false;
    }

    [Serializable]
    internal class ObjectToggleAction : Action {
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
    internal class BlendShapeAction : Action {
        public string blendShape;
        public float blendShapeValue = 100;
        public Renderer renderer;
        public bool allRenderers = true;
    }
    
    [Serializable]
    internal class MaterialAction : Action {
        [Obsolete] public GameObject obj;
        public Renderer renderer;
        public int materialIndex = 0;
        public GuidMaterial mat = null;
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (obj != null) {
                    renderer = obj.GetComponent<Renderer>();
                }
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }

    [Serializable]
    internal class SpsOnAction : Action {
        public VRCFuryHapticPlug target;
    }
    
    [Serializable]
    internal class FxFloatAction : Action {
        public string name;
        public float value = 1;
    }
    
    [Serializable]
    internal class AnimationClipAction : Action {
        public GuidAnimationClip clip;
    }

    [Serializable]
    internal class ShaderInventoryAction : Action {
        public Renderer renderer;
        public int slot = 1;
    }

    [Serializable]
    internal class PoiyomiUVTileAction : Action {
        public Renderer renderer;
        public int row = 0;
        public int column = 0;
        public bool dissolve = false;
        public string renamedMaterial = "";
    }
    
    [Serializable]
    internal class MaterialPropertyAction : Action {
        [Obsolete] public Renderer renderer;
        public GameObject renderer2;
        public bool affectAllMeshes;
        public string propertyName;
        public float value;
        public Vector4 valueVector;
        public Color valueColor = Color.white;
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (renderer != null) {
                    renderer2 = renderer.gameObject;
                }
                renderer = null;
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
    
    [Serializable]
    internal class FlipbookAction : Action {
        [Obsolete] public GameObject obj;
        public Renderer renderer;
        public int frame;

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (obj != null) {
                    renderer = obj.GetComponent<Renderer>();
                }
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
    
    [Serializable]
    internal class ScaleAction : Action {
        public GameObject obj;
        public float scale = 1;
    }
    
    [Serializable]
    internal class BlockBlinkingAction : Action {
    }
        
    [Serializable]
    internal class BlockVisemesAction : Action {
    }
    
    [Serializable]
    internal class ResetPhysboneAction : Action {
        public VRCPhysBone physBone;
    }
    
    [Serializable]
    internal class FlipBookBuilderAction : Action {
        [Obsolete] public List<State> states;
        public List<FlipBookPage> pages;

        [Serializable]
        public class FlipBookPage {
            public State state;
        }

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                pages.Clear();
                foreach (var state in states) {
                    pages.Add(new FlipBookPage() { state = state });
                }
                states.Clear();
            }

            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }

    [Serializable]
    internal class SmoothLoopAction : Action {
        public State state1;
        public State state2;
        public float loopTime = 5;
    }

    
    [Serializable]
    [DoNotApplyRestingState]
    internal class SyncParamAction : Action {
        public string param;
        public float value = 0;
    }

    [Serializable]
    [DoNotApplyRestingState]
    internal class ToggleStateAction : Action {
        public string toggle;
        public float value = 0;
    }

    [Serializable]
    [DoNotApplyRestingState]
    internal class TagStateAction : Action {
        public string tag;
        public float value = 0;
    }
}
