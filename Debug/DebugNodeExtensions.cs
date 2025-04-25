using Godot;

namespace ExtensionMethods
{
    public static class DebugNodeExtensions {
        // Enables all available debug renderers on the (assumed Main) scene root node specified.
        public static void EnableDebugRenderers()
        {
            var sceneTree = GurdyNodeExtensions.GetSceneTree();
            if (sceneTree != null)
            {
                var mainSceneNode = sceneTree.Root.GetNode("Main");
                if(mainSceneNode != null)
                {
                    // Set up a Debug node that all renderers can live under.
                    var debugRootNode = new Node2D();
                    debugRootNode.Name = "Debug";
                    debugRootNode.ZIndex = (int) RenderingServer.CanvasItemZMax; //draw above everything
                    mainSceneNode.AddChild(debugRootNode);

                    // Configure & Add our debug LineRenderer.
                    AddDebugDrawCallRenderer(debugRootNode);
                }
            }
        }

        private static void AddDebugDrawCallRenderer(Node parentNode)
        {
            DebugDrawCallRenderer ddcRenderer = new DebugDrawCallRenderer();
            ddcRenderer.Name = "DebugDrawCallRenderer";
            parentNode.AddChild(ddcRenderer);
        }

        // Attempts to acquire the DebugDrawCallRenderer under the conventional path /root/Main/Debug/DebugDrawCallRenderer, returns null
        // if the renderer is not enabled.
        public static DebugDrawCallRenderer GetDebugDrawCallRenderer() {
            var sceneTree = GurdyNodeExtensions.GetSceneTree();
            if (sceneTree != null) {
                return sceneTree.Root.GetNodeOrNull<DebugDrawCallRenderer>("Main/Debug/DebugDrawCallRenderer");
            }
            return null;
        }

        
        public static void DrawDebugLine(this Node node, Vector2 origin, Vector2 endpoint, Color color, double lifeTime = -1, string group = "default")
        {
            GetDebugDrawCallRenderer()?.PushLine(origin, endpoint, color, lifeTime, group);
        }

        public static void DrawDebugRect(this Node node, Vector2 origin, Vector2 size, Color color, bool fillInterior = false, double lifeTime = -1, string group = "default", bool centerOrigin = true) {
            GetDebugDrawCallRenderer()?.PushRect(origin, size, color, fillInterior, lifeTime, group, centerOrigin);
        }

        public static void DrawDebugCircle(this Node node, Vector2 origin, float radius, Color color, bool fillInterior = false, double lifeTime = -1, string group = "default") {
            GetDebugDrawCallRenderer()?.PushCircle(origin, radius, color, fillInterior, lifeTime, group);
        }

        public static void DrawDebugPoint(this Node node, Vector2 origin, float radius, Color color, double lifeTime = -1, string group = "default") {
            node.DrawDebugCircle(origin, radius, color, true, lifeTime, group);
        }

        public static void ClearDebugDrawCalls(this Node node, string group) {
            GetDebugDrawCallRenderer()?.Clear(group);
        }
    }

}
