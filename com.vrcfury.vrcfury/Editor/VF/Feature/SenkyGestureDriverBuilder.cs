using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class SenkyGestureDriverBuilder : FeatureBuilder<SenkyGestureDriver> {
    [FeatureBuilderAction(FeatureOrder.SenkyGestureDriver)]
    public void Apply() {
        var feature = new GestureDriver {
            gestures = {
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.THUMBSUP,
                    state = model.eyesHappy,
                    disableBlinking = true,
                    lockMenuItem = "Emote Lock/Happy",
                    exclusiveTag = "eyes",
                },
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.HANDGUN,
                    state = model.eyesSad,
                    disableBlinking = true,
                    lockMenuItem = "Emote Lock/Sad",
                    exclusiveTag = "eyes",
                },
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.ROCKNROLL,
                    state = model.eyesAngry,
                    disableBlinking = true,
                    lockMenuItem = "Emote Lock/Angry",
                    exclusiveTag = "eyes",
                },

                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.VICTORY,
                    state = model.mouthBlep,
                    lockMenuItem = "Emote Lock/Tongue",
                    exclusiveTag = "mouth",
                },
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.THUMBSUP,
                    state = model.mouthHappy,
                    lockMenuItem = "Emote Lock/Happy",
                    exclusiveTag = "mouth",
                },
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.HANDGUN,
                    state = model.mouthSad,
                    lockMenuItem = "Emote Lock/Sad",
                    exclusiveTag = "mouth",
                },
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.ROCKNROLL,
                    state = model.mouthAngry,
                    lockMenuItem = "Emote Lock/Angry",
                    exclusiveTag = "mouth",
                },
                
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.HANDGUN,
                    state = model.earsBack,
                    lockMenuItem = "Emote Lock/Sad",
                    exclusiveTag = "ears",
                },
                new GestureDriver.Gesture {
                    sign = GestureDriver.HandSign.ROCKNROLL,
                    state = model.earsBack,
                    lockMenuItem = "Emote Lock/Angry",
                    exclusiveTag = "ears",
                },
            }
        };

        foreach (var gesture in feature.gestures) {
            gesture.hand = GestureDriver.Hand.EITHER;
            gesture.enableLockMenuItem = true;
            gesture.enableExclusiveTag = true;
            gesture.customTransitionTime = model.transitionTime >= 0;
            gesture.transitionTime = model.transitionTime;
        }

        addOtherFeature(feature);
    }

    public override string GetEditorTitle() {
        return "Senky Gesture Driver";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        
        content.Add(VRCFuryEditorUtils.Info("This feature is designed to be used by Senky only. Implementation may change at any time. If you are not Senky, please use the 'Gesture' feature instead."));

        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("eyesClosed"), "Eyes Closed"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("eyesHappy"), "Eyes Happy"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("eyesSad"), "Eyes Sad"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("eyesAngry"), "Eyes Angry"));

        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("mouthBlep"), "Mouth Blep"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("mouthSuck"), "Mouth Suck"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("mouthSad"), "Mouth Sad"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("mouthAngry"), "Mouth Angry"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("mouthHappy"), "Mouth Happy"));

        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("earsBack"), "Ears Back"));
        
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionTime"), "Transition Time (in seconds, -1 will use VRCFury recommended value)"));
        content.Add(adv);

        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
