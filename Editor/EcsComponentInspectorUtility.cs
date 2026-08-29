using System;
using System.Collections.Generic;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal static class EcsComponentInspectorUtility
    {
        private const BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Builds a dependency-first, duplicate-free component addition plan. No caller
        /// state is changed unless this method succeeds.
        /// </summary>
        internal static bool TryBuildAdditionPlan(
            Type requestedType,
            IEnumerable<Type> existingTypes,
            List<Type> additionPlan,
            out string error)
        {
            if (additionPlan == null)
                throw new ArgumentNullException(nameof(additionPlan));

            additionPlan.Clear();
            error = null;

            var existingSet = new HashSet<Type>();
            var finalTypes = new List<Type>();
            if (existingTypes != null)
            {
                foreach (var type in existingTypes)
                {
                    if (type == null || !existingSet.Add(type))
                        continue;
                    finalTypes.Add(type);
                }
            }

            var processed = new HashSet<Type>();
            var processing = new HashSet<Type>();
            var plannedSet = new HashSet<Type>();
            if (!TryAppendRequiredComponents(
                    requestedType,
                    existingSet,
                    processed,
                    processing,
                    plannedSet,
                    additionPlan,
                    out error))
            {
                additionPlan.Clear();
                return false;
            }

            if (additionPlan.Count == 0)
                return true;

            finalTypes.AddRange(additionPlan);
            for (var i = 0; i < finalTypes.Count; i++)
            {
                var declaringType = finalTypes[i];
                if (!TryGetConstraintTypes<EcsExcludeComponentAttribute>(
                        declaringType,
                        attribute => attribute.ComponentTypes,
                        out var excludedTypes,
                        out error))
                {
                    additionPlan.Clear();
                    return false;
                }

                for (var j = 0; j < excludedTypes.Count; j++)
                {
                    var excludedType = excludedTypes[j];
                    if (!Contains(finalTypes, excludedType))
                        continue;

                    // Existing invalid pairs are outside the scope of this operation. An
                    // addition is rejected only when at least one side is newly planned.
                    if (!plannedSet.Contains(declaringType) && !plannedSet.Contains(excludedType))
                        continue;

                    error =
                        $"Cannot add ECS component '{requestedType.Name}': " +
                        $"'{declaringType.Name}' excludes '{excludedType.Name}'.";
                    additionPlan.Clear();
                    return false;
                }
            }

            return true;
        }

        internal static bool TryAddSerializedComponents(
            SerializedProperty componentsProperty,
            IReadOnlyList<ComponentWrapper> existingComponents,
            Object owner,
            Type requestedType,
            ComponentWrapper requestedTemplate = null)
        {
            if (componentsProperty == null || !componentsProperty.isArray)
            {
                Debug.LogError("Cannot add an ECS component: serialized component list was not found.", owner);
                return false;
            }
            if (requestedTemplate != null && requestedTemplate.GetComponentType() != requestedType)
            {
                Debug.LogError(
                    $"Cannot add ECS component '{requestedType?.Name}': pasted component type is " +
                    $"'{requestedTemplate.GetComponentType().Name}'.",
                    owner);
                return false;
            }

            var existingTypes = new List<Type>();
            CollectSerializedComponentTypes(componentsProperty, existingComponents, existingTypes);

            var additionPlan = new List<Type>();
            if (!TryBuildAdditionPlan(requestedType, existingTypes, additionPlan, out var error))
            {
                Debug.LogError(error, owner);
                return false;
            }

            if (additionPlan.Count == 0)
                return true;

            // Construct every wrapper before mutating the serialized list. Constraint or
            // construction failures therefore cannot leave a partially-added component set.
            var wrappers = new List<ComponentWrapper>(additionPlan.Count);
            try
            {
                for (var i = 0; i < additionPlan.Count; i++)
                {
                    var componentType = additionPlan[i];
                    var wrapper = CreateDefaultWrapper(componentType, owner);
                    if (requestedTemplate != null && componentType == requestedType)
                        wrapper.InitFromComponent(requestedTemplate.GetBoxedDefaultValue());
                    wrappers.Add(wrapper);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, owner);
                return false;
            }

            for (var i = 0; i < wrappers.Count; i++)
            {
                var index = componentsProperty.arraySize;
                componentsProperty.InsertArrayElementAtIndex(index);
                componentsProperty.GetArrayElementAtIndex(index).managedReferenceValue = wrappers[i];
            }

            return true;
        }

        internal static bool TryAddRuntimeComponents(EntityView view, Type requestedType)
        {
            if (view == null || !view.IsViewValid())
            {
                Debug.LogError("Cannot add an ECS component: EntityView is not attached to a valid entity.", view);
                return false;
            }

            return TryAddRuntimeComponents(view.World, view.Id, requestedType, view);
        }

        internal static bool TryAddRuntimeComponents(
            EcsWorld world,
            int entityId,
            Type requestedType,
            Object logContext = null)
        {
            if (world == null || !world.IsIdValid(entityId))
            {
                Debug.LogError("Cannot add an ECS component: the runtime entity is not valid.", logContext);
                return false;
            }

            var existingTypes = new List<Type>();
            CollectRuntimeComponentTypes(world, entityId, existingTypes);

            var additionPlan = new List<Type>();
            if (!TryBuildAdditionPlan(requestedType, existingTypes, additionPlan, out var error))
            {
                Debug.LogError(error, logContext);
                return false;
            }

            if (additionPlan.Count == 0)
                return true;

            // Register all closed component metadata before changing the entity. This keeps
            // reflection/registration failures from leaving a partially-added dependency set.
            try
            {
                for (var i = 0; i < additionPlan.Count; i++)
                    ComponentMapping.EnsureTypeRegistered(additionPlan[i]);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, logContext);
                return false;
            }

            try
            {
                for (var i = 0; i < additionPlan.Count; i++)
                    world.Add_Dynamic(additionPlan[i], entityId);
            }
            catch (Exception exception)
            {
                // Add<T> can fail after an earlier required component was attached (or
                // after its own structural add but before reactions finish). Restore the
                // pre-operation component set in reverse dependency order.
                for (var i = additionPlan.Count - 1; i >= 0; i--)
                {
                    var componentType = additionPlan[i];
                    try
                    {
                        var componentId = ComponentMapping.GetIdForType(componentType);
                        if (world.GetMask(entityId).Check(componentId))
                            world.Remove_Dynamic(componentType, entityId);
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.LogException(rollbackException, logContext);
                    }
                }

                Debug.LogException(exception, logContext);
                return false;
            }
            return true;
        }

        internal static void CollectRuntimeComponentTypes(EntityView view, ICollection<Type> destination)
        {
            destination.Clear();
            if (view == null || !view.IsViewValid())
                return;

            CollectRuntimeComponentTypes(view.World, view.Id, destination);
        }

        internal static void CollectRuntimeComponentTypes(
            EcsWorld world,
            int entityId,
            ICollection<Type> destination)
        {
            destination.Clear();
            if (world == null || !world.IsIdValid(entityId))
                return;

            foreach (var componentId in world.GetMask(entityId))
            {
                var componentType = ComponentMapping.GetTypeForId(componentId);
                if (typeof(IComponent).IsAssignableFrom(componentType))
                    destination.Add(componentType);
            }
        }

        private static bool TryAppendRequiredComponents(
            Type componentType,
            HashSet<Type> existingTypes,
            HashSet<Type> processed,
            HashSet<Type> processing,
            HashSet<Type> plannedTypes,
            List<Type> additionPlan,
            out string error)
        {
            if (!TryValidateComponentType(componentType, out error))
                return false;
            if (processed.Contains(componentType))
                return true;
            if (!processing.Add(componentType))
                return true; // A dependency cycle is satisfied by adding each type once.

            if (!TryGetConstraintTypes<EcsRequireComponentAttribute>(
                    componentType,
                    attribute => attribute.ComponentTypes,
                    out var requiredTypes,
                    out error))
            {
                processing.Remove(componentType);
                return false;
            }

            for (var i = 0; i < requiredTypes.Count; i++)
            {
                if (TryAppendRequiredComponents(
                        requiredTypes[i],
                        existingTypes,
                        processed,
                        processing,
                        plannedTypes,
                        additionPlan,
                        out error))
                {
                    continue;
                }

                processing.Remove(componentType);
                return false;
            }

            processing.Remove(componentType);
            processed.Add(componentType);
            if (!existingTypes.Contains(componentType) && plannedTypes.Add(componentType))
                additionPlan.Add(componentType);
            return true;
        }

        private static bool TryGetConstraintTypes<TAttribute>(
            Type declaringType,
            Func<TAttribute, Type[]> getTypes,
            out List<Type> componentTypes,
            out string error)
            where TAttribute : Attribute
        {
            componentTypes = new List<Type>();
            error = null;
            var seen = new HashSet<Type>();
            var attributes = declaringType.GetCustomAttributes<TAttribute>(false);
            foreach (var attribute in attributes)
            {
                var types = getTypes(attribute);
                if (types == null)
                    continue;
                for (var i = 0; i < types.Length; i++)
                {
                    var componentType = types[i];
                    if (!TryValidateComponentType(componentType, out var validationError))
                    {
                        error =
                            $"ECS component '{declaringType.Name}' has an invalid " +
                            $"{typeof(TAttribute).Name} declaration: {validationError}";
                        return false;
                    }

                    if (seen.Add(componentType))
                        componentTypes.Add(componentType);
                }
            }

            return true;
        }

        private static bool TryValidateComponentType(Type componentType, out string error)
        {
            if (componentType == null)
            {
                error = "the component type is null.";
                return false;
            }
            if (!typeof(IComponent).IsAssignableFrom(componentType))
            {
                error = $"'{componentType.FullName}' does not implement {nameof(IComponent)}.";
                return false;
            }
            if (componentType == typeof(MatchReact))
            {
                error = $"'{componentType.FullName}' is an internal reactive marker and cannot be added manually.";
                return false;
            }
            if (componentType.IsAbstract || componentType.IsInterface || componentType.IsGenericType)
            {
                error = $"'{componentType.FullName}' is not a concrete, non-generic ECS component.";
                return false;
            }

            error = null;
            return true;
        }

        private static void CollectSerializedComponentTypes(
            SerializedProperty componentsProperty,
            IReadOnlyList<ComponentWrapper> fallbackComponents,
            List<Type> destination)
        {
            destination.Clear();
            var seen = new HashSet<Type>();
            for (var i = 0; i < componentsProperty.arraySize; i++)
            {
                if (componentsProperty.GetArrayElementAtIndex(i).managedReferenceValue is not ComponentWrapper wrapper)
                    continue;
                var componentType = wrapper.GetComponentType();
                if (seen.Add(componentType))
                    destination.Add(componentType);
            }

            if (fallbackComponents == null)
                return;
            for (var i = 0; i < fallbackComponents.Count; i++)
            {
                var wrapper = fallbackComponents[i];
                if (wrapper == null)
                    continue;
                var componentType = wrapper.GetComponentType();
                if (seen.Add(componentType))
                    destination.Add(componentType);
            }
        }

        private static ComponentWrapper CreateDefaultWrapper(Type componentType, Object owner)
        {
            var wrapperType = typeof(ComponentWrapper<>).MakeGenericType(componentType);
            var wrapper = (ComponentWrapper)Activator.CreateInstance(wrapperType);
            var defaultValueGetter = componentType.GetProperty("Default", StaticMembers);
            if (defaultValueGetter != null)
                wrapper.InitFromComponent((IComponent)defaultValueGetter.GetValue(null));
            wrapper.OnAdded(owner);
            return wrapper;
        }

        private static bool Contains(List<Type> types, Type target)
        {
            for (var i = 0; i < types.Count; i++)
            {
                if (types[i] == target)
                    return true;
            }
            return false;
        }
    }
}
