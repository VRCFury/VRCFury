using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Model;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Inspector {

public static class VRCFuryEditorUtils {

    public static VisualElement List(
        SerializedProperty list,
        Action onPlus = null,
        Func<VisualElement> onEmpty = null
    ) {
        var output = new VisualElement();
        output.AddToClassList("vfList");

        if (list == null) {
            return Error("List is null");
        }
        if (!list.isArray) {
            return Error("List is not an array");
        }

        void OnClickPlus() {
            if (onPlus != null) {
                onPlus();
            } else {
                AddToList(list);
            }
        }

        void OnClickMinus() {
            EditorUtility.DisplayDialog("VRCFury", "Right click on the element you would like to remove", "Ok");
        }

        void Move(int offset, int pos) {
            if (pos < 0 || pos >= list.arraySize) return;
            list.MoveArrayElement(offset, pos);
            list.serializedObject.ApplyModifiedProperties();
            output.Bind(list.serializedObject);
        }

        void CreateRightClickMenu(VisualElement el) {
            el.AddManipulator(new ContextualMenuManipulator(e => {
                var offset = (int)el.userData;
                if (e.menu.MenuItems().Count > 0) {
                    e.menu.AppendSeparator();
                }
                e.menu.AppendAction("🗙 Remove Item", a => {
                    list.DeleteArrayElementAtIndex(offset);
                    list.serializedObject.ApplyModifiedProperties();
                });
                e.menu.AppendSeparator();
                var disabledIfTop = offset == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal;
                var disabledIfBottom = offset == list.arraySize - 1 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal;
                e.menu.AppendAction("🡱 Move up", a => {
                    Move(offset, offset - 1);
                }, disabledIfTop);
                e.menu.AppendAction("🡳 Move down", a => {
                    Move(offset, offset+1);
                }, disabledIfBottom);
                e.menu.AppendSeparator();
                e.menu.AppendAction("🡱🡱 Move to top", a => {
                    Move(offset, 0);
                }, disabledIfTop);
                e.menu.AppendAction("🡳🡳 Move to bottom", a => {
                    Move(offset, list.arraySize-1);
                }, disabledIfBottom);
                e.StopPropagation();
            }));
        }

#if UNITY_2022_1_OR_NEWER
        var listView = new ListView();
        listView.AddToClassList("vfList__listView");
        listView.reorderable = true;
        listView.reorderMode = ListViewReorderMode.Animated;
        listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
        listView.showBorder = true;
        listView.showAddRemoveFooter = false;
        listView.bindingPath = list.propertyPath;
        listView.showBoundCollectionSize = false;
        listView.selectionType = SelectionType.None;
        listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;

        listView.makeItem = () => {
            var field = new PropertyField();
            CreateRightClickMenu(field);
            return field;
        };
        listView.bindItem = (element, i) => {
            ((PropertyField)element).bindingPath = list.GetArrayElementAtIndex(i).propertyPath;
            element.Bind(list.serializedObject);
            element.userData = i;
        };
        listView.unbindItem = (element, i) => {
            element.Unbind();
        };

        var footer = new VisualElement() {
            name = BaseListView.footerUssClassName
        };
        footer.AddToClassList(BaseListView.footerUssClassName);
        footer.Add(new Button(OnClickMinus) {
            name = BaseListView.footerRemoveButtonName,
            text = "-"
        });
        footer.Add(new Button(OnClickPlus) {
            name = BaseListView.footerAddButtonName,
            text = "+"
        });

        output.Add(listView);
        output.Add(footer);
#else
        var entriesContainer = new VisualElement()
            .Border(1)
            .BorderColor(Color.black)
            .BorderRadius(5);
        output.Add(entriesContainer);
        entriesContainer.style.backgroundColor = new Color(0,0,0,0.1f);
        entriesContainer.style.minHeight = 20;

        entriesContainer.Add(RefreshOnChange(() => {
            var entries = new VisualElement();
            var size = list.arraySize;
            for (var i = 0; i < size; i++) {
                var offset = i;
                var el = list.GetArrayElementAtIndex(i);
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row
                    }
                };
                row.AddToClassList("vfListRow");
                if (offset != size - 1) {
                    row.AddToClassList("vfList2019__notLastItem");
                }
                row.style.alignItems = Align.FlexStart;
                entries.Add(row);

                VisualElement data = Prop(el).Padding(5);
                data.AddToClassList("vfListRowData");
                data.style.flexGrow = 1;
                row.Add(data);

                row.userData = i;
                CreateRightClickMenu(row);

                data.AddToClassList("vfListRowButtons");
            }
            if (size == 0) {
                if (onEmpty != null) {
                    entries.Add(onEmpty());
                } else {
                    var label = WrappedLabel("This list is empty. Click + to add an entry.").Padding(5);
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    entries.Add(label);
                }
            }
            return entries;
        }, list));

        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        output.Add(buttonRow);

        var buttonSpacer = new VisualElement();
        buttonRow.Add(buttonSpacer);
        buttonSpacer.style.flexGrow = 1;

        entriesContainer.style.borderBottomRightRadius = 0;
        var buttons = new VisualElement();
        buttons.AddToClassList("vfList2019__buttons");
        buttonRow.Add(buttons);

        var add = new Label("+");
        add.AddToClassList("vfList2019__button");
        add.AddManipulator(new Clickable(OnClickPlus));
        buttons.Add(add);
        
        var subtract = new Label("-");
        subtract.AddToClassList("vfList2019__button");
        subtract.AddManipulator(new Clickable(OnClickMinus));
        buttons.Add(subtract);
#endif
        return output;
    }

    public static SerializedProperty AddToList(SerializedProperty list, Action<SerializedProperty> doWith = null) {
        list.serializedObject.Update();
        list.InsertArrayElementAtIndex(list.arraySize);
        var newEntry = list.GetArrayElementAtIndex(list.arraySize-1);
        list.serializedObject.ApplyModifiedProperties();

        // InsertArrayElementAtIndex makes a copy of the last element for some reason, instead of a fresh copy
        // We fix that here by finding the raw array and creating a fresh object for the new element
        if (newEntry.propertyType == SerializedPropertyType.ManagedReference) {
            newEntry.managedReferenceValue = null;
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        } else {
            if (list.GetObject() is IList listObj) {
                var type = listObj[listObj.Count - 1].GetType();
                if (type == typeof(string)) {
                    listObj[listObj.Count - 1] = "";
                } else {
                    listObj[listObj.Count - 1] = Activator.CreateInstance(type);
                }
                list.serializedObject.Update();
            } else {
                UnityEngine.Debug.LogError("Failed to find list to reset new entry. This is likely a VRCFury bug, please report on the discord.");
            }
        }

        if (doWith != null) {
            doWith(newEntry);
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        return newEntry;
    }

    public static Label WrappedLabel(string text) {
        return new Label(text).TextWrap();
    }
    
    public static VisualElement BetterProp(
        SerializedProperty prop,
        string label = null,
        string tooltip = null,
        VisualElement fieldOverride = null
    ) {
        var el = Prop(prop, label, tooltip: tooltip, fieldOverride: fieldOverride);
        el.PaddingBottom(5);
        return el;
    }

    public static (VisualElement, VisualElement) CreateTooltip(string label, string content) {
        VisualElement labelBox = null;
        if (label != null) {
            if (content == null) {
                return (WrappedLabel(label), null);
            }

            labelBox = new VisualElement().Row();
            labelBox.Add(WrappedLabel(label));
            var im = new Image {
                image = EditorGUIUtility.FindTexture("_Help"),
                scaleMode = ScaleMode.ScaleToFit
            };
            labelBox.Add(im);
        }

        VisualElement tooltipBox = null;
        if (content != null && labelBox != null) {
            tooltipBox = Info(content);
            tooltipBox.AddToClassList("vfTooltip");
            tooltipBox.AddToClassList("vfTooltipHidden");
            labelBox.AddManipulator(new Clickable(e => {
                tooltipBox.ToggleInClassList("vfTooltipHidden");
            }));
        }

        return (labelBox, tooltipBox);
    }
    
    public static VisualElement Prop(
        SerializedProperty prop,
        string label = null,
        int labelWidth = 100,
        Func<string,string> formatEnum = null,
        string tooltip = null,
        VisualElement fieldOverride = null
    ) {
        VisualElement field = null;
        var isCheckbox = false;
        if (fieldOverride != null) {
            field = fieldOverride;
            isCheckbox = field is Toggle;
        } else if (prop == null) {
            field = WrappedLabel("Prop is null");
        } else {
            switch (prop.propertyType) {
                case SerializedPropertyType.Vector4: {
                    field = new Vector4Field { bindingPath = prop.propertyPath }.FlexShrink(1);
                    break;
                }
                case SerializedPropertyType.Enum: {
                    field = new PopupField<string>(
                        prop.enumDisplayNames.ToList(),
                        prop.enumValueIndex,
                        formatSelectedValueCallback: formatEnum,
                        formatListItemCallback: formatEnum
                    ) { bindingPath = prop.propertyPath }.FlexShrink(1);
                    break;
                }
                case SerializedPropertyType.Generic: {
                    if (prop.type == "State") {
                        return VRCFuryStateEditor.render(prop, label, labelWidth, tooltip);
                    }

                    break;
                }
            }
            if (field == null) {
                field = new PropertyField(prop);
            }
            isCheckbox = prop.propertyType == SerializedPropertyType.Boolean;
        }

        field.AddToClassList("VrcFuryEditorProp");

        return AssembleProp(
            label,
            tooltip,
            field,
            isCheckbox,
            false,
            labelWidth
        );
    }

    public static VisualElement AssembleProp(
        string label,
        string tooltip,
        VisualElement field,
        bool isCheckbox,
        bool forceLabelOnOwnLine,
        int labelWidth
    ) {
        var (labelBox, tooltipBox) = CreateTooltip(label, tooltip);
        var wrapper = new VisualElement();
        var addFieldLast = false;
        if (isCheckbox && labelBox != null) {
            var row = new VisualElement().Row().FlexShrink(0);
            field.style.paddingRight = 3;
            row.Add(field);
            labelBox.style.flexShrink = 1;
            row.Add(labelBox);
            wrapper.Add(row);
        } else if (forceLabelOnOwnLine || (label != null && label.Length > 16) || labelBox == null || field == null) {
            if (labelBox != null) {
                wrapper.Add(labelBox);
            }
            addFieldLast = true;
        } else {
            var labelRow = new VisualElement().Row();
            labelBox.style.minWidth = labelWidth;
            labelBox.style.flexGrow = 0;
            labelRow.Add(labelBox);

            field.style.flexGrow = 1;
            labelRow.Add(field);

            wrapper.Add(labelRow);
        }

        if (tooltipBox != null) {
            wrapper.Add(tooltipBox);
        }
        if (field != null && addFieldLast) {
            wrapper.Add(field);
        }

        return wrapper;
    }

    public static VisualElement OnChange(SerializedProperty prop, Action changed) {

        var c = changed;
        changed = () => {
            // Unity sometimes calls onchange when the SerializedProperty is no longer valid.
            // Unfortunately the only way to detect this is to try to access it and catch an error, since isValid is internal
            try {
                var name = prop.name;
            } catch (Exception) {
                return;
            }
            c();
        };

        switch(prop.propertyType) {
            case SerializedPropertyType.Boolean:
                return _OnChange(prop, () => prop.boolValue, changed, (a,b) => a==b);
            case SerializedPropertyType.Integer:
                return _OnChange(prop, () => prop.intValue, changed, (a,b) => a==b);
            case SerializedPropertyType.String:
                return _OnChange(prop, () => prop.stringValue, changed, (a,b) => a==b);
            case SerializedPropertyType.ObjectReference:
                return _OnChange(prop, () => prop.objectReferenceValue, changed, (a,b) => a==b);
            case SerializedPropertyType.Enum:
                return _OnChange(prop, () => prop.enumValueIndex, changed, (a,b) => a==b);
        }

        if (prop.isArray) {
            var fakeField = new IntegerField();
            fakeField.bindingPath = prop.propertyPath+".Array.size";
            fakeField.SetVisible(false);
            var oldValue = prop.arraySize;
            fakeField.RegisterValueChangedCallback(e => {
                if (prop.arraySize == oldValue) return;
                oldValue = prop.arraySize;
                //Debug.Log("Detected change in " + prop.propertyPath);
                changed();
            });
            return fakeField;
        }
        throw new Exception("Type " + prop.propertyType + " not supported (yet) by OnChange");
    }
    private static VisualElement _OnChange<T>(SerializedProperty prop, Func<T> getValue, Action changed, Func<T,T,bool> equals) {
        // The register events can sometimes randomly fire when binding / unbinding happens,
        // with the oldValue being "null", so we have to do our own change detection by caching the old value.
        var fakeField = new PropertyField(prop).SetVisible(false);
    
        var oldValue = getValue();
        void Check() {
            var newValue = getValue();
            if (equals(oldValue, newValue)) return;
            oldValue = newValue;
            //Debug.Log("Detected change in " + prop.propertyPath);
            changed();
        }
        if (prop.propertyType == SerializedPropertyType.Enum) {
            fakeField.RegisterCallback<ChangeEvent<string>>(e => changed());
        } else {
            fakeField.RegisterCallback<ChangeEvent<T>>(e => Check());
        }

        return fakeField;
    }
    
    public static VisualElement RefreshOnTrigger(Func<VisualElement> content, SerializedObject obj, out Action triggerRefresh) {
        var inner = new VisualElement();
        inner.Add(content());

        void Refresh() {
            inner.Unbind();
            inner.Clear();
            var newContent = content();
            inner.Add(newContent);
            inner.Bind(obj);
        }

        triggerRefresh = Refresh;
        return inner;
    }

    public static VisualElement RefreshOnChange(Func<VisualElement> content, params SerializedProperty[] props) {
        var container = new VisualElement();
        if (props.Length == 0 || props.Any(p => p == null))
            throw new Exception("RefreshOnChange received null prop");
        container.Add(RefreshOnTrigger(content, props[0].serializedObject, out var triggerRefresh));
        foreach (var prop in props) {
            if (prop != null) {
                var onChangeField = OnChange(prop, triggerRefresh);
                container.Add(onChangeField);
            }
        }
        return container;
    }

    private static float NextFloat(float input, int offset) {
        if (float.IsNaN(input) || float.IsPositiveInfinity(input) || float.IsNegativeInfinity(input))
            return input;

        var bytes = BitConverter.GetBytes(input);
        var bits = BitConverter.ToInt32(bytes, 0);

        if (input > 0) {
            bits += offset;
        } else if (input < 0) {
            bits -= offset;
        } else if (input == 0) {
            return (offset > 0) ? float.Epsilon : -float.Epsilon;
        }

        bytes = BitConverter.GetBytes(bits);
        return BitConverter.ToSingle(bytes, 0);
    }

    public static float NextFloatUp(float input) {
        return NextFloat(input, 1);
    }
    public static float NextFloatDown(float input) {
        return NextFloat(input, -1);
    }

    public static VisualElement Section(string title = null, string subtitle = null) {
        var section = new VisualElement() {
            style = {
                backgroundColor = new Color(0,0,0,0.1f),
                marginTop = 5,
                marginBottom = 10
            }
        }.Padding(5).BorderRadius(5);

        if (title != null) {
            section.Add(WrappedLabel(title).Bold().TextAlign(TextAnchor.MiddleCenter));
        }

        if (subtitle != null) {
            section.Add(WrappedLabel(subtitle).TextAlign(TextAnchor.MiddleCenter).PaddingBottom(5));
        }

        return section;
    }

    public static VisualElement Info(string message) {
        var el = new VisualElement() {
            style = {
                backgroundColor = new Color(0,0,0,0.1f),
                marginTop = 5,
                marginBottom = 10,
                flexDirection = FlexDirection.Row,
                alignItems = Align.FlexStart
            }
        }.Padding(5).BorderRadius(5);
        el.Add(new Image {
            image = EditorGUIUtility.FindTexture("_Help"),
            scaleMode = ScaleMode.ScaleToFit
        });
        el.Add(WrappedLabel(message).FlexGrow(1));
        return el;
    }
    
    public static VisualElement Debug(string message = "", Func<string> refreshMessage = null, Func<VisualElement> refreshElement = null, float interval = 1) {
        var el = new VisualElement() {
            style = {
                backgroundColor = new Color(0,0,0,0.1f),
                marginTop = 5,
                marginBottom = 10,
                flexDirection = FlexDirection.Row,
                alignItems = Align.FlexStart
            }
        }.Padding(5).BorderRadius(5);
        el.Add(new Image {
            image = EditorGUIUtility.FindTexture("d_Lighting"),
            scaleMode = ScaleMode.ScaleToFit
        });
        var rightColumn = new VisualElement();
        el.Add(rightColumn);
        rightColumn.Add(WrappedLabel("Debug Info").Bold());

        if (refreshElement != null) {
            var holder = new VisualElement();
            rightColumn.Add(holder);
            RefreshOnInterval(el, () => {
                holder.Clear();
                var show = false;
                try {
                    var newContent = refreshElement();
                    if (newContent != null) {
                        holder.Add(newContent);
                        show = true;
                    }
                } catch (Exception e) {
                    holder.Add(WrappedLabel($"Error: {e.Message}"));
                    show = true;
                }
                el.SetVisible(show);
            }, interval);
        } else {
            var label = WrappedLabel(message);
            rightColumn.Add(label);
            if (refreshMessage != null) {
                RefreshOnInterval(el, () => {
                    var show = false;
                    try {
                        label.text = refreshMessage();
                        show = !string.IsNullOrWhiteSpace(label.text);
                    } catch (Exception e) {
                        label.text = $"Error: {e.Message}";
                        show = true;
                    }
                    el.SetVisible(show);
                }, interval);
            }
        }
        
        return el;
    }

    public static VisualElement Error(string message) {
        var i = Section().BorderColor(Color.red).Border(2);
        i.Add(WrappedLabel(message));
        return i;
    }

    public static VisualElement Warn(string message) {
        var i = Section().BorderColor(Color.yellow).Border(2);
        i.Add(WrappedLabel(message));
        return i;
    }
    
    public static Type GetManagedReferenceType(SerializedProperty prop) {
        var typename = prop.managedReferenceFullTypename;
        var i = typename.IndexOf(' ');
        if (i > 0) {
            var assemblyPart = typename.Substring(0, i);
            var nsClassnamePart = typename.Substring(i);
            return Type.GetType($"{nsClassnamePart}, {assemblyPart}");
        }
        return null;
    }
    
    public static string GetManagedReferenceTypeName(SerializedProperty prop) {
        return GetManagedReferenceType(prop)?.Name;
    }

    /**
     * VRLabs Ragdoll System makes a copy of the entire armature, including VRCFury components,
     * which can result in a lot of duplicates.
     */
    public static bool IsInRagdollSystem(VFGameObject obj) {
        while (obj != null) {
            if (obj.name == "Ragdoll System") return true;
            if (obj.name == "CarbonCopy Container") return true;
            // DexClone_worldSpace/CloneContainer0?
            if (obj.name.StartsWith("CloneContainer")) return true;
            if (obj.name == "DexClone_worldSpace") return true;
            if (obj.name == "CCopy World Space") return true;
            obj = obj.parent;
        }
        return false;
    }
    
    public static void HoverHighlight(VisualElement el) {
        var oldBg = new StyleColor();
        
        el.RegisterCallback<MouseOverEvent>(e => {
            oldBg = el.style.backgroundColor;
            float FadeUp(float val) {
                return (1 - val) * 0.1f + val;
            }
            var newBg = oldBg.keyword == StyleKeyword.Undefined
                ? new Color(FadeUp(oldBg.value.r), FadeUp(oldBg.value.g), FadeUp(oldBg.value.b))
                : new Color(1, 1, 1, 0.1f);
            el.style.backgroundColor = newBg;
        });
        el.RegisterCallback<MouseOutEvent>(e => {
            el.style.backgroundColor = oldBg;
        });
    }

    [InitializeOnLoadMethod]
    public static void MakeMarkDirtyAvailableToRuntime() {
        VRCFury.markDirty = MarkDirty;
    }
    public static void MarkDirty(Object obj) {
        EditorUtility.SetDirty(obj);
        
        // This shouldn't be needed in unity 2020+
        if (obj is GameObject go) {
            MarkSceneDirty(go.scene);
        } else if (obj is UnityEngine.Component c) {
            MarkSceneDirty(c.owner().scene);
        }
    }

    private static void MarkSceneDirty(Scene scene) {
        if (Application.isPlaying) return;
        if (scene == null) return;
        if (!scene.isLoaded) return;
        if (!scene.IsValid()) return;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void RefreshOnInterval(VisualElement el, Action run, float interval = 1) {
        double lastUpdate = 0;
        void Update() {
            var now = EditorApplication.timeSinceStartup;
            if (lastUpdate < now - interval) {
                lastUpdate = now;
                run();
            }
        }
        el.RegisterCallback<AttachToPanelEvent>(e => {
            EditorApplication.update += Update;
        });
        el.RegisterCallback<DetachFromPanelEvent>(e => {
            EditorApplication.update -= Update;
        });
        Update();
    }

    public static string Rev(string s) {
        var charArray = s.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }
    
    public static T GetResource<T>(string path) where T : Object {
        var resourcesPath = AssetDatabase.GUIDToAssetPath("c4e4fa889bc2bc54abfc219a5424b763");
        return AssetDatabase.LoadAssetAtPath<T>($"{resourcesPath}/{path}");
    }

    public static T LoadGuid<T>(string guid) where T : Object {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrWhiteSpace(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    
    public static Type GetPropertyType(SerializedProperty prop) {
        var util = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ScriptAttributeUtility");
        var method = util.GetMethod("GetFieldInfoFromProperty",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var prms = new object[] { prop, null };
        method.Invoke(null, prms);
        return prms[1] as Type;
    }
}
    
}
