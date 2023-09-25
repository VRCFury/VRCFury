using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Inspector
{
    public class VRCFuryMaterialPropertySearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private Renderer[] _renderers;
        private SerializedProperty _targetProperty;
        
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Material Properties")));
            if (_renderers != null) {
                var singleRenderer = _renderers.Length == 1;
                foreach (var renderer in _renderers) {
                    var nest = 1;
                    var sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials.Length == 0) return entries;
                    var singleMaterial = sharedMaterials.Length == 1;
                    if (!singleRenderer) {
                        entries.Add(new SearchTreeGroupEntry(new GUIContent("Mesh: " + renderer.name), nest));
                    }
                    foreach (var material in sharedMaterials)
                    {
                        if (material != null)
                        {
                            nest = singleRenderer ? 1 : 2;
                            if (!singleMaterial) {
                                entries.Add(new SearchTreeGroupEntry(new GUIContent("Material: " + material.name),  nest));
                                nest++;
                            }
                            var shader = material.shader;
                            if (shader != null)
                            {
                                var count = ShaderUtil.GetPropertyCount(shader);
                                var materialProperties = MaterialEditor.GetMaterialProperties(new Object[]{ material });
                                for (var i = 0; i < count; i++)
                                {
                                    var propertyName = ShaderUtil.GetPropertyName(shader, i);
                                    var readableName = ShaderUtil.GetPropertyDescription(shader, i);
                                    var matProp = Array.Find(materialProperties, prop => prop.name == propertyName);
                                    if ((matProp.flags & MaterialProperty.PropFlags.HideInInspector) != 0) continue;
                                    
                                    var type = ShaderUtil.GetPropertyType(shader, i);
                                    if (type == ShaderUtil.ShaderPropertyType.Float || type == ShaderUtil.ShaderPropertyType.Range) {
                                        var prioritizePropName = readableName.Length > 25f;
                                        var entryName = prioritizePropName ? propertyName : readableName;
                                        if (!singleRenderer) {
                                            entryName += $" (Mesh: {renderer.name})";
                                        }
                                        if (!singleMaterial) {
                                            entryName += $" (Mat: {material.name})";
                                        }

                                        entryName += prioritizePropName ? $" ({readableName})" : $" ({propertyName})";
                                        entries.Add(new SearchTreeEntry(new GUIContent(entryName))
                                        {
                                            level = nest,
                                            userData = propertyName
                                        });
                                    }
                                }
                            }
                        }
                    }    
                }
            }
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            Debug.Log($"Selected {entry.userData}");
            _targetProperty.stringValue = (string) entry.userData;
            _targetProperty.serializedObject.ApplyModifiedProperties();
            return true;
        }

        public void InitProperties(Renderer renderer, SerializedProperty targetProperty)
        {
            _renderers = new [] { renderer };
            _targetProperty = targetProperty;
        }
        
        public void InitProperties(Renderer[] renderers, SerializedProperty targetProperty)
        {
            _renderers = renderers;
            _targetProperty = targetProperty;
        }
    }
}