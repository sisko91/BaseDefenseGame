using Godot;

namespace Gurdy.ProcGen
{
    // Placeables are the base Node2D wrapper that all entities placed by ProcGen routines extend from.
    // Each Placeable has a RectRegion with a Size corresponding to the footprint of the entity when placed within a broader scene. This footprint is used
    // during ProcGen point tests to determine where the entity will fit without conflicting with other placed entities or spawning constraints.
    public partial class Placeable : Node2D
    {
        // PlacedFootprint is used to determine overlaps and constraints such as and out of bounds locations where this scene cannot be placed by
        // a ProcGen routine. The footprint can be offset and smaller/larger than the actual Placeable scene to finely control placement logic depending on where the
        // origin of the Placeable scene and other constituents are.
        public RectRegion PlacedFootprint => GetNode<RectRegion>("PlacedFootprint");

        public override void _Ready()
        {
            if (GetNodeOrNull<RectRegion>("PlacedFootprint") == null)
            {
                GD.PushError($"PlacedFootprint is missing! {SceneFilePath} requires a RectRegion child named 'PlacedFootprint'.");
            }
        }
    }

}
