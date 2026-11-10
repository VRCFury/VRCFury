using UnityEditor;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Actions {
    [FeatureTitle("Change SPS Channels")]
    internal class ChangeSpsChannelsActionBuilder : ActionBuilder<ChangeSpsChannelsAction> {
        [VFAutowired] [CanBeNull] private readonly ChangeSpsChannelsService channels;

        public VFClip Build(ChangeSpsChannelsAction model, bool debugMode) {
            return debugMode || channels == null || !BuildTargetUtils.IsDesktop() ? NewClip() : channels.Request(model);
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            var target = prop.FindPropertyRelative("target");
            content.Add(VRCFuryEditorUtils.Prop(target, "Socket / Plug"));
            content.Add(VRCFuryEditorUtils.Warn("Adds several more contacts: a separate set of senders and receivers is needed for these channels."));
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var obj = target.objectReferenceValue as UnityEngine.GameObject;
                if (obj == null || (obj.GetComponent<VRCFuryHapticPlug>() != null ? 1 : 0)
                    + (obj.GetComponent<VRCFuryHapticSocket>() != null ? 1 : 0) != 1)
                    return VRCFuryEditorUtils.Info("Select an object with exactly one SPS socket or plug.");
                return VRCFuryHapticPlugEditor.SpsChannelsEditor(
                    obj, obj.GetComponent<VRCFuryHapticPlug>() != null,
                    prop.FindPropertyRelative("channelSettings"));
            }, target));
            return content;
        }
    }
}
