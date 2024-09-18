
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using VF.Utils;

namespace VF.Injector {
    /**
     * Poor-mans Inversion of Control, because I don't want to have to import
     * an entire DI library into VRCF.
     */
    internal class VRCFuryInjector {
        private readonly Dictionary<(VFInjectorParent,Type), object> completedObjects = new Dictionary<(VFInjectorParent,Type), object>();
        private readonly Dictionary<string, object> qualifiedObjects = new Dictionary<string, object>();

        private readonly ISet<Type> serviceTypes = new HashSet<Type>();

        public void ImportScan(Type type) {
            serviceTypes.UnionWith(ReflectionUtils.GetTypes(type));
        }
        
        public void ImportOne(Type type) {
            serviceTypes.Add(type);
        }

        public void Set(object service) {
            Set(service.GetType(), service);
        }
        
        public void Set(Type type, object service) {
            if (completedObjects.ContainsKey((null, type))) {
                throw new Exception("Service of type " + type.Name + " already set");
            }
            completedObjects[(null, type)] = service;
        }
        
        public void Set(string name, object service) {
            qualifiedObjects[name] = service;
        }

        private static bool IsPrototypeScope(Type type) {
            return type.GetCustomAttribute<VFPrototypeScopeAttribute>() != null;
        }
        
        public T CreateAndFillObject<T>() {
            return (T)CreateAndFillObject(typeof(T));
        }

        public object CreateAndFillObject(Type type, Context context = default) {
            var nextParentHolder = new VFInjectorParent();
            var parent = context.nearestNonPrototypeParent;
            if (context.nearestNonPrototypeParent == null) context.nearestNonPrototypeParent = nextParentHolder;

            var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            var args = constructor.GetParameters().Select(p => Get(p, context)).ToArray();
            var instance = Activator.CreateInstance(type, args);
            nextParentHolder.parent = instance;
            
            object GetField(FieldInfo field) {
                if (field.FieldType == typeof(VFInjectorParent) && parent != null) return parent;
                return Get(field, context);
            }
            foreach (var field in ReflectionUtils.GetAllFields(type)) {
                if (field.GetCustomAttribute<VFAutowiredAttribute>() == null) continue;
                field.SetValue(instance, GetField(field));
            }

            return instance;
        }

        public object FillMethod(MethodInfo method, object obj = null) {
            return method.Invoke(obj, method.GetParameters().Select(p => Get(p)).ToArray());
        }
        public void VerifyMethod(MethodInfo method) {
            method.GetParameters().Select(p => Get(p)).ToArray();
        }

        public object Get(ParameterInfo p, Context context = default) {
            context.fieldName = p.Name;
            context.nullable = p.GetCustomAttribute<CanBeNullAttribute>() != null;
            return GetService(p.ParameterType, context);
        }
        public object Get(FieldInfo p, Context context = default) {
            context.fieldName = p.Name;
            context.nullable = p.GetCustomAttribute<CanBeNullAttribute>() != null;
            return GetService(p.FieldType, context);
        }

        private object GetService(Type type, Context context = default) {
            try {
                var listItemType = ReflectionUtils.GetGenericArgument(type, typeof(IList<>));
                if (listItemType != null) {
                    var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(listItemType));
                    foreach (var s in GetServices(listItemType)) {
                        list.GetType().GetMethod("Add").Invoke(list, new object[] { s });
                    }
                    return list;
                }
                
                var nearestNonPrototypeParent = context.nearestNonPrototypeParent;
                var isPrototypeScope = IsPrototypeScope(type);
                if (!isPrototypeScope) nearestNonPrototypeParent = null;
                if (completedObjects.TryGetValue((nearestNonPrototypeParent, type), out var finished)) {
                    return finished;
                }
                if (context.fieldName != null && qualifiedObjects.TryGetValue(context.fieldName, out var finished2)) {
                    return finished2;
                }

                if (!serviceTypes.Contains(type)) {
                    if (context.nullable) return null;
                    throw new Exception($"{type.FullName} {context.fieldName} was not found in this injector");
                }
                
                var parents = new List<Type>();
                if (context.parents != null) {
                    if (context.parents.Contains(type)) {
                        throw new Exception($"{type.FullName} is already being constructed (dependency loop?) {string.Join(",", context.parents)}");
                    }
                    parents.AddRange(context.parents);
                }
                parents.Add(type);

                var instance = CreateAndFillObject(type, new Context() { parents = parents, nearestNonPrototypeParent = nearestNonPrototypeParent });

                completedObjects[(nearestNonPrototypeParent, type)] = instance;

                return instance;
            } catch(Exception e) {
                throw new Exception($"Error while constructing {type.FullName} service", e);
            }
        }

        public T[] GetServices<T>() {
            return GetServices(typeof(T)).OfType<T>().ToArray();
        }
        
        public object[] GetServices(Type type, Context context = default) {
            return serviceTypes
                .Where(type.IsAssignableFrom)
                .Where(t => !IsPrototypeScope(t))
                .Select(t => GetService(t, context))
                .ToArray();
        }

        public T GetService<T>() where T : class {
            return GetService(typeof(T)) as T;
        }

        public struct Context {
            public List<Type> parents;
            public VFInjectorParent nearestNonPrototypeParent;
            public string fieldName;
            public bool nullable;
        }
    }
}
