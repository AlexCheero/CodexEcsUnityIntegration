﻿using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityCollisionEnterProvider : EntityUnityCallbackProvider
    {
        void OnCollisionEnter(Collision collision)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionEnter invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new CollisionEnterComponent
            {
                collision = collision
            };
            if (view.Have<CollisionEnterComponent>())
            {
                if (view.Have<OverrideCollision>())
                    view.GetEcsComponent<CollisionEnterComponent>() = collisionComponent;
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}