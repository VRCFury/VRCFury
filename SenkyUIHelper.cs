#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class SenkyUIHelper {

    public static VisualElement List(SerializedProperty list, Func<int, SerializedProperty, VisualElement> renderElement = null, Action onPlus = null) {
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
            for (var i = 0; i < size; i++) {
                var offset = i;
                var el = list.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                if (offset != size - 1) {
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = Color.black;
                }
                row.style.alignItems = Align.FlexStart;
                entries.Add(row);

                var data = renderElement != null ? renderElement(offset, el) : new PropertyField(el);
                Padding(data, 5);
                data.style.flexGrow = 1;
                row.Add(data);

                var remove = new Label("x");
                remove.AddManipulator(new Clickable(e => {
                    list.DeleteArrayElementAtIndex(offset);
                    list.serializedObject.ApplyModifiedProperties();
                }));
                remove.style.flexGrow = 0;
                remove.style.borderLeftColor = remove.style.borderBottomColor = Color.black;
                remove.style.borderLeftWidth = remove.style.borderBottomWidth = 1;
                remove.style.borderBottomLeftRadius = 5;
                remove.style.paddingLeft = remove.style.paddingRight = 5;
                remove.style.paddingBottom = 3;
                row.Add(remove);
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
                addToList(list);
            }
        }));
        buttons.Add(add);

        return container;
    }

    public static SerializedProperty addToList(SerializedProperty list, Action<SerializedProperty> doWith = null) {
        list.serializedObject.Update();
        list.InsertArrayElementAtIndex(list.arraySize);
        var newEntry = list.GetArrayElementAtIndex(list.arraySize-1);
        list.serializedObject.ApplyModifiedProperties();

        var resetFlag = newEntry.FindPropertyRelative("ResetMePlease");
        if (resetFlag != null) {
            resetFlag.boolValue = true;
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            DefaultsCleaner.Cleanup(list.serializedObject.targetObject);
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

    public static VisualElement PropWithoutLabel(SerializedProperty prop) {
        var field = new PropertyField(prop, " ");
        field.style.marginLeft = -LABEL_WIDTH;
        return field;
    }

    public static VisualElement OnChange(SerializedProperty prop, Action changed) {
        if (prop.isArray) {
            var fakeField = new IntegerField();
            fakeField.bindingPath = prop.propertyPath+".Array.size";
            fakeField.style.display = DisplayStyle.None;
            fakeField.RegisterValueChangedCallback(e => {
                changed();
            });
            return fakeField;
        } else {
            var fakeField = new PropertyField(prop);
            fakeField.style.display = DisplayStyle.None;
            switch(prop.propertyType) {
                case SerializedPropertyType.Boolean:
                    fakeField.RegisterCallback<ChangeEvent<bool>>(e => changed());
                    break;
                case SerializedPropertyType.Integer:
                    fakeField.RegisterCallback<ChangeEvent<int>>(e => changed());
                    break;
                case SerializedPropertyType.String:
                    fakeField.RegisterCallback<ChangeEvent<string>>(e => changed());
                    break;
                case SerializedPropertyType.ObjectReference:
                    fakeField.RegisterCallback<ChangeEvent<UnityEngine.Object>>(e => changed());
                    break;
                default:
                    throw new Exception("Type " + prop.propertyType + " not supported (yet) by OnChange");
            }
            return fakeField;
        }
    }

    public static VisualElement RefreshOnChange(Func<VisualElement> content, params SerializedProperty[] props) {
        var container = new VisualElement();
        var inner = new VisualElement();
        container.Add(inner);
        inner.Add(content());
        Action refresh = () => {
            inner.RemoveAt(inner.childCount-1);
            var newContent = content();
            inner.Add(newContent);
            newContent.Bind(props[0].serializedObject);
        };
        foreach (var prop in props) {
            container.Add(OnChange(prop, refresh));
        }
        container.RegisterCallback<RefreshEvent>(e => {
            refresh();
        });
        return container;
    }

    public static int LABEL_WIDTH = 137;
}

public class RefreshEvent : EventBase<RefreshEvent> {
}

#endif
