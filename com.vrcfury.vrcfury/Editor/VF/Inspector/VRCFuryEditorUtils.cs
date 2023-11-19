using System;
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
        var entriesContainer = new VisualElement();
        output.Add(entriesContainer);
        Border(entriesContainer, 1);
        BorderColor(entriesContainer, Color.black);
        BorderRadius(entriesContainer, 5);
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

                VisualElement data = Prop(el);
                data.AddToClassList("vfListRowData");
                Padding(data, 5);
                data.style.flexGrow = 1;
                row.Add(data);

                row.userData = i;
                CreateRightClickMenu(row);

                data.AddToClassList("vfListRowButtons");
            }
            if (size != 0) return entries;
            if (onEmpty != null) {
                entries.Add(onEmpty());
            } else {
                var label = WrappedLabel("This list is empty. Click + to add an entry.");
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                Padding(label, 5);
                entries.Add(label);
            }
            return entries;
        }, list));

        var buttonRow = new VisualElement {
            style = {
                flexDirection = FlexDirection.Row
            }
        };

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

        var resetFlag = newEntry.FindPropertyRelative("ResetMePlease2");
        if (resetFlag != null) {
            resetFlag.boolValue = true;
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            UnitySerializationUtils.FindAndResetMarkedFields(list.serializedObject.targetObject);
            list.serializedObject.Update();
        }

        doWith?.Invoke(newEntry);
        list.serializedObject.ApplyModifiedPropertiesWithoutUndo();

        return newEntry;
    }

    public static void Margin(VisualElement el, float topbottom, float leftright) {
        el.style.marginTop = el.style.marginBottom = topbottom;
        el.style.marginLeft = el.style.marginRight = leftright;
    }
    public static void Margin(VisualElement el, float all) {
        Margin(el, all, all);
    }
    public static void Padding(VisualElement el, float topbottom, float leftright) {
        el.style.paddingTop = el.style.paddingBottom = topbottom;
        el.style.paddingLeft = el.style.paddingRight = leftright;
    }
    public static void Padding(VisualElement el, float all) {
        Padding(el, all, all);
    }
    public static void Border(VisualElement el, float topbottom, float leftright) {
        el.style.borderTopWidth = el.style.borderBottomWidth = topbottom;
        el.style.borderLeftWidth = el.style.borderRightWidth = leftright;
    }
    public static void Border(VisualElement el, float all) {
        Border(el, all, all);
    }
    public static void BorderRadius(VisualElement el, float all) {
        BorderRadius(el.style, all);
    }
    public static void BorderRadius(IStyle style, float all) {
        style.borderTopLeftRadius = style.borderTopRightRadius =
            style.borderBottomLeftRadius = style.borderBottomRightRadius = all;
    }
    public static void BorderColor(VisualElement el, Color topbottom, Color leftright) {
        el.style.borderTopColor = el.style.borderBottomColor = topbottom;
        el.style.borderLeftColor = el.style.borderRightColor = leftright;
    }
    public static void BorderColor(VisualElement el, Color all) {
        BorderColor(el, all, all);
    }
    
    public static Label WrappedLabel(string text, Action<IStyle> style = null) {
        var field = new Label(text) {
            style = {
                whiteSpace = WhiteSpace.Normal
            }
        };
        style?.Invoke(field.style);
        return field;
    }

    public static VisualElement Button(string text, Action onPress) {
        var b = new Button(onPress) {
            text = text,
        };
        Margin(b, 0);
        return b;
    }

    public static VisualElement BetterCheckbox(
        SerializedProperty prop,
        string label,
        Action<IStyle> style = null
    ) {
        return BetterProp(prop, label, style: style);
    }
    
    public static VisualElement BetterProp(
        SerializedProperty prop,
        string label = null,
        Action<IStyle> style = null,
        string tooltip = null,
        VisualElement fieldOverride = null
    ) {
        return Prop(prop, label, tooltip: tooltip, fieldOverride: fieldOverride, style: style, better: true);
    }

    public static (VisualElement, VisualElement) CreateTooltip(string label, string content) {
        VisualElement labelBox = null;
        if (label != null) {
            if (content == null) {
                return (WrappedLabel(label), null);
            }

            labelBox = new VisualElement {
                style = {
                    flexGrow = 0,
                    flexDirection = FlexDirection.Row
                }
            };
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
        Action<IStyle> style = null,
        string tooltip = null,
        VisualElement fieldOverride = null,
        bool better = false
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
                case SerializedPropertyType.Enum: {
                    field = new PopupField<string>(
                        prop.enumDisplayNames.ToList(),
                        prop.enumValueIndex,
                        formatSelectedValueCallback: formatEnum,
                        formatListItemCallback: formatEnum
                    ) { bindingPath = prop.propertyPath };
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

        var output = AssembleProp(
            label,
            tooltip,
            field,
            isCheckbox,
            false,
            labelWidth
        );
        if (better) {
            output.style.paddingBottom = 5;
        }
        style?.Invoke(output.style);
        return output;
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
            var row = new VisualElement() {
                style = {
                    flexDirection = FlexDirection.Row
                }
            };
            field.style.paddingRight = 3;
            field.style.flexShrink = 0;
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
            var labelRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row
                }
            };

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

        if (!prop.isArray) throw new Exception("Type " + prop.propertyType + " not supported (yet) by OnChange");
        var fakeField = new IntegerField {
            bindingPath = prop.propertyPath+".Array.size",
            style = {
                display = DisplayStyle.None
            }
        };
        var oldValue = prop.arraySize;
        fakeField.RegisterValueChangedCallback(e => {
            if (prop.arraySize == oldValue) return;
            oldValue = prop.arraySize;
            //Debug.Log("Detected change in " + prop.propertyPath);
            changed();
        });
        return fakeField;
    }
    private static VisualElement _OnChange<T>(SerializedProperty prop, Func<T> getValue, Action changed, Func<T,T,bool> equals) {
        // The register events can sometimes randomly fire when binding / unbinding happens,
        // with the oldValue being "null", so we have to do our own change detection by caching the old value.
        var fakeField = new PropertyField(prop) { style = { display = DisplayStyle.None } };
    
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
        foreach (var prop in props)     {
            if (prop == null) continue;
            var onChangeField = OnChange(prop, triggerRefresh);
            container.Add(onChangeField);
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
        };
        VRCFuryEditorUtils.Padding(section, 5);
        VRCFuryEditorUtils.BorderRadius(section, 5);

        if (title != null) {
            section.Add(VRCFuryEditorUtils.WrappedLabel(title, style => {
                style.unityFontStyleAndWeight = FontStyle.Bold;
                style.unityTextAlign = TextAnchor.MiddleCenter;
            }));
        }

        if (subtitle != null) {
            section.Add(VRCFuryEditorUtils.WrappedLabel(subtitle, style => {
                style.unityTextAlign = TextAnchor.MiddleCenter;
                style.paddingBottom = 5;
            }));
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
        };
        Padding(el, 5);
        BorderRadius(el, 5);
        var im = new Image {
            image = EditorGUIUtility.FindTexture("_Help"),
            scaleMode = ScaleMode.ScaleToFit
        };
        el.Add(im);
        var label = WrappedLabel(message);
        label.style.flexGrow = 1;
        label.style.flexBasis = 0;
        el.Add(label);
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
        };
        Padding(el, 5);
        BorderRadius(el, 5);
        var im = new Image {
            image = EditorGUIUtility.FindTexture("d_Lighting"),
            scaleMode = ScaleMode.ScaleToFit
        };
        el.Add(im);
        var rightColumn = new VisualElement();
        el.Add(rightColumn);
        var title = WrappedLabel("Debug Info");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        rightColumn.Add(title);

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
                el.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
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
                    el.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                }, interval);
            }
        }
        
        return el;
    }

    public static VisualElement Error(string message) {
        var i = Section();
        i.Add(WrappedLabel(message));
        BorderColor(i, Color.red);
        Border(i, 2);
        return i;
    }

    public static VisualElement Warn(string message) {
        var i = Section();
        i.Add(WrappedLabel(message));
        BorderColor(i, Color.yellow);
        Border(i, 2);
        return i;
    }
    
    public static Type GetManagedReferenceType(SerializedProperty prop) {
        var typename = prop.managedReferenceFullTypename;
        var i = typename.IndexOf(' ');
        if (i <= 0) return null;
        var assemblyPart = typename.Substring(0, i);
        var nsClassnamePart = typename.Substring(i);
        return Type.GetType($"{nsClassnamePart}, {assemblyPart}");
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
            switch (obj.name) {
                case "Ragdoll System":
                case "CarbonCopy Container":
                    return true;
            }

            // DexClone_worldSpace/CloneContainer0?
            if (obj.name.StartsWith("CloneContainer")) return true;
            if (obj.name == "DexClone_worldSpace") return true;
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

    public static void MarkDirty(Object obj) {
        EditorUtility.SetDirty(obj);

        switch (obj) {
            // This shouldn't be needed in unity 2020+
            case GameObject go:
                MarkSceneDirty(go.scene);
                break;
            case UnityEngine.Component c:
                MarkSceneDirty(c.gameObject.scene);
                break;
        }
    }

    private static void MarkSceneDirty(Scene scene) {
        if (Application.isPlaying) return;
        if (!scene.isLoaded) return;
        if (!scene.IsValid()) return;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void RefreshOnInterval(VisualElement el, Action run, float interval = 1) {
        double lastUpdate = 0;
        void Update() {
            var now = EditorApplication.timeSinceStartup;
            if (!(lastUpdate < now - interval)) return;
            lastUpdate = now;
            run();
        }
        el.RegisterCallback<AttachToPanelEvent>(e => {
            EditorApplication.update += Update;
        });
        el.RegisterCallback<DetachFromPanelEvent>(e => {
            EditorApplication.update -= Update;
        });
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
        return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }
    
    public static Type GetPropertyType(SerializedProperty prop) {
        var util = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ScriptAttributeUtility");
        var method = util.GetMethod("GetFieldInfoFromProperty",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var prms = new object[] { prop, null };
        method?.Invoke(null, prms);
        return prms[1] as Type;
    }
}
    
}
