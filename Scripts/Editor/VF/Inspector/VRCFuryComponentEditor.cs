using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using Color = UnityEngine.Color;
using FontStyle = UnityEngine.FontStyle;
using Image = UnityEngine.UIElements.Image;

namespace VF.Inspector {
    public class VRCFuryComponentEditor : Editor {

        public override bool UseDefaultMargins() {
            return false;
        }
        
        private Action detachChanges = null;

        private void Detach() {
            if (detachChanges != null) detachChanges();
            detachChanges = null;
        }

        private void CreateHeaderOverlay(VisualElement el) {
            Detach();
            
            var inspectorRoot = el.parent?.parent;
            if (inspectorRoot == null) return;

            var header = inspectorRoot.Children().ToList()
                .FirstOrDefault(c => c.name.EndsWith("Header"));
            var footer = inspectorRoot.Children().ToList()
                .FirstOrDefault(c => c.name.EndsWith("Footer"));
            if (header != null) header.style.display = DisplayStyle.None;
            if (footer != null) footer.style.display = DisplayStyle.None;

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 21,
                    borderTopWidth = 1,
                    borderTopColor = Color.black,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f),
                }
            };
            VRCFuryEditorUtils.HoverHighlight(row);
            var normalLabelColor = new Color(0.05f, 0.05f, 0.05f);
            var label = new Label("VRCF") {
                style = {
                    color = new Color(0.8f, 0.4f, 0f),
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0,
                    paddingLeft = 10,
                    paddingRight = 7,
                    backgroundColor = normalLabelColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    flexShrink = 1,
                }
            };
            row.Add(label);

            row.Add(new VisualElement {
                style = {
                    borderLeftColor = normalLabelColor,
                    borderTopColor = normalLabelColor,
                    borderLeftWidth = 5,
                    borderTopWidth = 10,
                    borderRightWidth = 5,
                    borderBottomWidth = 10,
                }
            });

            var name = new Label("Toggle") {
                style = {
                    //color = Color.white,
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 5
                }
            };
            row.Add(name);
            void ContextMenu(Vector2 pos) {
                var displayMethod = typeof(EditorUtility)
                    .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Where(method => method.Name == "DisplayObjectContextMenu")
                    .Where(method => method.GetParameters()[1].ParameterType.IsArray)
                    .First();
                displayMethod?.Invoke(null, new object[] {
                    new Rect(pos.x, pos.y, 0.0f, 0.0f),
                    targets,
                    0
                });
            }
            row.RegisterCallback<MouseUpEvent>(e => {
                if (e.button == 0) {
                    Debug.Log("Toggle " + target);
                    e.StopImmediatePropagation();
                    InternalEditorUtility.SetIsInspectorExpanded(target, !InternalEditorUtility.GetIsInspectorExpanded(target));
                }
                if (e.button == 1) {
                    e.StopImmediatePropagation();
                    ContextMenu(e.mousePosition);
                }
            });
            {
                var up = new Label("↑") {
                    style = {
                        width = 16,
                        height = 16,
                        marginTop = 2,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    }
                };
                up.RegisterCallback<MouseUpEvent>(e => {
                    if (e.button != 0) return;
                    e.StopImmediatePropagation();
                    ComponentUtility.MoveComponentUp(target as Component);
                });
                row.Add(up);
                VRCFuryEditorUtils.HoverHighlight(up);
            }
            {
                var menu = new Image {
                    image = EditorGUIUtility.FindTexture("_Menu@2x"),
                    scaleMode = ScaleMode.StretchToFill,
                    style = {
                        width = 16,
                        height = 16,
                        marginTop = 2,
                        marginRight = 4,
                    }
                };
                menu.RegisterCallback<MouseUpEvent>(e => {
                    if (e.button != 0) return;
                    e.StopImmediatePropagation();
                    ContextMenu(e.mousePosition);
                });
                row.Add(menu);
                VRCFuryEditorUtils.HoverHighlight(menu);
            }

            detachChanges = () => {
                if (header != null) header.style.display = DisplayStyle.Flex;
                if (footer != null) footer.style.display = DisplayStyle.Flex;
                row.parent.Remove(row);
            };

            inspectorRoot.Insert(0, row);
        }

        public sealed override VisualElement CreateInspectorGUI() {
            var el = CreateEditor();
            el.RegisterCallback<AttachToPanelEvent>(e => {
                CreateHeaderOverlay(el);
            });
            el.RegisterCallback<DetachFromPanelEvent>(e => {
                Detach();
            });
            el.style.marginBottom = 4;
            return el;
        }
        
        public virtual VisualElement CreateEditor() {
            return new VisualElement();
        }
    }
}
