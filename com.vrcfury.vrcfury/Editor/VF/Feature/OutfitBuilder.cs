using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature
{

    [FeatureTitle("Outfit")]
    [FeatureRootOnly]
    internal class OutfitBuilder : FeatureBuilder<Outfit>
    {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

        public const string togglePathTooltip =
            "Menu path to other toggles.\n\n" +
            "Supports wildcards:\n" +
            "  *  matches any sequence of characters (including none)\n" +
            "  ?  matches exactly one character\n\n" +
            "Examples:\n" +
            "  All accessories:             Accessories/Jewelery/*\n" +
            "  All clothing with 'Party':   Clothing/*Party";
        [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
        public void Apply()
        {
            var allToggles = globals.allBuildersInRun
                .OfType<ToggleBuilder>()
                .ToArray();

            var param = fx.NewBool(model.name, false, false, false, false, true);

            menu.NewMenuButton(
                model.name,
                param,
                icon: null
            );

            var layerName = model.name;
            if (string.IsNullOrEmpty(layerName)) layerName = "Outfit";
            var layer = fx.NewLayer(layerName);

            VFCondition onCase;
            onCase = param.IsTrue();

            var off = layer.NewState("Off");
            var on = layer.NewState("On");

            off.TransitionsTo(on).When(onCase);
            on.TransitionsToExit().When(onCase.Not());

            foreach (var toggle in allToggles)
            {
                bool handled = false;

                foreach (string toggle_name in model.toggleOn)
                {
                    if (WildcardMatch(toggle_name, toggle.model.name))
                    {
                        toggle.drive(on, true);
                        handled = true;
                    }
                }

                if (handled) continue;

                if (model.allOff)
                {
                    toggle.drive(on, false);
                    continue;
                }

                foreach (string toggle_name in model.toggleOff)
                {
                    if (WildcardMatch(toggle_name, toggle.model.name))
                    {
                        toggle.drive(on, false);
                    }
                }

            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject, VFGameObject componentObject, Outfit model)
        {
            var content = new VisualElement();

            var flex = new VisualElement().Row();
            content.Add(flex);

            var pathProp = prop.FindPropertyRelative("name");
            flex.Add(VRCFuryEditorUtils.Prop(pathProp, "Menu Path", tooltip: ToggleBuilder.menuPathTooltip).FlexGrow(1));

            var c = new VisualElement();

            var toggleOn = prop.FindPropertyRelative("toggleOn");
            content.Add(VRCFuryEditorUtils.Prop(toggleOn, tooltip: togglePathTooltip).FlexGrow(1));

            var toggleOff = prop.FindPropertyRelative("toggleOff");
            content.Add(VRCFuryEditorUtils.Prop(toggleOff, tooltip: togglePathTooltip).FlexGrow(1));

            var allOff = prop.FindPropertyRelative("allOff");
            content.Add(VRCFuryEditorUtils.Prop(allOff, "Turn everything else off"));

            return content;
        }

        public static bool WildcardMatch(string pattern, string input)
        {
            int p = 0, s = 0;
            int starIdx = -1, match = 0;

            while (s < input.Length)
            {
                if (p < pattern.Length &&
                    (pattern[p] == '?' || pattern[p] == input[s]))
                {
                    // Characters match, or '?' matches any one character
                    p++;
                    s++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    // Record position of '*' and the position in the input
                    starIdx = p;
                    match = s;
                    p++;
                }
                else if (starIdx != -1)
                {
                    // Backtrack: last pattern char was '*'
                    p = starIdx + 1;
                    match++;
                    s = match;
                }
                else
                {
                    return false;
                }
            }

            // Skip any remaining '*' at the end of the pattern
            while (p < pattern.Length && pattern[p] == '*')
                p++;

            return p == pattern.Length;
        }
    }
}
