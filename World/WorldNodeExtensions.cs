using Godot;

namespace ExtensionMethods
{
    public static class WorldNodeExtensions
    {
        // Gets the game's World instance. Should be accessible anywhere, at any time.
        // Note: This is different from GetWorld2D() / GetWorld3D() which have to do with referencing the *spaces* that the physics engine populates for that side of the simulation.
        public static World GetGameWorld(this Node node)
        {
            if (Engine.GetMainLoop() is SceneTree mainSceneTree)
            {
                return mainSceneTree.Root.GetNode<World>("Main/World");
            }
            GD.PushError($"Game requires MainLoop to be a SceneTree but was {Engine.GetMainLoop().GetType()}");
            return null;
        }

        public static SceneTree GetSceneTree()
        {
            if (Engine.GetMainLoop() is SceneTree mainSceneTree)
            {
                return mainSceneTree;
            }
            GD.PushError($"Game requires MainLoop to be a SceneTree but was {Engine.GetMainLoop().GetType()}");
            return null;
        }
    }
}

