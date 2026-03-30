using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Actions;
using VF.Feature.Base;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Inspector {

    [CustomPropertyDrawer(typeof(VF.Model.State))]
    internal class VRCFuryActionSetDrawer : PropertyDrawer {
        public static Func<VFGameObject, State, VisualElement> renderDebugInfo;

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            return render(property);
        }

        public static VisualElement render(
            SerializedProperty prop,
            string myLabel = null,
            int labelWidth = 100,
            string tooltip = null,
            bool showDebugInfo = true
        ) {
            var container = new VisualElement();
            container.AddToClassList("vfState");

            var list = prop.FindPropertyRelative("actions");

            void OnPlus() {
                var menu = new GenericMenu();
                foreach (var menuItem in FeatureFinder.GetAllFeaturesForMenu<ActionBuilder>()) {
                    menu.AddItem(new GUIContent(menuItem.title), false, () => {
                        var instance = Activator.CreateInstance(menuItem.modelType);
                        VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = instance);
                    });
                }
                menu.ShowAsContext();
            }

            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var body = new VisualElement();

                VisualElement singleLineEditor = null;
                if (list.arraySize == 1) {
                    var first = list.GetArrayElementAtIndex(0);
                    var type = VRCFuryEditorUtils.GetManagedReferenceType(first);
                    if (type == typeof(ObjectToggleAction) || type == typeof(AnimationClipAction)) {
                        singleLineEditor = VRCFuryEditorUtils.Prop(first);
                    }
                }

                var showPlus = singleLineEditor != null || list.arraySize == 0;
                var showSingleLineEditor = singleLineEditor != null;
                var showList = singleLineEditor == null && list.arraySize > 0;

                if (showSingleLineEditor || showPlus) {
                    var segments = new VisualElement().Row();
                    body.Add(segments);

                    if (showSingleLineEditor) {
                        singleLineEditor.style.flexGrow = 1;
                        segments.Add(singleLineEditor);
                        var x = new Button()
                            .Text("x")
                            .OnClick(() => {
                                list.DeleteArrayElementAtIndex(0);
                                list.serializedObject.ApplyModifiedProperties();
                            })
                            .FlexBasis(20);
                        segments.Add(x);
                    }
                    if (showPlus) {
                        var plus = new Button()
                            .Text(singleLineEditor != null ? "+" : "Add Action +")
                            .OnClick(OnPlus)
                            .FlexBasis(20)
                            .FlexGrow(showSingleLineEditor ? 0 : 1);
                        segments.Add(plus);
                    }
                }
                if (showList) {
                    body.Add(VRCFuryEditorUtils.List(list, onPlus: OnPlus));
                }

                return VRCFuryEditorUtils.AssembleProp(myLabel, tooltip, body, false, showList, labelWidth);
            }, list));
            
            if (showDebugInfo && renderDebugInfo != null) {
                container.Add(VRCFuryEditorUtils.Debug(refreshElement: () => {
                    var actionSet = prop.GetObject() as State;
                    if (actionSet == null) return new VisualElement();
                    var gameObject = prop.serializedObject.GetGameObject();
                    return renderDebugInfo(gameObject, actionSet);
                }));
            }

            return container;
        }
    }

}
