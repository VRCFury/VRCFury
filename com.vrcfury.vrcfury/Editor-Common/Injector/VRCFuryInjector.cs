
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
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly Dictionary<string, object> qualifiedObjects = new Dictionary<string, object>();

        private readonly Dictionary<Type, ISet<Type>> serviceTypesByParent = new Dictionary<Type, ISet<Type>>();

        public void ImportScan(Type type) {
            foreach (var serviceType in ReflectionUtils.GetTypes(type)) {
                ImportOne(serviceType);
            }
        }

        public void ImportOne(Type type) {
            foreach (var parent in GetAllParents(type)) {
                AddServiceType(parent, type);
            }
        }

        private void AddServiceType(Type parent, Type type) {
            if (!serviceTypesByParent.TryGetValue(parent, out var implementations)) {
                implementations = new HashSet<Type>();
                serviceTypesByParent[parent] = implementations;
            }
            implementations.Add(type);
        }

        private static IEnumerable<Type> GetAllParents(Type type) {
            for (var parent = type; parent != null; parent = parent.BaseType) {
                yield return parent;
            }
            foreach (var parent in type.GetInterfaces()) {
                yield return parent;
            }
        }

        public void Set(object service) {
            Set(service.GetType(), service);
        }
        
        public void Set(Type type, object service) {
            if (services.ContainsKey(type)) {
                throw new Exception("Service of type " + type.Name + " already set");
            }
            services[type] = service;
            foreach (var parent in GetAllParents(type)) {
                AddServiceType(parent, type);
            }
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
            var parent = context.scope;
            if (context.scope == null) context.scope = nextParentHolder;

            var constructor = type.GetConstructors().FirstOrDefault(c => c.GetCustomAttribute<VFAutowiredAttribute>() != null) ??
                type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
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

        private object Get(ParameterInfo p, Context context = default) {
            context.fieldName = p.Name;
            context.nullable = p.GetCustomAttribute<CanBeNullAttribute>() != null;
            return GetService(p.ParameterType, context);
        }
        private object Get(FieldInfo p, Context context = default) {
            context.fieldName = p.Name;
            context.nullable = p.GetCustomAttribute<CanBeNullAttribute>() != null;
            return GetService(p.FieldType, context);
        }

        private object GetService(Type type, Context context = default) {
            try {
                var listItemType = ReflectionUtils.GetGenericArgument(type, typeof(IList<>));
                if (listItemType != null) {
                    var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(listItemType));
                    foreach (var s in GetServices(listItemType, context, includeScoped: false)) {
                        list.GetType().VFMethod("Add").Invoke(list, new object[] { s });
                    }
                    return list;
                }

                if (context.fieldName != null && qualifiedObjects.TryGetValue(context.fieldName, out var finished2)) {
                    if (finished2 != null && !type.IsInstanceOfType(finished2)) {
                        throw new Exception($"{context.fieldName} was set to {finished2.GetType().FullName}, but {type.FullName} was requested");
                    }
                    return finished2;
                }

                var implementations = GetServices(type, context, includeScoped: true, requireSingle: true);
                if (implementations.Length == 1) return implementations[0];
                if (context.nullable && implementations.Length == 0) {
                    return null;
                }
                throw new Exception($"{type.FullName} {context.fieldName} was not found in this injector");
            } catch(Exception e) {
                throw new Exception($"Error while constructing {type.FullName} service", e);
            }
        }

        private object GetConcreteService(Type type, Context context) {
            try {
                var isPrototypeScope = IsPrototypeScope(type);
                if (services.TryGetValue(type, out var finished)) return finished;

                var cache = isPrototypeScope ? context.scope?.scopedServices : null;
                if (cache != null && cache.TryGetValue(type, out var cached)) return cached;
                
                var parents = new List<Type>();
                if (context.parents != null) {
                    if (context.parents.Contains(type)) {
                        throw new Exception($"{type.FullName} is already being constructed (dependency loop?) {context.parents.Select(t => t.Name).Join(',')}");
                    }
                    parents.AddRange(context.parents);
                }
                parents.Add(type);

                var instance = CreateAndFillObject(type, new Context() {
                    parents = parents,
                    scope = isPrototypeScope ? context.scope : null
                });

                if (isPrototypeScope) {
                    if (cache != null) cache[type] = instance;
                } else {
                    services[type] = instance;
                }

                return instance;
            } catch(Exception e) {
                throw new Exception($"Error while constructing {type.FullName} service", e);
            }
        }

        private ICollection<Type> GetServiceTypes(Type type) {
            return serviceTypesByParent.GetOrDefault(type) ?? (ICollection<Type>) Array.Empty<Type>();
        }

        public T[] GetServices<T>() {
            return GetServices(typeof(T), new Context(), includeScoped: false).OfType<T>().ToArray();
        }

        private object[] GetServices(Type type, Context context, bool includeScoped, bool requireSingle = false) {
            var serviceTypes = GetServiceTypes(type)
                .Where(t => includeScoped || !IsPrototypeScope(t))
                .ToArray();
            if (requireSingle && serviceTypes.Length > 1) {
                throw new Exception($"{type.FullName} {context.fieldName} has multiple implementations in this injector");
            }
            return serviceTypes
                .Select(t => GetConcreteService(t, context))
                .ToArray();
        }

        public T GetService<T>() where T : class {
            return GetService(typeof(T)) as T;
        }

        public struct Context {
            public List<Type> parents;
            public VFInjectorParent scope;
            public string fieldName;
            public bool nullable;
        }
    }
}
