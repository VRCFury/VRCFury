using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VRC.SDK3.Avatars.Components;
using AnimatorAsCode.V0;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditorInternal;

public abstract class BetterPropertyDrawer : PropertyDrawer {
    private bool printing = false;
    private Rect fullArea;
    private Rect renderArea;
    private GenericMenu rightClickMenu;
    private SerializedProperty renderingProp;
    private Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();

    protected ReorderableList makeList(SerializedProperty list, Action onAdd = null, Func<int,string> getLabel = null) {
        if (lists.ContainsKey(list.propertyPath)) {
            var old = lists[list.propertyPath];
            if (old.serializedProperty.serializedObject == list.serializedObject) {
                return old;
            }
        };

        var output = new ReorderableList(list.serializedObject, list) {
            displayAdd = true,
            displayRemove = true,
            draggable = true,
            drawElementCallback = (Rect rect, int index, bool active, bool focused) => {
                var sp = list.GetArrayElementAtIndex(index);
                rect.y += space;
                rect.height -= 2*space;
                if (getLabel != null) {
                    var label = new GUIContent(getLabel(index));
                    EditorGUI.PropertyField(rect, sp, label, true);
                } else {
                    EditorGUI.PropertyField(rect, sp, true);
                }
            },
            elementHeightCallback = (index) => {
                var sp = list.GetArrayElementAtIndex(index);
                float elHeight = 0;
                if (getLabel != null) {
                    var label = new GUIContent(getLabel(index));
                    elHeight = EditorGUI.GetPropertyHeight(sp, label, true);
                } else {
                    elHeight = EditorGUI.GetPropertyHeight(sp, true);
                }
                return elHeight + 2*space;
            },
            headerHeight = 1
        };
        if (onAdd != null) {
            output.onAddDropdownCallback = (rect, rl) => onAdd();
        }
        lists[list.propertyPath] = output;
        return output;
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
        fullArea = renderArea = new Rect(0,0,0,0);
        render(prop, label);
        return renderArea.y;
    }

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label) {
        //EditorGUI.DrawRect(pos, Color.red);
        fullArea = renderArea = pos;
        rightClickMenu = new GenericMenu();
        printing = true;
        renderingProp = prop;
        EditorGUI.BeginProperty(pos, GUIContent.none, prop);
        render(prop, label);
        EditorGUI.EndProperty();
        printing = false;
        if (rightClickMenu.GetItemCount() > 0) {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && fullArea.Contains(e.mousePosition)) {
                rightClickMenu.ShowAsContext();
            }
        }
    }

    protected abstract void render(SerializedProperty prop, GUIContent label);

    protected void renderProp(SerializedProperty prop, string label = "") {
        GUIContent content = null;
        if (label == "") {
            content = GUIContent.none;
        } else if (label != null) {
            content = new GUIContent();
            content.text = label;
        }
        renderProp(renderRect(EditorGUI.GetPropertyHeight(prop, content)), prop, label);
    }
    protected void renderProp(Rect pos, SerializedProperty prop, string label = "") {
        GUIContent content = null;
        if (label == "") {
            content = GUIContent.none;
        } else if (label != null) {
            content = new GUIContent();
            content.text = label;
        }
        renderThing(() => {
            EditorGUI.PropertyField(pos, prop, content);
        });
    }
    protected void renderList(ReorderableList list) {
        renderList(renderRect(list.GetHeight()), list);
    }
    protected void renderList(Rect pos, ReorderableList list) {
        renderThing(() => {
            list.DoList(pos);
        });
    }
    protected void renderLabel(string label) {
        renderLabel(renderRect(line), label);
    }
    protected void renderLabel(Rect pos, string label) {
        renderThing(() => {
            EditorGUI.LabelField(pos, label);
        });
    }
    protected void renderFoldout(SerializedProperty prop, string label) {
        renderFoldout(renderRect(line), prop, label);
    }
    protected void renderFoldout(Rect pos, SerializedProperty prop, string label) {
        renderThing(() => {
            prop.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(pos, prop.isExpanded, label);
            EditorGUI.EndFoldoutHeaderGroup();
        });
    }
    protected void renderButton(string label, Action onClick) {
        renderButton(renderRect(line), label, onClick);
    }
    protected void renderButton(Rect pos, string label, Action onClick) {
        renderThing(() => {
            if (GUI.Button(pos, label)) onClick();
        });
    }




    private void renderThing(Action doIt) {
        if (!printing) return;
        int indentBak = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        doIt();
        EditorGUI.indentLevel = indentBak;
    }
    protected Rect renderRect(float height) {
        var copy = renderArea;
        copy.height = height;
        renderArea.y += height;
        copy = EditorGUI.IndentedRect(copy);
        //EditorGUI.DrawRect(copy, Color.blue);
        return copy;
    }
    protected Rect[] renderFlex(float height, params float[] widths) {
        var full = renderRect(height);
        float leftoverWidth = full.width;
        float fractionTotal = 0;
        foreach (var width in widths) {
            if (width >= 5) leftoverWidth -= width;
            else fractionTotal += width;
        }
        var outRects = new List<Rect>();
        float xOffset = full.x;
        foreach (var width in widths) {
            float calculatedWidth = (width >= 5) ? width : leftoverWidth*(width/fractionTotal);
            outRects.Add(new Rect(xOffset, full.y, calculatedWidth, full.height));
            xOffset += calculatedWidth;
        }
        return outRects.ToArray();
    }
    protected void renderSpace(float height = -1) {
        if (height == -1) height = space;
        renderArea.y += height;
    }

    protected void addToList(SerializedProperty list, Action<SerializedProperty> doWith = null) {
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
    }

    protected void addToRightClickMenu(string label, Action fn) {
        addToRightClickMenu(label, false, fn);
    }
    protected void addToRightClickMenu(string label, bool selected, Action fn) {
        if (!printing) return;
        addToMenu(rightClickMenu, label, selected, fn);
    }
    protected void addToMenu(GenericMenu menu, string label, bool selected, Action fn) {
        menu.AddItem(new GUIContent(label), selected, () => {
            fn();
            renderingProp.serializedObject.ApplyModifiedProperties();
        });
    }

    protected static float line = EditorGUIUtility.singleLineHeight;
    protected static float space = EditorGUIUtility.standardVerticalSpacing;
}
