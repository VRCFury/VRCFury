using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature {

    [FeatureTitle("When-Talking State")]
    internal class TalkingBuilder : FeatureBuilder<Talking> {
        [VFAutowired] private readonly ActionClipService actionClipService;

        [FeatureBuilderAction]
        public void Apply() {
            var fx = GetFx();
            var layer = fx.NewLayer("Talk Glow");
            var clip = actionClipService.LoadState("TalkGlow", model.state);
            var off = layer.NewState("Off");
            var on = layer.NewState("On").WithAnimation(clip);

            off.TransitionsTo(on).When(fx.Viseme().IsGreaterThan(9));
            on.TransitionsTo(off).When(fx.Viseme().IsLessThan(10));
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will activate the given animation whenever the avatar is talking."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state")));
            return content;
        }
    }

}
