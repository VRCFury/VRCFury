using System;
using UnityEngine;
using System.Collections.Generic;
using VRCF.Model.Feature;
using UnityEditor;

namespace VRCF.Model {

[Serializable]
public class VRCFuryConfig {
    public int version;
    [SerializeReference] public List<VRCF.Model.Feature.FeatureModel> features = new List<VRCF.Model.Feature.FeatureModel>();

    public void Upgrade(VRCFury owner) {
        if (version < 2) {
            UpgradeTo2(owner);
        }
    }

    private void UpgradeTo2(VRCFury owner) {
        Debug.Log("Upgrading VRCFury config to version 2 ...");
        #pragma warning disable 0612
        if (StateExists(owner.stateBlink)) {
            features.Add(new Blinking {
                state = owner.stateBlink,
            });
        }
        if (owner.viseme != null) {
            features.Add(new Visemes {
                oneAnim = owner.viseme,
            });
        }
        if (owner.scaleEnabled) {
            features.Add(new AvatarScale {
            });
        }
        if (owner.securityCodeLeft != 0 && owner.securityCodeRight != 0) {
            features.Add(new SecurityLock {
                leftCode = owner.securityCodeLeft,
                rightCode = owner.securityCodeRight,
            });
        }
        if (owner.breatheObject != null || !string.IsNullOrEmpty(owner.breatheBlendshape)) {
            features.Add(new Breathing {
                obj = owner.breatheObject,
                blendshape = owner.breatheBlendshape,
                scaleMin = owner.breatheScaleMin,
                scaleMax = owner.breatheScaleMax,
            });
        }
        if (StateExists(owner.stateToesDown) || StateExists(owner.stateToesSplay) || StateExists(owner.stateToesUp)) {
            features.Add(new Toes {
                down = owner.stateToesDown,
                up = owner.stateToesUp,
                splay = owner.stateToesSplay,
            });
        }

        var enableGestures = StateExists(owner.stateEyesClosed)
            || StateExists(owner.stateEyesHappy)
            || StateExists(owner.stateEyesSad)
            || StateExists(owner.stateEyesAngry)
            || StateExists(owner.stateMouthBlep)
            || StateExists(owner.stateMouthSuck)
            || StateExists(owner.stateMouthSad)
            || StateExists(owner.stateMouthAngry)
            || StateExists(owner.stateMouthHappy)
            || StateExists(owner.stateEarsBack);
        if (enableGestures) {
            features.Add(new SenkyGestureDriver {
                eyesClosed = owner.stateEyesClosed,
                eyesHappy = owner.stateEyesHappy,
                eyesSad = owner.stateEyesSad,
                eyesAngry = owner.stateEyesAngry,
                mouthBlep = owner.stateMouthBlep,
                mouthSuck = owner.stateMouthSuck,
                mouthSad = owner.stateMouthSad,
                mouthAngry = owner.stateMouthAngry,
                mouthHappy = owner.stateMouthHappy,
                earsBack = owner.stateEarsBack,
            });
        }
        if (StateExists(owner.stateTalking)) {
            features.Add(new Talking {
                state = owner.stateTalking,
            });
        }
        if (owner.props != null && owner.props.props != null) {
            foreach (var prop in owner.props.props) {
                if (prop.type == VRCFuryProp.CONTROLLER) {
                    features.Add(new FullController {
                        controller = prop.controller,
                        menu = prop.controllerMenu,
                        parameters = prop.controllerParams
                    });
                } else if (prop.type == VRCFuryProp.TOGGLE) {
                    features.Add(new Toggle {
                        name = prop.name,
                        state = prop.state,
                        saved = prop.saved,
                        slider = prop.slider,
                        securityEnabled = prop.securityEnabled,
                        defaultOn = prop.defaultOn,
                        resetPhysbones = prop.resetPhysbones
                    });
                } else if (prop.type == VRCFuryProp.MODES) {
                    features.Add(new Modes {
                        name = prop.name,
                        saved = prop.saved,
                        securityEnabled = prop.securityEnabled,
                        modes = prop.modes,
                        resetPhysbones = prop.resetPhysbones
                    });
                }
            }
        }
        owner.stateBlink = null;
        owner.viseme = null;
        owner.scaleEnabled = false;
        owner.securityCodeLeft = owner.securityCodeRight = 0;
        owner.breatheObject = null;
        owner.breatheBlendshape = "";
        owner.breatheScaleMin = 0;
        owner.breatheScaleMax = 0;
        owner.stateToesDown = owner.stateToesUp = owner.stateToesSplay = null;
        owner.stateEyesClosed = owner.stateEyesHappy = owner.stateEyesSad = owner.stateEyesAngry = null;
        owner.stateMouthBlep = owner.stateMouthSuck = owner.stateMouthSad = owner.stateMouthAngry = owner.stateMouthHappy = null;
        owner.stateEarsBack = owner.stateTalking = null;
        owner.props = null;
        version = 2;
        EditorUtility.SetDirty(owner);
        #pragma warning restore 0612
        Debug.Log("Upgrade complete, migrated " + features.Count + " features");
    }

    private static bool StateExists(VRCFuryState state) {
        return state != null && !state.isEmpty();
    }
}

}
