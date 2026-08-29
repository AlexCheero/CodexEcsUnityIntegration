using System;
using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal static class PureEntityEditorUtility
    {
        internal static EcsFilter BuildFilter(
            EcsWorld world,
            IEnumerable<Type> requiredTypes,
            IEnumerable<Type> excludedTypes)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            var includes = new BitMask(ComponentMapping.EnsureTypeRegistered(typeof(PureEntity)));
            var excludes = new BitMask();
            var required = new HashSet<Type> { typeof(PureEntity) };
            AddTypes(requiredTypes, required, ref includes);

            var excluded = new HashSet<Type>();
            AddTypes(excludedTypes, excluded, ref excludes);
            if (excluded.Contains(typeof(PureEntity)))
                throw new ArgumentException($"{nameof(PureEntity)} cannot be excluded from a pure-entity query.");
            if (required.Overlaps(excluded))
                throw new ArgumentException("A component cannot be both required and excluded.");

            return world.RegisterFilter(includes, excludes);
        }

        internal static bool IsEntityReferenceValid(
            EcsWorld world,
            int entityId,
            in Entity entity)
        {
            return world != null &&
                   entityId > 0 &&
                   world.IsIdValid(entityId) &&
                   world.GetById(entityId).Val == entity.Val &&
                   world.IsEntityValid(entity);
        }

        internal static bool IsPureEntityReferenceValid(
            EcsWorld world,
            int entityId,
            in Entity entity)
        {
            return IsEntityReferenceValid(world, entityId, in entity) &&
                   world.Have<PureEntity>(entityId);
        }

        internal static bool TryGetPosition(
            EcsWorld world,
            int entityId,
            in Entity entity,
            out Vector3 position)
        {
            position = default;
            if (!IsPureEntityReferenceValid(world, entityId, in entity) ||
                !world.Have<Position>(entityId))
                return false;

            position = world.Get<Position>(entityId).position;
            return true;
        }

        internal static bool TryGetRotation(
            EcsWorld world,
            int entityId,
            in Entity entity,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!IsPureEntityReferenceValid(world, entityId, in entity) ||
                !world.Have<Rotation>(entityId))
                return false;

            rotation = world.Get<Rotation>(entityId).rotation;
            return true;
        }

        internal static bool TrySetPosition(
            EcsWorld world,
            int entityId,
            in Entity entity,
            Vector3 position)
        {
            if (!IsPureEntityReferenceValid(world, entityId, in entity) ||
                !world.Have<Position>(entityId))
                return false;

            world.Get<Position>(entityId).position = position;
            return true;
        }

        internal static bool TrySetRotation(
            EcsWorld world,
            int entityId,
            in Entity entity,
            Quaternion rotation)
        {
            if (!IsPureEntityReferenceValid(world, entityId, in entity) ||
                !world.Have<Rotation>(entityId))
                return false;

            var magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);
            world.Get<Rotation>(entityId).rotation = magnitude > Mathf.Epsilon
                ? new Quaternion(
                    rotation.x / magnitude,
                    rotation.y / magnitude,
                    rotation.z / magnitude,
                    rotation.w / magnitude)
                : Quaternion.identity;
            return true;
        }

        private static void AddTypes(
            IEnumerable<Type> types,
            HashSet<Type> destination,
            ref BitMask mask)
        {
            if (types == null)
                return;

            foreach (var type in types)
            {
                ValidateComponentType(type);
                if (!destination.Add(type))
                    continue;
                mask.Set(ComponentMapping.EnsureTypeRegistered(type));
            }
        }

        private static void ValidateComponentType(Type type)
        {
            if (type == null)
                throw new ArgumentException("A pure-entity query component type is null.");
            if (type == typeof(MatchReact) ||
                !typeof(IComponent).IsAssignableFrom(type) ||
                type.IsAbstract ||
                type.IsInterface ||
                type.IsGenericType)
            {
                throw new ArgumentException(
                    $"'{type.FullName}' is not a concrete, non-generic ECS component type.");
            }
        }
    }
}
