using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {
    internal class VRCFuryComponentEditor<T> : UnityEditor.Editor where T : VRCFuryComponent {
        private GameObject dummyObject;

        public sealed override VisualElement CreateInspectorGUI() {
            VisualElement content;
            try {
                content = CreateInspectorGUIUnsafe();
            } catch (Exception e) {
                Debug.LogException(new Exception("Failed to render editor", e));
                content = VRCFuryEditorUtils.Error("Failed to render editor (see unity console)");
            }

            var avatarObject = VRCAvatarUtils.GuessAvatarObject(target as UnityEngine.Component);
            var versionLabel = new Label(VrcfDebugLine.GetOutputString(avatarObject));
            versionLabel.AddToClassList("vfVersionLabel");
            
            var contentWithVersion = new VisualElement();
            contentWithVersion.styleSheets.Add(VRCFuryEditorUtils.GetResource<StyleSheet>("VRCFuryStyle.uss"));
            contentWithVersion.Add(versionLabel);
            contentWithVersion.Add(content);
            return contentWithVersion;
        }

        private VisualElement CreateInspectorGUIUnsafe() {
            if (!(target is UnityEngine.Component c)) {
                return VRCFuryEditorUtils.Error("This isn't a component?");
            }
            if (!(c is T v)) {
                return VRCFuryEditorUtils.Error("Unexpected type?");
            }

            var loadError = v.GetBrokenMessage();
            if (loadError != null) {
                return VRCFuryEditorUtils.Error(
                    $"This VRCFury component failed to load ({loadError}). Please update VRCFury." +
                    $" If this doesn't help, let us know on the discord at https://vrcfury.com/discord");
            }
            
            var isInstance = PrefabUtility.IsPartOfPrefabInstance(v);

            var container = new VisualElement();

            container.Add(CreateOverrideLabel());

            if (isInstance) {
                // We prevent users from adding overrides on prefabs, because it does weird things (at least in unity 2019)
                // when you apply modifications to an object that lives within a SerializedReference. Some properties not overridden
                // will just be thrown out randomly, and unity will dump a bunch of errors.
                container.Add(CreatePrefabInstanceLabel(v));
            }

            VisualElement body;
            if (isInstance) {
                var copy = CopyComponent(v);
                var copyGameObject = copy.owner();
                try {
                    VRCFury.RunningFakeUpgrade = true;
                    copy.Upgrade();
                    // Note that copy may be deleted here!
                } finally {
                    VRCFury.RunningFakeUpgrade = false;
                }
                // We need to prevent our added children from being bound to
                // the original component by unity
                body = new BindingBlock();
                body.SetEnabled(false);

                var children = copyGameObject.GetComponents<T>();
                if (children.Length != 1) body.Add(VRCFuryComponentHeader.CreateHeaderOverlay("Legacy Multi-Component"));
                foreach (var child in children) {
                    child.gameObjectOverride = v.owner();
                    var childSo = new SerializedObject(child);
                    var childEditor = _CreateEditor(childSo, child);
                    if (children.Length > 1) childEditor.AddToClassList("vrcfMultipleHeaders");
                    childEditor.Bind(childSo);
                    body.Add(childEditor); 
                }
            } else {
                v.Upgrade();
                if (v == null) return new VisualElement();
                serializedObject.Update();
                body = _CreateEditor(serializedObject, v);
            }
            
            container.Add(body);

            var editingPrefab = UnityCompatUtils.IsEditingPrefab();

            container.Add(VRCFuryEditorUtils.Debug(refreshElement: () => {
                var warning = new VisualElement();

                if (c == null) return warning;

                var descriptors = c.owner().GetComponentsInSelfAndParents<VRCAvatarDescriptor>();
                if (!editingPrefab && !descriptors.Any()) {
                    var animators = c.owner().GetComponentsInSelfAndParents<Animator>();
                    if (animators.Any()) {
                        warning.Add(VRCFuryEditorUtils.Error(
                            "Your avatar does not have a VRC Avatar Descriptor, and thus this component will not do anything! " +
                            "Make sure that your avatar can actually be uploaded using the VRCSDK before attempting to add VRCFury things to it."));
                    } else {
                        warning.Add(VRCFuryEditorUtils.Error(
                            "This VRCFury component is not placed on an avatar, and thus will not do anything! " +
                            "If you intended to include this in your avatar, make sure you've placed it within your avatar's " +
                            "object, and not just alongside it in the scene."));
                    }
                }

                if (descriptors.Length > 1) {
                    warning.Add(VRCFuryEditorUtils.Error(
                        "There are multiple avatar descriptors in this hierarchy. Each avatar should only have one avatar descriptor on the avatar root." +
                        " This may cause issues in this inspector or during your avatar build.\n\n" + descriptors.Select(d => d.owner().GetPath()).Join('\n')));
                }

                var hasDelete = v is VRCFury z && z.GetAllFeatures().OfType<DeleteDuringUpload>().Any();
                var isDeleted = EditorOnlyUtils.IsInsideEditorOnly(c.owner());
                if (isDeleted && !hasDelete) {
                    warning.Add(VRCFuryEditorUtils.Error(
                        "This VRCFury component is placed within an object that is tagged as EditorOnly or has a vrcfury 'Delete During Upload' component, and thus will not do anything!"));
                }
                
                return warning;
            }));

            return container;
        }

        private C CopyComponent<C>(C original) where C : UnityEngine.Component {
            OnDestroy();
            dummyObject = new GameObject();
            dummyObject.SetActive(false);
            dummyObject.hideFlags |= HideFlags.HideAndDontSave;
            var copy = dummyObject.AddComponent<C>();
            UnitySerializationUtils.CloneSerializable(original, copy);
            return copy;
        }

        public void OnDestroy() {
            if (dummyObject) {
                DestroyImmediate(dummyObject);
            }
        }
        
        private VisualElement _CreateEditor(SerializedObject serializedObject, T target) {
            if (target is VRCFury) {
                return CreateEditor(serializedObject, target);
            }

            var output = new VisualElement();
            var type = target.GetType();
            var attr = type.GetCustomAttribute<AddComponentMenu>();
            string title;
            if (attr != null) {
                title = attr.componentMenu;
                title = Regex.Replace(title, @".*/", "");
                title = Regex.Replace(title, @"^vrcfury[^a-zA-Z0-9]*", "", RegexOptions.IgnoreCase);
            } else {
                title = target.GetType().Name;
                title = Regex.Replace(title, @"(a-z)([A-Z])", "$1 $2");
            }
            output.Add(VRCFuryComponentHeader.CreateHeaderOverlay(title));
            output.Add(CreateEditor(serializedObject, target));
            return output;
        }

        protected virtual VisualElement CreateEditor(SerializedObject serializedObject, T target) {
            return new VisualElement();
        }
        
        private VisualElement CreateOverrideLabel() {
            var baseText = "The VRCFury features in this prefab are overridden on this instance. Please revert them!" +
                           " If you apply, it may corrupt data in the changed features.";
            var overrideLabel = VRCFuryEditorUtils.Error(baseText);
            overrideLabel.SetVisible(false);

            void CheckOverride() {
                var vrcf = target as VRCFuryComponent;
                if (vrcf == null) return;

                var mods = VRCFPrefabFixer.GetModifications(vrcf);
                var isModified = mods.Count > 0;
                overrideLabel.SetVisible(isModified);
                if (isModified) {
                    overrideLabel.Clear();
                    overrideLabel.Add(VRCFuryEditorUtils.WrappedLabel(baseText + "\n\n" + mods.Select(m => m.propertyPath).Join(", ")));
                }
            }

            //overrideLabel.schedule.Execute(CheckOverride).Every(1000);
            CheckOverride();

            return overrideLabel;
        }

        private VisualElement CreatePrefabInstanceLabel(UnityEngine.Component component) {
            void Open() {
                var componentInBasePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
                var prefabPath = AssetDatabase.GetAssetPath(componentInBasePrefab);
                UnityCompatUtils.OpenPrefab(prefabPath, component.owner());
            }

            var row = new VisualElement().Row();
            row.Add(new VisualElement().FlexGrow(1));

            var label = new Button()
                .OnClick(Open)
                .Text("Edit in Prefab")
                .TextAlign(TextAnchor.MiddleCenter)
                .TextWrap()
                .Padding(3, 5)
                .BorderColor(Color.black)
                .BorderRadius(5)
                .Margin(0, 10)
                .Border(1);
            label.style.borderTopRightRadius = 0;
            label.style.borderTopLeftRadius = 0;
            label.style.marginTop = -2;
            label.style.borderTopWidth = 0;
            row.Add(label);
            return row;
        }
    }
}
