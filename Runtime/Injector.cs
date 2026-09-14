using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LightweightDI
{
    /// <summary>
    /// A minimal inversion-of-control container for Unity.
    ///
    /// Dependencies are registered by type and resolved into fields and properties marked
    /// with <see cref="InjectAttribute"/>. Works on MonoBehaviours and on plain C# classes
    /// alike, because resolution happens on an existing instance rather than through a
    /// constructor - which MonoBehaviours cannot have.
    /// </summary>
    public class Injector
    {
        private static Injector _instance;

        /// <summary>
        /// Static access point. Kept for the few call sites that cannot be injected into -
        /// the scene bootstrap entry points. Everything else should receive its dependencies.
        /// </summary>
        public static Injector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Injector();
                }
                return _instance;
            }
        }

        // The whole container is one type -> instance map.
        private readonly Dictionary<Type, object> _injections;

        public Injector()
        {
            _injections = new Dictionary<Type, object>();
        }

        #region Resolution

        /// <summary>
        /// Injects into an object that tracks whether it has been injected into already.
        /// The flag is what keeps reflection off the hot path - this can be called from
        /// anywhere without checking first.
        /// </summary>
        public void InjectInto(IInjectable injectable, bool suppressErrors = false)
        {
            if (!injectable.Injected)
            {
                InjectInto((object)injectable, suppressErrors);
            }

            injectable.Injected = true;
        }

        /// <summary>
        /// Resolves every field and property marked with <see cref="InjectAttribute"/>.
        /// A type that is not registered leaves the member untouched and logs what was
        /// missing, what asked for it, and what was available at that moment.
        /// </summary>
        public void InjectInto(object subject, bool suppressErrors = false)
        {
            if (subject == null)
            {
                Debug.LogError("Cannot inject into null.");
                return;
            }

            Type subjectType = subject.GetType();

            // NonPublic and FlattenHierarchy matter: most [Inject] fields are private
            // and declared on a base class.
            FieldInfo[] fields = subjectType.GetFields(
                BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttributes(typeof(InjectAttribute), false).Length == 0)
                {
                    continue;
                }

                if (_injections.TryGetValue(field.FieldType, out object value))
                {
                    field.SetValue(subject, value);
                }
                else if (!suppressErrors)
                {
                    LogMissingMapping(field.FieldType, subjectType);
                }
            }

            PropertyInfo[] properties = subjectType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (PropertyInfo property in properties)
            {
                if (property.GetCustomAttributes(typeof(InjectAttribute), false).Length == 0)
                {
                    continue;
                }

                if (_injections.TryGetValue(property.PropertyType, out object value))
                {
                    property.SetValue(subject, value, null);
                }
                else if (!suppressErrors)
                {
                    LogMissingMapping(property.PropertyType, subjectType);
                }
            }
        }

        /// <summary>
        /// Injects into every element of a collection, skipping nulls.
        /// </summary>
        public void InjectIntoEnumerable<T>(IEnumerable<T> subjects, bool suppressErrors = false)
        {
            if (subjects == null)
            {
                return;
            }

            foreach (object subject in subjects)
            {
                if (subject == null)
                {
                    Debug.LogWarning("Skipping null element while injecting into a collection.");
                    continue;
                }

                InjectInto(subject, suppressErrors);
            }
        }

        #endregion

        #region Registration

        /// <summary>
        /// Creates a new instance, registers it under T and resolves its own dependencies.
        /// Used for plain C# services that have no scene presence.
        /// </summary>
        public T MapSingleton<T>(bool suppressErrors = false) where T : new()
        {
            T instance = new T();
            MapAndInjectInto(instance, suppressErrors);
            return instance;
        }

        /// <summary>
        /// Idempotent variant of <see cref="MapSingleton{T}"/> - returns the existing
        /// registration instead of replacing it.
        /// </summary>
        public T MapOrGetSingleton<T>() where T : new()
        {
            if (_injections.TryGetValue(typeof(T), out object existing))
            {
                return (T)existing;
            }

            return MapSingleton<T>();
        }

        /// <summary>
        /// Binds a concrete implementation to a requested interface or base type.
        /// </summary>
        public void MapSingletonOf<TRequested, TClass>() where TClass : TRequested, new()
        {
            MapAndInjectInto<TRequested>(new TClass());
        }

        /// <summary>
        /// Registers an existing instance under T and immediately resolves its dependencies.
        /// </summary>
        public void MapAndInjectInto<T>(T value, bool suppressErrors = false)
        {
            if (value == null)
            {
                Debug.LogError($"Cannot register a null value for {typeof(T).Name}.");
                return;
            }

            _injections[typeof(T)] = value;

            InjectInto(value, suppressErrors);
        }

        /// <summary>
        /// Registers a value the container does not own - ScriptableObject settings,
        /// serialized contexts, data the game loaded from elsewhere.
        /// </summary>
        public void MapValue<T>(object value, bool suppressErrors = false)
        {
            MapValue(value, typeof(T), suppressErrors);
        }

        public void MapValue(object value, Type type, bool suppressErrors = false)
        {
            if (value == null)
            {
                Debug.LogError($"Cannot register a null value for {type.Name}.");
                return;
            }

            if (_injections.ContainsKey(type))
            {
                Debug.LogWarning(
                    $"Mapping for {type.Name} is already defined. Unmap it first if the " +
                    "mapping is meant to change.");
            }

            _injections[type] = value;
            InjectInto(value, suppressErrors);
        }

        /// <summary>
        /// Registers a manager that lives as a component in a scene.
        ///
        /// If the type is already registered, this call is coming from a scene that was
        /// loaded a second time: the freshly loaded duplicate is destroyed before it can
        /// register listeners or start coroutines, and the caller gets back the instance
        /// that is already live, so existing references stay valid.
        ///
        /// On first registration the manager is initialised right after injection, so its
        /// [Inject] fields are guaranteed to be set inside Initialize().
        /// </summary>
        public T TryMapManager<T>(T value, bool suppressErrors = false) where T : IManager
        {
            if (value == null)
            {
                Debug.LogError($"Cannot register a null manager for {typeof(T).Name}.");
                return default;
            }

            if (HasInjection(typeof(T)))
            {
                MonoBehaviour monoBehaviour = value as MonoBehaviour;
                if (monoBehaviour != null)
                {
                    UnityEngine.Object.Destroy(monoBehaviour.gameObject);
                }

                return (T)_injections[typeof(T)];
            }

            MapAndInjectInto(value, suppressErrors);

            value.Initialize();

            return value;
        }

        #endregion

        #region Lookup and removal

        /// <summary>
        /// Direct lookup. Service-locator style, kept only for call sites where field
        /// injection is not possible.
        /// </summary>
        public T Get<T>() where T : class
        {
            if (_injections.TryGetValue(typeof(T), out object value))
            {
                return (T)value;
            }

            return null;
        }

        public bool HasInjection(Type type)
        {
            return _injections.ContainsKey(type);
        }

        public void Unmap<T>()
        {
            if (!_injections.Remove(typeof(T)))
            {
                Debug.LogWarning($"There is no mapping for {typeof(T).Name} to remove.");
            }
        }

        public void Unmap(object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (!_injections.Remove(obj.GetType()))
            {
                Debug.LogWarning($"There is no mapping for {obj.GetType().Name} to remove.");
            }
        }

        /// <summary>
        /// Re-runs injection on every registered object. Used after a late registration
        /// that earlier dependencies were still waiting for.
        /// </summary>
        public void RefreshAllInjections()
        {
            foreach (object injection in _injections.Values)
            {
                InjectInto(injection, suppressErrors: true);
            }
        }

        public void Clear()
        {
            _injections.Clear();
            _instance = null;
        }

        #endregion

        private void LogMissingMapping(Type missingType, Type requestedBy)
        {
            Debug.LogError(
                $"Missing injection rule for {missingType.Name}, requested by " +
                $"{requestedBy.Name}. Registered types: {GetRegisteredTypes()}");
        }

        /// <summary>
        /// Everything currently registered. Printed with every missing-mapping error so the
        /// log answers "what was available at that moment" without attaching a debugger.
        /// </summary>
        private string GetRegisteredTypes()
        {
            if (_injections.Count == 0)
            {
                return "(none)";
            }

            string[] names = new string[_injections.Count];
            int i = 0;
            foreach (Type type in _injections.Keys)
            {
                names[i++] = type.Name;
            }

            return string.Join(", ", names);
        }
    }
}
