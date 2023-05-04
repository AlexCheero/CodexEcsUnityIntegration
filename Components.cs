using ECS;

namespace Components
{
    public interface IComponent { }

    public struct CollisionWith : IComponent
    {
        public Entity entity;
    }
}

namespace Tags
{
    public interface ITag { }

    public struct OverrideCollision : ITag { }
}