using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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
        Func<int, SerializedProperty, VisualElement> renderElement = null,
        Action onPlus = null,
        Func<VisualElement> onEmpty = null
    ) {
        if (list == null) {
            return new Label("List is null");
        }
        
        var container = new VisualElement();
        container.AddToClassList("vfList");

        var entriesContainer = new VisualElement();
        container.Add(entriesContainer);
        Border(entriesContainer, 1);
        BorderColor(entriesContainer, Color.black);
        BorderRadius(entriesContainer, 5);
        entriesContainer.style.backgroundColor = new Color(0,0,0,0.1f);
        entriesContainer.style.minHeight = 20;

        entriesContainer.Add(RefreshOnChange(() => {
            var entries = new VisualElement();
            var size = list.arraySize;
            var refreshAllElements = new List<Action>();
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
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = Color.black;
                }
                row.style.alignItems = Align.FlexStart;
                entries.Add(row);

                VisualElement data = RefreshOnTrigger(
                    () => renderElement != null ? renderElement(offset, el) : Prop(el),
                    el.serializedObject,
                    out var triggerRefresh
                );
                data.AddToClassList("vfListRowData");
                refreshAllElements.Add(triggerRefresh);
                Padding(data, 5);
                data.style.flexGrow = 1;
                row.Add(data);

                var elButtons = new VisualElement();
                elButtons.style.flexDirection = FlexDirection.ColumnReverse;
                elButtons.style.flexGrow = 0;
                elButtons.style.flexShrink = 0;
                elButtons.style.flexBasis = 20;
                row.Add(elButtons);
                data.AddToClassList("vfListRowButtons");

                var shownButton = false;

                void Move(int pos) {
                    if (pos < 0 || pos >= size) return;
                    list.MoveArrayElement(offset, pos);
                    list.serializedObject.ApplyModifiedProperties();
                    foreach (var r in refreshAllElements) r();
                }

                if (offset != 0) {
                    var move = new Label("↑");
                    move.AddManipulator(new Clickable(e => {
                        Move(offset - 1);
                    }));
                    move.AddManipulator(new Clickable(e => {
                        Move(0);
                    }) { activators = { new ManipulatorActivationFilter() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift } } });
                    move.style.flexGrow = 0;
                    move.style.borderLeftColor = move.style.borderBottomColor = Color.black;
                    move.style.borderLeftWidth = move.style.borderBottomWidth = 1;
                    move.style.borderBottomLeftRadius = shownButton ? 0 : 5;
                    move.style.paddingLeft = move.style.paddingRight = 5;
                    move.style.paddingBottom = 3;
                    move.style.unityTextAlign = TextAnchor.MiddleCenter;
                    elButtons.Add(move);
                    shownButton = true;
                }
                
                if (offset != size - 1) {
                    var move = new Label("↓");
                    move.AddManipulator(new Clickable(e => {
                        Move(offset + 1);
                    }));
                    move.AddManipulator(new Clickable(e => {
                        Move(size - 1);
                    }) { activators = { new ManipulatorActivationFilter() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift } } });
                    move.style.flexGrow = 0;
                    move.style.borderLeftColor = move.style.borderBottomColor = Color.black;
                    move.style.borderLeftWidth = move.style.borderBottomWidth = 1;
                    move.style.borderBottomLeftRadius = shownButton ? 0 : 5;
                    move.style.paddingLeft = move.style.paddingRight = 5;
                    move.style.paddingBottom = 3;
                    move.style.unityTextAlign = TextAnchor.MiddleCenter;
                    elButtons.Add(move);
                    shownButton = true;
                }

                var remove = new Label("✕");
                remove.AddManipulator(new Clickable(e => {
                    list.DeleteArrayElementAtIndex(offset);
                    list.serializedObject.ApplyModifiedProperties();
                }));
                remove.style.flexGrow = 0;
                remove.style.borderLeftColor = remove.style.borderBottomColor = Color.black;
                remove.style.borderLeftWidth = remove.style.borderBottomWidth = 1;
                remove.style.borderBottomLeftRadius = shownButton ? 0 : 5;
                remove.style.paddingLeft = remove.style.paddingRight = 5;
                remove.style.paddingBottom = 3;
                remove.style.unityTextAlign = TextAnchor.MiddleCenter;
                elButtons.Add(remove);
            }
            if (size == 0) {
                if (onEmpty != null) {
                    entries.Add(onEmpty());
                } else {
                    var label = WrappedLabel("This list is empty. Click + to add an entry.");
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    Padding(label, 5);
                    entries.Add(label);
                }
            }
            return entries;
        }, list));

        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        container.Add(buttonRow);

        var buttonSpacer = new VisualElement();
        buttonRow.Add(buttonSpacer);
        buttonSpacer.style.flexGrow = 1;

        entriesContainer.style.borderBottomRightRadius = 0;
        var buttons = new VisualElement();
        buttonRow.Add(buttons);
        buttons.style.flexGrow = 0;
        buttons.style.borderLeftColor = buttons.style.borderRightColor = buttons.style.borderBottomColor = Color.black;
        buttons.style.borderLeftWidth = buttons.style.borderRightWidth = buttons.style.borderBottomWidth = 1;
        buttons.style.borderBottomLeftRadius = buttons.style.borderBottomRightRadius = 5;
        var add = new Label("+");
        add.style.paddingLeft = add.style.paddingRight = 5;
        add.style.paddingBottom = 3;
        add.AddManipulator(new Clickable(e => {
            if (onPlus != null) {
                onPlus();
            } else {
                AddToList(list);
            }
        }));
        buttons.Add(add);

        return container;
    }

    public static SerializedProperty AddToList(SerializedProperty list, Action<SerializedProperty> doWith = null) {
        list.serializedObject.Update();
        list.InsertArrayElementAtIndex(list.arraySize);
        var newEntry = list.GetArrayElementAtIndex(list.arraySize-1);
        list.serializedObject.ApplyModifiedProperties();

        var resetFlag = newEntry.FindPropertyRelative("ResetMePlease");
        if (resetFlag != null) {
            resetFlag.boolValue = true;
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            FindAndResetMarkedFields(list.serializedObject.targetObject);
            list.serializedObject.Update();
        }

        if (doWith != null) doWith(newEntry);
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
        string tooltip = null
    ) {
        return Prop(prop, label, tooltip: tooltip, style: s => {
            s.paddingBottom = 5;
            style?.Invoke(s);
        });
    }

    public static (VisualElement, VisualElement) CreateTooltip(string label, string content) {
        VisualElement labelBox = null;
        if (label != null) {
            if (content == null) {
                return (WrappedLabel(label), null);
            }

            labelBox = new VisualElement();
            labelBox.style.flexGrow = 0;
            labelBox.style.flexDirection = FlexDirection.Row;
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
        string tooltip = null
    ) {
        VisualElement field = null;
        if (prop == null) {
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
        }

        field.AddToClassList("VrcFuryEditorProp");

        var output = AssembleProp(
            label,
            tooltip,
            field,
            prop != null && prop.propertyType == SerializedPropertyType.Boolean,
            false,
            labelWidth
        );
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
            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;

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

        if (prop.isArray) {
            var fakeField = new IntegerField();
            fakeField.bindingPath = prop.propertyPath+".Array.size";
            fakeField.style.display = DisplayStyle.None;
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
        container.Add(RefreshOnTrigger(content, props[0].serializedObject, out var triggerRefresh));
        foreach (var prop in props) {
            if (prop != null) {
                var onChangeField = OnChange(prop, triggerRefresh);
                container.Add(onChangeField);
            }
        }
        return container;
    }

    public static bool FindAndResetMarkedFields(object obj) {
        if (obj == null) return false;
        var objType = obj.GetType();
        if (!objType.FullName.StartsWith("VF")) return false;
        var fields = objType.GetFields();
        foreach (var field in fields) {
            var value = field.GetValue(obj);
            if (value is IList) {
                var list = value as IList;
                for (var i = 0; i < list.Count; i++) {
                    var remove = FindAndResetMarkedFields(list[i]);
                    if (remove) {
                        var elemType = list[i].GetType();
                        var newInst = Activator.CreateInstance(elemType);
                        list.RemoveAt(i);
                        list.Insert(i, newInst);
                    }
                }
            } else {
                if (field.Name == "ResetMePlease") {
                    if ((bool)value) {
                        return true;
                    }
                } else {
                    var type = field.FieldType;
                    if (type.IsClass) {
                        FindAndResetMarkedFields(value);
                    }
                }
            }
        }
        return false;
    }

    public static T DeepCloneSerializable<T>(T obj) {
        using (var ms = new MemoryStream()) {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, obj);
            ms.Position = 0;
            return (T) formatter.Deserialize(ms);
        }
    }

    private static float NextFloat(float input, int offset) {
        if (float.IsNaN(input) || float.IsPositiveInfinity(input) || float.IsNegativeInfinity(input))
            return input;

        byte[] bytes = BitConverter.GetBytes(input);
        int bits = BitConverter.ToInt32(bytes, 0);

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
        return NextFloat(input, 1);
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
    
    public static VisualElement Debug(string message = "", Func<string> refreshMessage = null, float interval = 1) {
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
        var label = WrappedLabel(message);
        rightColumn.Add(label);

        if (refreshMessage != null) {
            RefreshOnInterval(el, () => {
                try {
                    label.text = refreshMessage();
                } catch (Exception e) {
                    label.text = $"Error: {e.Message}";
                }
            }, interval);
        }
        
        return el;
    }

    public static Label Error(string message) {
        var label = new Label(message) {
            style = {
                backgroundColor = new Color(0.5f, 0, 0),
                paddingTop = 5,
                paddingBottom = 5,
                unityTextAlign = TextAnchor.MiddleCenter,
                whiteSpace = WhiteSpace.Normal,
                marginTop = 5
            }
        };
        Padding(label, 5);
        BorderColor(label, Color.black);
        BorderRadius(label, 5);
        Border(label, 1);
        return label;
    }

    public static VisualElement Warn(string message) {
        var i = Error(message);
        i.style.backgroundColor = new Color(0.5f, 0.25f, 0);
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
        
        // This shouldn't be needed in unity 2020+
        if (obj is GameObject go) {
            MarkSceneDirty(go.scene);
        } else if (obj is UnityEngine.Component c) {
            MarkSceneDirty(c.gameObject.scene);
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
    }

    public static string Rev(string s) {
        var charArray = s.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }
}
    
}
