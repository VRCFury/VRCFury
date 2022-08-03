using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VRCF.Model;

namespace VRCF.Legacy {

    [CustomEditor(typeof(VRCFuryLegacy), true)]
    public class VRCFuryLegacyEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            return new Button(() => { Upgrade((VRCFuryLegacy)target); }) {
                text = "Click here to upgrade from legacy VRCFury"
            };
        }

        private static void Upgrade(VRCFuryLegacy oldConfig) {
            while (PrefabUtility.IsPartOfPrefabInstance(oldConfig)) {
                var parent = PrefabUtility.GetCorrespondingObjectFromSource(oldConfig);
                if (parent == null) break;
                oldConfig = PrefabUtility.GetCorrespondingObjectFromSource(oldConfig);
            }

            var gameObject = oldConfig.gameObject;
            Debug.Log("Upgrading VRCFury model to version 2: " + oldConfig + " " + AssetDatabase.GetAssetPath(oldConfig));

            var newModel = gameObject.GetComponent<VRCFury>();
            if (newModel == null) {
                newModel = gameObject.AddComponent<VRCFury>();
            }

            var newConfig = newModel.config;

#pragma warning disable 0612
            if (StateExists(oldConfig.stateBlink)) {
                newConfig.features.Add(new VF.Model.Feature.Blinking {
                    state = oldConfig.stateBlink.Upgrade(),
                });
            }

            if (oldConfig.viseme != null) {
                newConfig.features.Add(new VF.Model.Feature.Visemes());
            }

            if (oldConfig.scaleEnabled) {
                newConfig.features.Add(new VF.Model.Feature.AvatarScale());
            }

            if (oldConfig.securityCodeLeft != 0 && oldConfig.securityCodeRight != 0) {
                newConfig.features.Add(new VF.Model.Feature.SecurityLock {
                });
            }

            if (oldConfig.breatheObject != null || !string.IsNullOrEmpty(oldConfig.breatheBlendshape)) {
                newConfig.features.Add(new VF.Model.Feature.Breathing {
                    obj = oldConfig.breatheObject,
                    blendshape = oldConfig.breatheBlendshape,
                    scaleMin = oldConfig.breatheScaleMin,
                    scaleMax = oldConfig.breatheScaleMax,
                });
            }

            if (StateExists(oldConfig.stateToesDown) || StateExists(oldConfig.stateToesSplay) ||
                StateExists(oldConfig.stateToesUp)) {
                newConfig.features.Add(new VF.Model.Feature.Toes {
                    down = oldConfig.stateToesDown.Upgrade(),
                    up = oldConfig.stateToesUp.Upgrade(),
                    splay = oldConfig.stateToesSplay.Upgrade(),
                });
            }

            var enableGestures = StateExists(oldConfig.stateEyesClosed)
                                 || StateExists(oldConfig.stateEyesHappy)
                                 || StateExists(oldConfig.stateEyesSad)
                                 || StateExists(oldConfig.stateEyesAngry)
                                 || StateExists(oldConfig.stateMouthBlep)
                                 || StateExists(oldConfig.stateMouthSuck)
                                 || StateExists(oldConfig.stateMouthSad)
                                 || StateExists(oldConfig.stateMouthAngry)
                                 || StateExists(oldConfig.stateMouthHappy)
                                 || StateExists(oldConfig.stateEarsBack);
            if (enableGestures) {
                newConfig.features.Add(new VF.Model.Feature.SenkyGestureDriver {
                    eyesClosed = oldConfig.stateEyesClosed.Upgrade(),
                    eyesHappy = oldConfig.stateEyesHappy.Upgrade(),
                    eyesSad = oldConfig.stateEyesSad.Upgrade(),
                    eyesAngry = oldConfig.stateEyesAngry.Upgrade(),
                    mouthBlep = oldConfig.stateMouthBlep.Upgrade(),
                    mouthSuck = oldConfig.stateMouthSuck.Upgrade(),
                    mouthSad = oldConfig.stateMouthSad.Upgrade(),
                    mouthAngry = oldConfig.stateMouthAngry.Upgrade(),
                    mouthHappy = oldConfig.stateMouthHappy.Upgrade(),
                    earsBack = oldConfig.stateEarsBack.Upgrade(),
                });
            }

            if (StateExists(oldConfig.stateTalking)) {
                newConfig.features.Add(new VF.Model.Feature.Talking {
                    state = oldConfig.stateTalking.Upgrade(),
                });
            }

            if (oldConfig.props != null && oldConfig.props.props != null) {
                foreach (var prop in oldConfig.props.props) {
                    if (prop.type == VRCFuryProp.CONTROLLER) {
                        newConfig.features.Add(new VF.Model.Feature.FullController {
                            controller = prop.controller,
                            menu = prop.controllerMenu,
                            parameters = prop.controllerParams
                        });
                    } else if (prop.type == VRCFuryProp.TOGGLE) {
                        newConfig.features.Add(new VF.Model.Feature.Toggle {
                            name = prop.name,
                            state = prop.state.Upgrade(),
                            saved = prop.saved,
                            slider = prop.slider,
                            securityEnabled = prop.securityEnabled,
                            defaultOn = prop.defaultOn,
                            resetPhysbones = prop.resetPhysbones
                        });
                    } else if (prop.type == VRCFuryProp.MODES) {
                        newConfig.features.Add(new VF.Model.Feature.Modes {
                            name = prop.name,
                            saved = prop.saved,
                            securityEnabled = prop.securityEnabled,
                            modes = VRCF.Model.Feature.Modes.UpgradeModes(prop.modes),
                            resetPhysbones = prop.resetPhysbones
                        });
                    }
                }
            }

            if (oldConfig.config != null && oldConfig.config.features != null) {
                foreach (var oldFeature in oldConfig.config.features) {
                    if (oldFeature != null) newConfig.features.Add(oldFeature.Upgrade());
                }
            }
#pragma warning restore 0612
            Debug.Log("Upgrade complete, migrated " + newConfig.features.Count + " features");

            DestroyImmediate(oldConfig, true);
            EditorUtility.SetDirty(gameObject);
            AssetDatabase.SaveAssets();
        }

        private static bool StateExists(VRCFuryState state) {
            return state != null && !state.isEmpty();
        }
    }

}
