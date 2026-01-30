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
using UnityEngine;
using System.Collections.Generic;
using System;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature
{

    [FeatureTitle("Outfit")]
    internal class OutfitBuilder : FeatureBuilder<Outfit>
    {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

        public const string togglePathTooltip =
            "\n\nSupports wildcards:\n" +
            "  *  matches any sequence of characters (including none)\n" +
            "  ?  matches exactly one character\n\n" +
            "Examples:\n" +
            "  All accessories: Accessories/*\n" +
            "  All items in Clothing with 'Party' at the end: Clothing/*Party" +
            "  Everything: *";

        [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
        public void Apply()
        {
            var allToggles = globals.allBuildersInRun
                .OfType<ToggleBuilder>()
                .ToArray();

            var param = fx.NewBool(model.name, true, false, false, false, true);

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

                // Toggle on has priority over off
                foreach (string toggle_name in model.toggleOn)
                {
                    if (WildcardMatch(toggle_name, toggle.model.name))
                    {
                        toggle.drive(on, true);
                        handled = true;
                    }
                }

                if (handled) continue;

                // Toggle off
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

            content.Add(VRCFuryEditorUtils.Info("This feature will add a menu item that will toggle on or off a selection of other toggles on the avatar."));

            var pathProp = prop.FindPropertyRelative("name");
            content.Add(VRCFuryEditorUtils.Prop(pathProp, "Menu Path", tooltip: ToggleBuilder.menuPathTooltip).FlexGrow(1));

            var toggleOnProp = prop.FindPropertyRelative("toggleOn");
            var sectionOn = VRCFuryEditorUtils.Section();
            sectionOn.Add(ToggleMenuPathListDrawer.BuildList(toggleOnProp, avatarObject, "Turn On", "These toggles will be enabled." + togglePathTooltip));
            content.Add(sectionOn);

            var toggleOffProp = prop.FindPropertyRelative("toggleOff");
            var sectionOff = VRCFuryEditorUtils.Section();
            sectionOff.Add(ToggleMenuPathListDrawer.BuildList(toggleOffProp, avatarObject, "Turn Off", "These toggles will be disabled." + togglePathTooltip));
            content.Add(sectionOff);

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

    internal static class ToggleMenuPathListDrawer
    {
        public static VisualElement BuildList(
            SerializedProperty listProp,
            VFGameObject avatarObject,
            string label,
            string tooltip = null
        )
        {
            var container = new VisualElement();
            container.Add(new Label(label) { tooltip = tooltip, style = { unityFontStyleAndWeight = FontStyle.Bold } });

            void RefreshList()
            {
                container.Clear();
                var (labelBox, tooltipBox) = VRCFuryEditorUtils.CreateTooltip(label, tooltip);
                container.Add(labelBox);
                container.Add(tooltipBox);

                for (int i = 0; i < listProp.arraySize; i++)
                {
                    int index = i;
                    var row = new VisualElement().Row();

                    var itemProp = listProp.GetArrayElementAtIndex(index);

                    // TextField for manual edit
                    var textField = new TextField { value = itemProp.stringValue };
                    textField.style.flexGrow = 1;
                    textField.style.width = 100;
                    textField.RegisterValueChangedCallback(e => {
                        itemProp.stringValue = e.newValue;
                        itemProp.serializedObject.ApplyModifiedProperties();
                        RefreshList();
                    });
                    row.Add(textField);

                    // Search button
                    var searchButton = new Button(() => {
                        SelectButton(
                            avatarObject,
                            foldersOnly: false,
                            prop: itemProp,
                            label: null,
                            immediate: true,
                            onComplete: RefreshList
                        );
                    })
                    { text = "Select" };
                    row.Add(searchButton);

                    // Remove button
                    var removeButton = new Button(() => {
                        listProp.DeleteArrayElementAtIndex(index);
                        listProp.serializedObject.ApplyModifiedProperties();
                        RefreshList();
                    })
                    { text = "X" };
                    row.Add(removeButton);

                    container.Add(row);
                }

                // Add button
                var addButton = new Button(() => {
                    listProp.arraySize++;
                    listProp.serializedObject.ApplyModifiedProperties();
                    RefreshList();
                })
                { text = "Add" };
                container.Add(addButton);
            }

            // Initial build
            RefreshList();

            return container;
        }

        // Pulled from MoveMenuItemBuilder
        // Added a callback on completion to update the list
        public static VisualElement SelectButton(
            VFGameObject avatarObject,
            bool foldersOnly,
            SerializedProperty prop,
            string label = "Menu Path",
            Func<string> append = null,
            string selectLabel = "Select",
            string tooltip = null,
            bool immediate = false,
            Vector2? pos = null,
            Action onComplete = null
        )
        {
            void Apply(string path)
            {
                if (append != null)
                {
                    if (path != "") path += "/";
                    path += append();
                }
                prop.stringValue = path;
                prop.serializedObject.ApplyModifiedProperties();

                onComplete?.Invoke();
            }

            void OnClick()
            {
                if (avatarObject == null) return;

                var controlPaths = new List<IList<string>>();
                MenuEstimator.Estimate(avatarObject).GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                    if (!foldersOnly || control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        controlPaths.Add(path);
                    }

                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                });

                string PathToString(IList<string> path)
                {
                    return path.Select(p => p.Replace("/", "\\/")).Join('/');
                }

                void AddItem(VrcfSearchWindow.Group group, IList<string> prefix)
                {
                    var children = controlPaths
                        .Where(path => path.Count == prefix.Count + 1)
                        .Where(path => prefix.Select((segment, i) => path[i] == segment).All(c => c))
                        .ToList();
                    if (prefix.Count == 0)
                    {
                        if (foldersOnly)
                        {
                            group.Add("<Select this folder>", "");
                        }

                        foreach (var child in children)
                        {
                            AddItem(group, child);
                        }
                    }
                    else
                    {
                        if (children.Count > 0)
                        {
                            var subGroup = group.AddGroup(prefix.Last());
                            subGroup.Add("<Select this folder>", PathToString(prefix));
                            foreach (var child in children)
                            {
                                AddItem(subGroup, child);
                            }
                        }
                        else
                        {
                            group.Add(prefix.Last(), PathToString(prefix));
                        }
                    }
                }

                var window = new VrcfSearchWindow("Avatar Menu Items");
                AddItem(window.GetMainGroup(), new string[] { });

                window.Open(Apply, pos);
            }

            if (immediate)
            {
                OnClick();
                return null;
            }

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(prop, label, tooltip: tooltip).FlexGrow(1));
            row.Add(new Button(OnClick) { text = selectLabel });
            return row;
        }
    }
}
