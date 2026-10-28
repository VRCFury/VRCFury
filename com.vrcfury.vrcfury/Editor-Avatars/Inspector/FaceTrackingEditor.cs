#if VRCSDK_HAS_FACE_TRACKING
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;

namespace VF.Inspector {
    internal static class FaceTrackingEditor {
        public static VisualElement Create(SerializedProperty meshes, SerializedProperty sets, SerializedProperty behaviors) {
            var output = new Foldout {
                text = "Face Tracking",
                value = meshes.arraySize > 0 || sets.arraySize > 0 || behaviors.arraySize > 0
            };
            output.Add(VRCFuryEditorUtils.Warn(
                "If you're adding an accessory to the avatar's face, use Blendshape Link instead. " +
                "Do NOT add face tracking for the accessory in this section."));
            var meshSection = VRCFuryEditorUtils.Section("Face Tracking Meshes").AddTo(output);
            meshSection.Add(ObjectReferenceDrawer.List(meshes, new ObjectReferenceDrawer.Options {
                defaultMode = ObjectReference.Mode.ObjectReference,
                allowBone = false,
                componentType = typeof(SkinnedMeshRenderer)
            }));
            var setsSection = VRCFuryEditorUtils.Section("Face Tracking Blendshape Sets").AddTo(output);
            setsSection.Add(new BlendshapeSetsEditor(sets, meshes));
            var behaviorsSection = VRCFuryEditorUtils.Section("Face Tracking Parameter Behaviours").AddTo(output);
            behaviorsSection.Add(ClipboardButtons(behaviors, true));
            behaviorsSection.Add(VRCFuryEditorUtils.List(behaviors, renderItem: (item, index) => {
                var entry = new VisualElement();
                var copy = CopyButton(item.Copy(), true, "Copy");
                copy.style.alignSelf = Align.FlexEnd;
                entry.Add(copy);
                entry.Add(Field(item, () => new FaceTrackingParameterBehaviorDrawer(), meshes));
                return entry;
            }));
            return output;
        }

        internal static string Serialize(SerializedProperty property, bool behaviors) {
            var entries = property.isArray
                ? Enumerable.Range(0, property.arraySize).Select(property.GetArrayElementAtIndex)
                : new[] { property };
            return behaviors
                ? FaceTrackingClipboard.SerializeParameterBehaviors(entries.Select(entry => (VRCAvatarDescriptor.FaceTrackingParameterBehavior)entry.boxedValue))
                : FaceTrackingClipboard.SerializeBlendshapesSets(entries.Select(entry => (VRCAvatarDescriptor.FaceTrackingBlendshapesSet)entry.boxedValue));
        }

        internal static bool Paste(SerializedProperty list, bool behaviors, string json, out string error) {
            System.Collections.IList values;
            if (behaviors) {
                if (!FaceTrackingClipboard.TryParseParameterBehaviors(json, out var parsed, out error)) return false;
                values = parsed;
            } else {
                if (!FaceTrackingClipboard.TryParseBlendshapesSets(json, out var parsed, out error)) return false;
                values = parsed;
            }
            var max = behaviors ? FaceTrackingUtils.MaxParameterBehaviors : FaceTrackingUtils.MaxBlendshapeSets;
            if (values.Count > max - list.arraySize) {
                error = $"Cannot paste {values.Count} configurations here. The maximum is {max}; no configurations were pasted.";
                return false;
            }
            Undo.SetCurrentGroupName(behaviors ? "Paste Parameter Behaviors" : "Paste Blendshapes Sets");
            var first = list.arraySize;
            list.arraySize += values.Count;
            for (var i = 0; i < values.Count; i++) {
                var entry = list.GetArrayElementAtIndex(first + i);
                entry.boxedValue = values[i];
                entry.isExpanded = true;
                FaceTrackingEditorContext.ForProperty(entry).SourceAvatar = null;
            }
            list.serializedObject.ApplyModifiedProperties();
            return true;
        }

        private static Button CopyButton(SerializedProperty property, bool behaviors, string text) {
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            return new Button(() => {
                var current = serialized.FindProperty(path);
                if (current != null) EditorGUIUtility.systemCopyBuffer = Serialize(current, behaviors);
            }) { text = text };
        }

