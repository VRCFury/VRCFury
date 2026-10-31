using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Inspector {
    internal static class VRCFuryComponentHeader {
        private class Triangle : VisualElement {
            private readonly bool pointLeft;
            private readonly Color color;

            public Triangle(bool pointLeft, Color color) {
                this.pointLeft = pointLeft;
                this.color = color;
                style.width = 10;
                style.height = 20;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context) {
                var rect = contentRect;
                var mesh = context.Allocate(3, 3);
                void AddVertex(float x, float y) {
                    mesh.SetNextVertex(new Vertex {
                        position = new Vector3(x, y, Vertex.nearZ),
                        tint = color
                    });
                }
                if (pointLeft) {
                    AddVertex(rect.xMax, rect.yMin);
                    AddVertex(rect.xMax, rect.yMax);
                    AddVertex(rect.xMin, rect.yMax);
                } else {
                    AddVertex(rect.xMin, rect.yMin);
                    AddVertex(rect.xMax, rect.yMin);
                    AddVertex(rect.xMin, rect.yMax);
                }
                mesh.SetNextIndex(0);
                mesh.SetNextIndex(1);
                mesh.SetNextIndex(2);
            }
        }

        private static VisualElement FindEditor(VisualElement el) {
            if (el == null) return null;
            if (el is InspectorElement) return el.parent;
            return FindEditor(el.parent);
        }

        private static VisualElement RenderHeader(string title, bool overlay) {
            if (!overlay) {
                var l = new Label(title).Bold();
                l.style.marginTop = 10;
                return l;
            }
            
            var headerArea = new VisualElement {
                style = {
                    height = 20,
                    width = Length.Percent(100),
                    top = -21,
                    position = Position.Absolute,
                },
                pickingMode = PickingMode.Ignore
            };

            Color backgroundColor = EditorGUIUtility.isProSkin
                ? new Color32(61, 61, 61, 255)
                : new Color32(194, 194, 194, 255);
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 20,
                    backgroundColor = backgroundColor,
                    marginLeft = 18,
                    marginRight = 60,
                },
                pickingMode = PickingMode.Ignore
            };
            VRCFuryEditorUtils.HoverHighlight(row);
            headerArea.Add(row);

            var normalLabelColor = new Color(0.05f, 0.05f, 0.05f);
             
            var triangleLeft = new Triangle(true, normalLabelColor).FlexShrink(0);
            row.Add(triangleLeft);
            
            var label = new Label("VRCFury") {
                style = {
                    color = new Color(0.8f, 0.4f, 0f),
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0,
                    paddingLeft = 3,
                    paddingRight = 3,
                    backgroundColor = normalLabelColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexShrink = 1,
                },
                pickingMode = PickingMode.Ignore
            }.FlexShrink(0);
            row.Add(label);

            var triangleRight = new Triangle(false, normalLabelColor).FlexShrink(0);
            row.Add(triangleRight);

            var name = new Label(title) {
                style = {
                    //color = Color.white,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 3
                },
                pickingMode = PickingMode.Ignore
            }.FlexGrow(1);
            row.Add(name);

            var wrapper = new VisualElement();
            wrapper.Add(headerArea);

            return wrapper;
        }

        private static bool HasMultipleHeaders(VisualElement root) {
            if (root == null) return false;
            if (root.ClassListContains("vrcfMultipleHeaders")) return true;
            return HasMultipleHeaders(root.parent);
        }

        private static void AttachHeaderOverlay(VisualElement body, string title) {
            var inspectorRoot = FindEditor(body);

            if (HasMultipleHeaders(body) || inspectorRoot == null) {
                body.Add(RenderHeader(title, false));
                return;
            }

            var headerIndex = inspectorRoot.Children()
                .Select((e, i) => (element: e, index: i))
                .Where(x => x.element.name.EndsWith("Header"))
                .Select(x => x.index)
                .DefaultIfEmpty(-1)
                .First();

            if (headerIndex < 0) {
                body.Add(RenderHeader(title, false));
                return;
            }

            var headerArea = RenderHeader(title, true);
            headerArea.AddToClassList("vrcfHeaderOverlay");
            inspectorRoot.Insert(headerIndex+1, headerArea);
            
            body.RegisterCallback<DetachFromPanelEvent>(e => {
                headerArea.parent?.Remove(headerArea);
            });
        }

        public static VisualElement CreateHeaderOverlay(string title) {
            var el = new VisualElement();
            el.AddToClassList("vrcfHeader");
            el.RegisterCallback<AttachToPanelEvent>(e => {
                AttachHeaderOverlay(el, title);
            });

            return el;
        }
    }
}
