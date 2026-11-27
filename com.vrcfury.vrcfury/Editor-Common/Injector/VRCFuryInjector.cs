
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
        private static readonly Dictionary<Type, (ParameterInfo[] parameters, FieldInfo[] fields)> injectionLayouts = new();
        private static readonly Dictionary<Type, Type> listItemTypes = new();
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly Dictionary<string, object> qualifiedObjects = new Dictionary<string, object>();

        private readonly Dictionary<Type, ISet<Type>> serviceTypesByParent = new Dictionary<Type, ISet<Type>>();

        public VRCFuryInjector() {
            Set(this);
        }

        public void ImportScan(Type type) {
            foreach (var serviceType in ReflectionUtils.GetTypes(type)) {
                if (serviceType.GetCustomAttribute<VFServiceAttribute>()?.autoRegister == false) continue;
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
            if (service == null) throw new ArgumentNullException(nameof(service));
            Set(service.GetType(), service);
        }
        
        public void Set(Type type, object service) {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (services.ContainsKey(type)) {
                throw new Exception("Service of type " + type.Name + " already set");
            }
            services[type] = service;
            foreach (var parent in GetAllParents(type)) {
                AddServiceType(parent, type);
            }
        }
        
        public void Set(string name, object service) {
            if (service == null) throw new ArgumentNullException(nameof(service));
            qualifiedObjects[name] = service;
        }

        private static bool IsPrototypeScope(Type type) {
            return type.IsDefined(typeof(VFPrototypeScopeAttribute), true);
        }
        
        public T CreateAndFillObject<T>() {
            return (T)CreateAndFillObject(typeof(T));
        }

        public object CreateAndFillObject(Type type, Context context = default) {
            var nextParentHolder = new VFInjectorParent();
            var parent = context.scope;
            if (context.scope == null) context.scope = nextParentHolder;

            if (!injectionLayouts.TryGetValue(type, out var layout)) {
                var constructors = type.GetConstructors();
                var constructor = constructors.FirstOrDefault(c => c.IsDefined(typeof(VFAutowiredAttribute), true))
                    ?? constructors.OrderByDescending(c => c.GetParameters().Length).First();
                var fields = ReflectionUtils.GetAllFields(type)
                    .Where(field => field.IsDefined(typeof(VFAutowiredAttribute), true)).ToArray();
                injectionLayouts[type] = layout = (constructor.GetParameters(), fields);
            }
            var args = layout.parameters.Select(p => Get(p, context)).ToArray();
            var instance = Activator.CreateInstance(type, args);
            nextParentHolder.parent = instance;
            
            object GetField(FieldInfo field) {
                if (field.FieldType == typeof(VFInjectorParent) && parent != null) return parent;
                return Get(field, context);
            }
            foreach (var field in layout.fields) {
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
                if (!listItemTypes.TryGetValue(type, out var listItemType)) {
                    listItemTypes[type] = listItemType = ReflectionUtils.GetGenericArgument(type, typeof(IList<>));
                }
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

                var implementations = GetServiceTypes(type);
                if (implementations.Count == 1) return GetConcreteService(implementations.First(), context);
                if (implementations.Count > 1) {
                    throw new Exception($"{type.FullName} {context.fieldName} has multiple implementations in this injector");
                }
                if (context.nullable) return null;
                throw new Exception($"{type.FullName} {context.fieldName} was not found in this injector");
            } catch(Exception e) {
                throw new Exception($"Error while constructing {type.FullName} service", e);
            }
        }

        private object GetConcreteService(Type type, Context context) {
            try {
                if (services.TryGetValue(type, out var finished)) return finished;
                var isPrototypeScope = IsPrototypeScope(type);

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

        private object[] GetServices(Type type, Context context, bool includeScoped) {
            var serviceTypes = GetServiceTypes(type)
                .Where(t => includeScoped || !IsPrototypeScope(t))
                .ToArray();
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
