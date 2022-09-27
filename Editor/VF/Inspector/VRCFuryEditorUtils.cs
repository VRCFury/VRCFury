using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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
                if (offset != size - 1) {
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = Color.black;
                }
                row.style.alignItems = Align.FlexStart;
                entries.Add(row);

                VisualElement data = RefreshOnTrigger(
                    () => renderElement != null ? renderElement(offset, el) : new PropertyField(el),
                    el.serializedObject,
                    out var triggerRefresh
                );
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

                var shownButton = false;

                if (offset != 0) {
                    var move = new Label("↑");
                    move.AddManipulator(new Clickable(e => {
                        list.MoveArrayElement(offset, offset - 1);
                        list.serializedObject.ApplyModifiedProperties();
                        foreach (var r in refreshAllElements) r();
                    }));
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
                        list.MoveArrayElement(offset, offset + 1);
                        list.serializedObject.ApplyModifiedProperties();
                        foreach (var r in refreshAllElements) r();
                    }));
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
                    entries.Add(new Label("This list is empty. Click + to add an entry."));
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
        el.style.borderTopLeftRadius = el.style.borderTopRightRadius =
        el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = all;
    }
    public static void BorderColor(VisualElement el, Color topbottom, Color leftright) {
        el.style.borderTopColor = el.style.borderBottomColor = topbottom;
        el.style.borderLeftColor = el.style.borderRightColor = leftright;
    }
    public static void BorderColor(VisualElement el, Color all) {
        BorderColor(el, all, all);
    }
    
    public static VisualElement WrappedLabel(string text) {
        var field = new Label(text) {
            style = {
                whiteSpace = WhiteSpace.Normal
            }
        };
        return field;
    }

    public static int LABEL_WIDTH = 153;
    public static VisualElement PropWithoutLabel(SerializedProperty prop) {
        var wrapper = new VisualElement();
        wrapper.style.overflow = Overflow.Hidden;
        switch (prop.propertyType) {
            case SerializedPropertyType.String:
                wrapper.Add(new TextField {
                    bindingPath = prop.propertyPath
                });
                break;
            case SerializedPropertyType.Integer:
                wrapper.Add(new IntegerField() {
                    bindingPath = prop.propertyPath
                });
                break;
            case SerializedPropertyType.Float:
                wrapper.Add(new FloatField() {
                    bindingPath = prop.propertyPath
                });
                break;
            default:
                wrapper.Add(new PropertyField(prop, " ") {
                    style = {
                        marginLeft = -LABEL_WIDTH
                    }
                });
                break;
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

    public static string MakeFilenameSafe(string str) {
        var output = "";
        foreach (var c in str) {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) {
                output += c;
            } else {
                output += '_';
            }
        }
        output = output.Trim();
        if (output.Length > 32) output = output.Substring(0, 32);
        if (output.Length == 0) output = "Unknown";
        return output;
    }
}

}
