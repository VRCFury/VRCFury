using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class SenkyUIHelper {
    private Stack<VisualElement> stack = new Stack<VisualElement>();
    private VisualElement inspector = new VisualElement();
    private Func<string,SerializedProperty> GetProp;
    private SerializedObject root;

    public SenkyUIHelper(SerializedObject obj)
        : this(obj.FindProperty, obj) { }

    public SenkyUIHelper(SerializedProperty prop)
        : this(prop.FindPropertyRelative, prop.serializedObject) { }

    private SenkyUIHelper(Func<string,SerializedProperty> GetProp, SerializedObject root) {
        stack.Push(inspector);
        this.GetProp = GetProp;
        this.root = root;
    }

    private VisualElement getContainer() {
        return stack.Peek();
    }
    private void Inside(VisualElement el, Action with) {
        stack.Push(el);
        with();
        stack.Pop();
    }

    public void Add(VisualElement el) {
        getContainer().Add(el);
    }
    public void FoldoutOpen(string header, Action with) {
        Foldout(header, with, true);
    }
    public void Foldout(string header, Action with, bool def = false) {
        var foldout = new Foldout();
        foldout.text = header;
        Add(foldout);
        Inside(foldout.contentContainer, with);
    }
    public void Property(string prop, string label="") {
        Property(GetProp(prop), label);
    }
    public void Property(SerializedProperty prop, string label="") {
        Add(new PropertyField(prop, label));
    }
    public void Button(string label, Action onClick) {
        var button = new Button(onClick);
        button.text = label;
        Add(button);
    }
    public VisualElement List(string propStr, Action<SerializedProperty, Action<Action<SerializedProperty>>> onPlus = null) {
        var container = new VisualElement();

        var list = GetProp(propStr);
        var entriesContainer = new VisualElement();
        container.Add(entriesContainer);
        Border(entriesContainer, 1);
        BorderColor(entriesContainer, Color.black);
        BorderRadius(entriesContainer, 5);
        entriesContainer.style.backgroundColor = new Color(0,0,0,0.1f);

        Action refreshList = () => {
            entriesContainer.Clear();
            var size = list.arraySize;
            for (var i = 0; i < size; i++) {
                var offset = i;
                var el = list.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = Color.black;
                row.style.alignItems = Align.FlexStart;
                entriesContainer.Add(row);

                var data = new PropertyField(el, "");
                Padding(data, 5);
                data.style.flexGrow = 1;
                row.Add(data);

                var remove = new Button(() => {
                    list.DeleteArrayElementAtIndex(offset);
                    Save();
                });
                remove.text = "x";
                remove.style.flexGrow = 0;
                row.Add(remove);
            }
            entriesContainer.Bind(list.serializedObject);
        };

        refreshList();
        container.Add(OnSizeChange(list, refreshList));

        var buttons = new VisualElement();
        container.Add(buttons);
        buttons.style.paddingLeft = StyleKeyword.Auto;
        var add = new Button(() => {
            if (onPlus != null) {
                onPlus(list, with => {
                    var newEl = addToList(list, with);
                });
            } else {
                addToList(list);
            }
        });
        add.text = "+";
        buttons.Add(add);

        return container;
    }

    public VisualElement Render() {
        if (stack.Count != 1) throw new Exception("Wrong stack size?");
        return stack.Peek();
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

    public void Save() {
        root.ApplyModifiedProperties();
    }

    public static VisualElement OnChange<Type>(SerializedProperty prop, Action changed) {
        var fakeField = new PropertyField(prop);
        fakeField.style.display = DisplayStyle.None;
        fakeField.RegisterCallback<ChangeEvent<Type>>(e => {
            changed();
        });
        return fakeField;
    }
    public static VisualElement OnSizeChange(SerializedProperty prop, Action changed) {
        var fakeField = new IntegerField();
        fakeField.bindingPath = prop.propertyPath+".Array.size";
        fakeField.style.display = DisplayStyle.None;
        fakeField.RegisterValueChangedCallback(e => {
            changed();
        });
        return fakeField;
    }

}
