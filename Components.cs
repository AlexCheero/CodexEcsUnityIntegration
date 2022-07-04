using ECS;

namespace Components
{
    struct CollisionWith
    {
        public Entity entity;
    }
}

namespace Tags
{
    struct OverrideCollision { }
}