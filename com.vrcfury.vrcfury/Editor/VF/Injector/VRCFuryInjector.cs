
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
        private Dictionary<Type, object> completedObjects = new Dictionary<Type, object>();
        private HashSet<Type> availableTypes = new HashSet<Type>();
        private HashSet<Type> typesInConstruction = new HashSet<Type>();

        public void RegisterService(Type type) {
            if (availableTypes.Contains(type)) {
                throw new Exception($"{type.FullName} was registered twice");
            }
            availableTypes.Add(type);
        }

        public void RegisterService<T>(T service) {
            RegisterService(typeof(T));
            completedObjects[typeof(T)] = service;
        }

        public object CreateAndInject(Type type, List<Type> _parents = null) {
            var parents = new List<Type>();
            if (_parents != null) parents.AddRange(parents);
            parents.Add(type);

            var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            var args = constructor.GetParameters()
                .Select(p => GetService(p.ParameterType, parents))
                .ToArray();
            var instance = Activator.CreateInstance(type, args);
            foreach (var field in ReflectionUtils.GetAllFields(type)) {
                if (field.GetCustomAttribute<VFAutowiredAttribute>() == null) continue;
                field.SetValue(instance, GetService(field.FieldType, parents));
            }

            return instance;
        }

        public T CreateAndInject<T>() where T : class {
            return CreateAndInject(typeof(T)) as T;
        }

        private object GetService(Type type, List<Type> _parents = null) {
            var parents = new List<Type>();
            if (_parents != null) parents.AddRange(_parents);

            if (completedObjects.TryGetValue(type, out var finished)) {
                return finished;
            }
            if (!availableTypes.Contains(type)) {
                throw new Exception($"{type.FullName} is not a registered type");
            }
            if (typesInConstruction.Contains(type)) {
                throw new Exception($"{type.FullName} is already being constructed (dependency loop?) {string.Join(",", parents)}");
            }
            typesInConstruction.Add(type);
            completedObjects[type] = CreateAndInject(type, parents);
            return completedObjects[type];
        }

        public object[] GetAllServices() {
            return availableTypes.Select(t => GetService(t, null)).ToArray();
        }
    }
}