        private static VisualElement ClipboardButtons(SerializedProperty property, bool behaviors,
            Func<int> selected = null, Action<int> onPasted = null) {
            var list = property.Copy();
            var header = new VisualElement { style = {
                flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.FlexEnd
            } };
            var copy = CopyButton(list, behaviors, "Copy All");
            header.Add(copy);
            var copySelected = selected == null ? null : new Button(() => {
                EditorGUIUtility.systemCopyBuffer = Serialize(list.GetArrayElementAtIndex(selected()), behaviors);
            }) { text = "Copy Selected" };
            if (copySelected != null) header.Add(copySelected);
            var paste = new Button(() => {
                var first = list.arraySize;
                if (Paste(list, behaviors, EditorGUIUtility.systemCopyBuffer, out var error)) onPasted?.Invoke(first);
                else DialogUtils.DisplayDialog("Paste Face Tracking", error, "OK");
            }) { text = "Paste" };
            header.Add(paste);
            void UpdateButtons() {
                copy.SetEnabled(list.arraySize > 0);
                copySelected?.SetEnabled(list.arraySize > 0);
                paste.SetEnabled(list.arraySize < (behaviors ? FaceTrackingUtils.MaxParameterBehaviors : FaceTrackingUtils.MaxBlendshapeSets));
            }
            UpdateButtons();
            header.TrackPropertyValue(list, _ => UpdateButtons());
            return header;
        }

        internal sealed class BlendshapeSetsEditor : VisualElement {
            private readonly SerializedProperty sets;
            private readonly SerializedProperty meshes;
            private readonly DropdownField selector = new DropdownField("Selected Blendshapes Set");
            private readonly VisualElement body = new VisualElement();
            private readonly Button add;
            private readonly Button remove;
            private readonly Button up;
            private readonly Button down;
            private int displayedIndex = -1;
            private int displayedCount = -1;
            internal int SelectedIndex { get; private set; }

            public BlendshapeSetsEditor(SerializedProperty sets, SerializedProperty meshes) {
                this.sets = sets.Copy();
                this.meshes = meshes.Copy();
                Add(ClipboardButtons(this.sets, false, () => SelectedIndex, Select));
                Add(selector);
                selector.RegisterValueChangedCallback(_ => Select(selector.index));
                var controls = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                add = new Button(AddSet) { text = "+", tooltip = "Add a blendshape set" };
                remove = new Button(RemoveSet) { text = "-", tooltip = "Remove the selected blendshape set" };
                up = new Button(() => Move(-1)) { text = "Move Up" };
                down = new Button(() => Move(1)) { text = "Move Down" };
                controls.Add(add);
                controls.Add(remove);
                controls.Add(up);
                controls.Add(down);
                Add(controls);
                Add(body);
                Refresh();
                this.TrackPropertyValue(this.sets, _ => Refresh());
            }

            internal void Select(int index) {
                SelectedIndex = index;
                Refresh();
            }

            internal void Refresh(bool force = false) {
                SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Math.Max(0, sets.arraySize - 1));
                selector.choices = Enumerable.Range(0, sets.arraySize).Select(i => {
                    var name = sets.GetArrayElementAtIndex(i).FindPropertyRelative("setName").stringValue;
                    return string.IsNullOrEmpty(name) ? (i + 1).ToString() : $"{i + 1}: {name}";
                }).ToList();
                selector.SetValueWithoutNotify(sets.arraySize > 0 ? selector.choices[SelectedIndex] : "");
                selector.SetEnabled(sets.arraySize > 0);
                add.SetEnabled(sets.arraySize < FaceTrackingUtils.MaxBlendshapeSets);
                remove.SetEnabled(sets.arraySize > 0);
                up.SetEnabled(sets.arraySize > 0 && SelectedIndex > 0);
                down.SetEnabled(SelectedIndex < sets.arraySize - 1);
                // Updating a set's name must not recreate its editor and steal keyboard focus.
                if (!force && displayedIndex == SelectedIndex && displayedCount == sets.arraySize) return;
                body.Clear();
                if (sets.arraySize > 0) {
                    body.Add(Field(sets.GetArrayElementAtIndex(SelectedIndex), () => new FaceTrackingBlendshapesSetDrawer(), meshes));
                } else {
                    body.Add(VRCFuryEditorUtils.WrappedLabel("No blendshape sets. Click + or Paste to add one."));
                }
                displayedIndex = SelectedIndex;
                displayedCount = sets.arraySize;
            }

