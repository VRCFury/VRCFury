using System;
using UnityEditor;
using UnityEngine;
using VRCF.Model.Feature;

namespace VRCF.Model {

[Serializable]
public class VRCFuryConfigUpgrader {

    public static VRCFuryConfig GetConfig(VRCFury script) {
        if (script.config == null) script.config = new VRCFuryConfig();
        Upgrade(script, script.config);
        return script.config;
    }

    public static void Upgrade(VRCFury script, VRCFuryConfig config) {
        if (config.version < 2) {
            UpgradeTo2(script, config);
            EditorUtility.SetDirty(script);
        }
    }

    private static void UpgradeTo2(VRCFury script, VRCFuryConfig config) {
        Debug.Log("Upgrading VRCFury config to version 2 ...");
        #pragma warning disable 0612
        if (StateExists(script.stateBlink)) {
            config.features.Add(new Blinking {
                state = script.stateBlink,
            });
        }
        if (script.viseme != null) {
            config.features.Add(new Visemes {
                oneAnim = script.viseme,
            });
        }
        if (script.scaleEnabled) {
            config.features.Add(new AvatarScale());
        }
        if (script.securityCodeLeft != 0 && script.securityCodeRight != 0) {
            config.features.Add(new SecurityLock {
                leftCode = script.securityCodeLeft,
                rightCode = script.securityCodeRight,
            });
        }
        if (script.breatheObject != null || !string.IsNullOrEmpty(script.breatheBlendshape)) {
            config.features.Add(new Breathing {
                obj = script.breatheObject,
                blendshape = script.breatheBlendshape,
                scaleMin = script.breatheScaleMin,
                scaleMax = script.breatheScaleMax,
            });
        }
        if (StateExists(script.stateToesDown) || StateExists(script.stateToesSplay) || StateExists(script.stateToesUp)) {
            config.features.Add(new Toes {
                down = script.stateToesDown,
                up = script.stateToesUp,
                splay = script.stateToesSplay,
            });
        }

        var enableGestures = StateExists(script.stateEyesClosed)
            || StateExists(script.stateEyesHappy)
            || StateExists(script.stateEyesSad)
            || StateExists(script.stateEyesAngry)
            || StateExists(script.stateMouthBlep)
            || StateExists(script.stateMouthSuck)
            || StateExists(script.stateMouthSad)
            || StateExists(script.stateMouthAngry)
            || StateExists(script.stateMouthHappy)
            || StateExists(script.stateEarsBack);
        if (enableGestures) {
            config.features.Add(new SenkyGestureDriver {
                eyesClosed = script.stateEyesClosed,
                eyesHappy = script.stateEyesHappy,
                eyesSad = script.stateEyesSad,
                eyesAngry = script.stateEyesAngry,
                mouthBlep = script.stateMouthBlep,
                mouthSuck = script.stateMouthSuck,
                mouthSad = script.stateMouthSad,
                mouthAngry = script.stateMouthAngry,
                mouthHappy = script.stateMouthHappy,
                earsBack = script.stateEarsBack,
            });
        }
        if (StateExists(script.stateTalking)) {
            config.features.Add(new Talking {
                state = script.stateTalking,
            });
        }
        if (script.props != null && script.props.props != null) {
            foreach (var prop in script.props.props) {
                if (prop.type == VRCFuryProp.CONTROLLER) {
                    config.features.Add(new FullController {
                        controller = prop.controller,
                        menu = prop.controllerMenu,
                        parameters = prop.controllerParams
                    });
                } else if (prop.type == VRCFuryProp.TOGGLE) {
                    config.features.Add(new Toggle {
                        name = prop.name,
                        state = prop.state,
                        saved = prop.saved,
                        slider = prop.slider,
                        securityEnabled = prop.securityEnabled,
                        defaultOn = prop.defaultOn,
                        resetPhysbones = prop.resetPhysbones
                    });
                } else if (prop.type == VRCFuryProp.MODES) {
                    config.features.Add(new Modes {
                        name = prop.name,
                        saved = prop.saved,
                        securityEnabled = prop.securityEnabled,
                        modes = prop.modes,
                        resetPhysbones = prop.resetPhysbones
                    });
                }
            }
        }
        script.stateBlink = null;
        script.viseme = null;
        script.scaleEnabled = false;
        script.securityCodeLeft = script.securityCodeRight = 0;
        script.breatheObject = null;
        script.breatheBlendshape = "";
        script.breatheScaleMin = 0;
        script.breatheScaleMax = 0;
        script.stateToesDown = script.stateToesUp = script.stateToesSplay = null;
        script.stateEyesClosed = script.stateEyesHappy = script.stateEyesSad = script.stateEyesAngry = null;
        script.stateMouthBlep = script.stateMouthSuck = script.stateMouthSad = script.stateMouthAngry = script.stateMouthHappy = null;
        script.stateEarsBack = script.stateTalking = null;
        script.props = null;
        config.version = 2;
        #pragma warning restore 0612
        Debug.Log("Upgrade complete, migrated " + config.features.Count + " features");
    }

    private static bool StateExists(VRCFuryState state) {
        return state != null && !state.isEmpty();
    }
}

}
