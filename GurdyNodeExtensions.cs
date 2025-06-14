using Godot;
using System.Collections.Generic;

namespace ExtensionMethods
{
    public static class GurdyNodeExtensions
    {
        // Gets the game's World instance. Should be accessible anywhere, at any time.
        // Note: This is different from GetWorld2D() / GetWorld3D() which have to do with referencing the *spaces* that the physics engine populates for that side of the simulation.
        public static World GetGameWorld(this Node node)
        {
            return Main.Instance.World;
        }

        public static CanvasLayer GetGameHUD(this Node node)
        {
            return GetSceneTree().Root.GetNode<CanvasLayer>("Main/HUD");
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

        // Just like GetNodesInGroup(), but enforces a type constraint and only returns nodes which match. For this reason it's
        // not possible to count the number of elements returned without iterating the full enumeration (or using LINQ).
        public static IEnumerable<T> GetTypedNodesInGroup<T>(this SceneTree sceneTree, string groupName) {
            foreach (var node in sceneTree.GetNodesInGroup(groupName)) {
                if (node is T typedNode) {
                    yield return typedNode;
                }
            }
        }

        // Just like GetChildren(), but enforces a type constraint and only returns children which match.
        public static IEnumerable<T> GetChildrenOfType<T>(this Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is T typedChild)
                {
                    yield return typedChild;
                }
            }
        }

        // Tweens a Vector3 into a Vector2.
        public static Vector2 XY(this Vector3 vec3) {
            return new Vector2(vec3.X, vec3.Y);
        }

        public static List<Node> GetAllChildren(this Node node) {
            List<Node> children = new List<Node>();
            foreach (Node child in node.GetChildren()) {
                children.Add(child);
                if (child.GetChildCount() > 0) {
                    children.AddRange(GetAllChildren(child));
                }
            }

            return children;
        }

        public static bool IsInWorld(this Node node, World world) {
            var parent = node.GetParent();
            while (parent != null) {
                if (parent == world) {
                    return true;
                }
                parent = parent.GetParent();
            }

            return false;
        }
    }
}

