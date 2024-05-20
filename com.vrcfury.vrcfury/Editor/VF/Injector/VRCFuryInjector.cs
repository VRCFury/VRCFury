
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VF.Injector {
    /**
     * Poor-mans Inversion of Control, because I don't want to have to import
     * an entire DI library into VRCF.
     */
    public class VRCFuryInjector {
        private readonly Dictionary<Type, object> completedObjects = new Dictionary<Type, object>();

        public void SetService(object service) {
            if (completedObjects.ContainsKey(service.GetType())) {
                throw new Exception("Service of type " + service.GetType() + " already set");
            }
            completedObjects[service.GetType()] = service;
        }

        private static bool IsPrototypeScope(Type type) {
            return type.GetCustomAttribute<VFPrototypeScopeAttribute>() != null;
        }

        public object GetService(Type type, List<Type> _parents = null, VFInjectorParent parentHolder = null, bool useCache = true) {
            try {
                var parents = new List<Type>();
                if (_parents != null) {
                    if (_parents.Contains(type)) {
                        throw new Exception($"{type.FullName} is already being constructed (dependency loop?) {string.Join(",", _parents)}");
                    }
                    parents.AddRange(_parents);
                }
                parents.Add(type);

                var isPrototypeScope = !useCache || IsPrototypeScope(type);
                if (completedObjects.TryGetValue(type, out var finished) && !isPrototypeScope) {
                    return finished;
                }

                var nextParentHolder = new VFInjectorParent();
                object GetField(Type type) {
                    if (isPrototypeScope && type == typeof(VFInjectorParent) && parentHolder != null) return parentHolder;
                    return GetService(type, parents, isPrototypeScope && parentHolder != null ? parentHolder : nextParentHolder);
                }

                var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var args = constructor.GetParameters()
                    .Select(p => GetField(p.ParameterType))
                    .ToArray();
                var instance = Activator.CreateInstance(type, args);
                nextParentHolder.parent = instance;
                foreach (var field in ReflectionUtils.GetAllFields(type)) {
                    if (field.GetCustomAttribute<VFAutowiredAttribute>() == null) continue;
                    field.SetValue(instance, GetField(field.FieldType));
                }

                if (!isPrototypeScope) {
                    SetService(instance);
                }
                return instance;
            } catch(Exception e) {
                throw new Exception($"Error while constructing {type.FullName} service\n" + e.Message, e);
            }
        }

        public object[] GetAllServices() {
            return completedObjects.Values.ToArray();
        }

        public T GetService<T>() where T : class {
            return GetService(typeof(T)) as T;
        }
    }
}
