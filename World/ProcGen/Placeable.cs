using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gurdy.ProcGen
{
    // Placeables are the base Node2D wrapper that all entities placed by ProcGen routines extend from.
    // Each Placeable has a RectRegion with a Size corresponding to the footprint of the entity when placed within a broader scene. This footprint is used
    // during ProcGen point tests to determine where the entity will fit without conflicting with other placed entities or spawning constraints.
    public partial class Placeable : Node2D
    {
        // PlacedFootprint is used to determine overlaps and constraints such as overlaps and out of bounds locations where this scene cannot be placed
        // by a ProcGen routine. The footprint can be offset and smaller/larger than the actual Placeable scene to finely control placement logic
        // depending on where the origin of the Placeable scene and other constituents are.
        public RectRegion PlacedFootprint
        {
            get
            {
                if (_placedFootprint == null)
                {
                    _placedFootprint = GetNode<RectRegion>("PlacedFootprint");
                }
                return _placedFootprint;
            }
        }
        private RectRegion _placedFootprint = null;

        // A list of all RectRegions parented under /SecondaryFootprints on the Placeable scene.
        // These are inspected during ProcGen routines. This operation is cached once it is called for the first time.
        public IEnumerable<RectRegion> SecondaryFootprints
        {
            get
            {
                if (_secondaryFootprints == null)
                {
                    _secondaryFootprints = GetNodeOrNull("SecondaryFootprints")?.GetChildren().OfType<RectRegion>().ToList() ?? [];
                }
                return _secondaryFootprints;
            }
        }
        private List<RectRegion> _secondaryFootprints = null;

        public override void _Ready()
        {
            if (GetNodeOrNull<RectRegion>("PlacedFootprint") == null)
            {
                GD.PushError($"PlacedFootprint is missing! {SceneFilePath} requires a RectRegion child named 'PlacedFootprint'.");
            }
        }
    }

}
