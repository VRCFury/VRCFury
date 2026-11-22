using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Actions {
    [FeatureTitle("Flipbook Builder")]
    internal class FlipBookBuilderActionBuilder : ActionBuilder<FlipBookBuilderAction> {
        [VFAutowired] [CanBeNull] private readonly ClipBuilderService clipBuilder;

        public VFClip Build(
            FlipBookBuilderAction model,
            VFGameObject animObject,
            ActionClipService actionClipService,
            (int first, int last) sliderNetworkCodes
        ) {
            var onClip = NewClip();
            var states = model.pages.Select(page => page.state).ToList();
            if (states.Count == 0) return onClip;
            var pageCount = states.Count;
            var (first, last) = sliderNetworkCodes;
            var snapPageTimes = last > 0;
            // A power-of-two duration preserves adjacent slider float values in clip time.
            var duration = snapPageTimes ? Mathf.NextPowerOfTwo(pageCount) : pageCount;
            var maxPages = last - first + 1;
            if (snapPageTimes && pageCount > maxPages) {
                throw new VRCFBuilderException($"Flipbook Builder has {pageCount} pages, but this slider can sync only {maxPages}.");
            }
            float GetPageTime(int page) {
                if (!snapPageTimes || page == 0) return page;
                // Switch at the rounding boundary just before the first network value on this page.
                var firstCode = System.Math.Max(first + page, (int)System.Math.Ceiling((double)page * last / pageCount));
                return VRCExpressionParameterExtensions.GetFloatNetworkThreshold(firstCode, last) * duration;
            }
            var sources = states
                .Select((substate,i) => {
                    var loaded = actionClipService.LoadStateAdv("tmp", substate, animObject);
                    return (GetPageTime(i), loaded.onClip.EvaluateMotion(1).FlattenToClip(VFMotionFlattenMode.DefaultVisibleClips));
                })
                .ToArray();

            if (clipBuilder != null) {
                var built = clipBuilder.MergeSingleFrameClips(sources);
                built.UpdateLengthIfLonger(duration);
                built.UseConstantTangents();
                onClip.CopyFrom(built);
            } else {
                // This is wrong, but it's fine because this branch is for debug info only
                foreach (var (time,source) in sources) {
                    onClip.CopyFrom(source);
                }
            }
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.Info(
                "This will create a clip made up of one frame per child action. This is mostly useful for" +
                " VRCFury Toggles with 'Use a Slider (Radial)' enabled, as you can put various presets in these slots" +
                " and use the slider to select one of them."
            ));
            output.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("pages")));
            return output;
        }

        [CustomPropertyDrawer(typeof(FlipBookBuilderAction.FlipBookPage))]
        public class FlipbookPageDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var content = new VisualElement();
                var match = Regex.Match(prop.propertyPath, @"\[(\d+)\]$");
                string pageNum;
                if (match.Success && int.TryParse(match.Groups[1].ToString(), out var num)) {
                    pageNum = (num + 1).ToString();
                } else {
                    pageNum = "?";
                }
                content.Add(new Label($"Page #{pageNum}").Bold());
                content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("state"), showDebugInfo: false));
                return content;
            }
        }
    }
}