            private void Change(string name, Action action) {
                Undo.SetCurrentGroupName(name);
                action();
                sets.serializedObject.ApplyModifiedProperties();
                Refresh(true);
            }

            internal void AddSet() {
                if (sets.arraySize >= FaceTrackingUtils.MaxBlendshapeSets) return;
                var name = "unnamed".MakeUnique(Enumerable.Range(0, sets.arraySize)
                    .Select(i => sets.GetArrayElementAtIndex(i).FindPropertyRelative("setName").stringValue), FaceTrackingUtils.MaxSetNameLength);
                Change("Add Blendshapes Set", () => {
                    SelectedIndex = sets.arraySize++;
                    sets.GetArrayElementAtIndex(SelectedIndex).boxedValue = FaceTrackingUtils.CreateBlendshapeSet(name);
                });
            }

            internal void RemoveSet() {
                if (sets.arraySize == 0) return;
                Change("Remove Blendshapes Set", () => sets.DeleteArrayElementAtIndex(SelectedIndex));
            }

            internal void Move(int offset) {
                var destination = SelectedIndex + offset;
                if (destination < 0 || destination >= sets.arraySize) return;
                Change("Move Blendshapes Set", () => {
                    sets.MoveArrayElement(SelectedIndex, destination);
                    SelectedIndex = destination;
                });
            }
        }

        internal static VisualElement Field(SerializedProperty property, Func<PropertyDrawer> createDrawer, SerializedProperty meshes) {
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            var arrayStart = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            var arrayPath = arrayStart >= 0 && path.EndsWith("]", StringComparison.Ordinal) ? path.Substring(0, arrayStart) : null;
            var arraySize = -1;
            PropertyDrawer drawer = null;
            SerializedProperty ResolveField() {
                var field = serialized.FindProperty(path);
                var size = arrayPath == null ? 0 : serialized.FindProperty(arrayPath)?.arraySize ?? 0;
                // Rows can repaint after deletion but before ListView rebinding. Never retain element handles.
                if (field == null || size != arraySize) {
                    drawer = null;
                    arraySize = size;
                }
                if (field != null && drawer == null) drawer = createDrawer();
                return field;
            }
            var meshList = meshes.Copy();
            SkinnedMeshRenderer[] ReadMeshes() {
                var avatarRoot = ObjectReferenceDrawer.GetAvatarRoot(meshList);
                return Enumerable.Range(0, meshList.arraySize)
                    .Select(i => ObjectReferenceDrawer.Resolve(meshList.GetArrayElementAtIndex(i), avatarRoot)?.GetComponent<SkinnedMeshRenderer>())
                    .OfType<SkinnedMeshRenderer>().Distinct().ToArray();
            }
            var context = FaceTrackingEditorContext.ForProperty(property);
            // Override automatic parent-avatar inference as well as any saved authoring avatar.
            context.SourceAvatar = null;
            var currentMeshes = ReadMeshes();
            context.SetMeshes(currentMeshes);
            void UpdateMeshes() {
                serialized.UpdateIfRequiredOrScript();
                var nextMeshes = ReadMeshes();
                if (!currentMeshes.SequenceEqual(nextMeshes)) {
                    currentMeshes = nextMeshes;
                    context.SetMeshes(currentMeshes);
                }
            }
            var container = new IMGUIContainer(() => {
                serialized.UpdateIfRequiredOrScript();
                var field = ResolveField();
                if (field == null) return;
                if (context.SourceAvatar != null) context.SourceAvatar = null;
                var rect = EditorGUILayout.GetControlRect(false, drawer.GetPropertyHeight(field, GUIContent.none));
                drawer.OnGUI(rect, field, GUIContent.none);
                serialized.ApplyModifiedProperties();
            });
            container.TrackPropertyValue(meshList, _ => UpdateMeshes());
            container.RegisterCallback<AttachToPanelEvent>(_ => {
                UpdateMeshes();
                EditorApplication.hierarchyChanged += UpdateMeshes;
            });
            container.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.hierarchyChanged -= UpdateMeshes);
            return container;
        }
    }
}
#endif
