using Godot;

namespace ExtensionMethods
{
    public static class DebugNodeExtensions {
        // Enables all available debug renderers on the (assumed Main) scene root node specified.
        public static void EnableDebugRenderers()
        {
            var sceneTree = WorldNodeExtensions.GetSceneTree();
            if (sceneTree != null)
            {
                var mainSceneNode = sceneTree.Root.GetNode("Main");
                if(mainSceneNode != null)
                {
                    // Set up a Debug node that all renderers can live under.
                    var debugRootNode = new Node();
                    debugRootNode.Name = "Debug";
                    mainSceneNode.AddChild(debugRootNode);

                    // Configure & Add our debug LineRenderer.
                    AddLineRenderer(debugRootNode);
                }
            }
        }

        private static void AddLineRenderer(Node parentNode)
        {
            DebugLineRenderer lineRenderer = new DebugLineRenderer();
            lineRenderer.Name = "LineRenderer";
            parentNode.AddChild(lineRenderer);
        }

        // Attempts to acquire the DebugLineRenderer under the conventional path /root/Main/Debug/LineRenderer, no-op if the line
        // renderer isn't defined.
        public static void DrawDebugLine(this Node node, Vector2 origin, Vector2 endpoint, Color color, double lifeTime = -1)
        {
            var sceneTree = WorldNodeExtensions.GetSceneTree();
            if (sceneTree != null)
            {
                var lineRenderer = sceneTree.Root.GetNode<DebugLineRenderer>("Main/Debug/LineRenderer");
                lineRenderer?.PushLine(origin, endpoint, color, lifeTime);
            }
        }
    }

}
