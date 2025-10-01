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

            foreach(var toggle in allToggles)
            {
                bool handled = false;

                foreach (string toggle_name in model.toggleOn)
                {
                    if (toggle_name == toggle.model.name)
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
                    if (toggle_name == toggle.model.name)
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
            content.Add(VRCFuryEditorUtils.Prop(toggleOn).FlexGrow(1));

            var toggleOff = prop.FindPropertyRelative("toggleOff");
            content.Add(VRCFuryEditorUtils.Prop(toggleOff).FlexGrow(1));

            var allOff = prop.FindPropertyRelative("allOff");
            content.Add(VRCFuryEditorUtils.Prop(allOff, "Turn everything else off"));

            return content;
        }
    }

}
