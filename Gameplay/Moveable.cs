using Godot;
using System;

//Common functionality for any moveable object that implements CharacterBody2D (like projectiles and characters)
public partial class Moveable : CharacterBody2D, IEntity {

    // The entity's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // entities enter their regions
    public int CurrentElevationLevel = 0;
    public BuildingRegion CurrentRegion { get; set; }

    [ExportCategory("Grass Interaction")]
    // Controls whether this moveable entity displaces grass patches that it travels through.
    [Export] protected bool DisplaceGrass = true;
    // The GrassDisplacementMarker to instantiate if/when this moveable should be influencing grass that it moves through.
    // All Moveables displace grass by default when DisplaceGrass==true, using a default displacement marker. Instances
    // can override the displacement marker and provide their own through this override.
    [Export] protected PackedScene GrassDisplacementMarkerOverride = null;
    
    // The grass displacement marker to instantiate when DisplaceGrass==true and no GrassDisplacementMarkerOverride is
    // configured. Subclasses can override this to provide a different default for their instances.
    protected virtual PackedScene DefaultGrassDisplacementMarkerScene => GD.Load<PackedScene>("res://World/Environment/Rendering/DisplacementMasks/Grass/default_grass_displacement_marker.tscn");

    public Moveable() : base() {
        //Better for 2d top-down
        MotionMode = MotionModeEnum.Floating;
    }

    public override void _Ready()
    {
        base._Ready();
        if (DisplaceGrass)
        {
            // Create our grass displacement marker. This tracks the character itself so we don't need a stored reference.
            var grassMarker = GrassDisplacementMarkerOverride?.Instantiate<DisplacementMaskMarker>() ?? 
                              DefaultGrassDisplacementMarkerScene?.Instantiate<DisplacementMaskMarker>();
            grassMarker?.RegisterOwner(this);
            if (grassMarker == null)
            {
                GD.PushError($"{Name} had DisplaceGrass configured but provided no GrassDisplacementMarker (via Override or default).");
            }
        }
    }
    
    public virtual void ChangeFloor(int targetFloor) {
        if (targetFloor == CurrentElevationLevel) {
            return;
        }

        int shift = targetFloor - CurrentElevationLevel;
        CurrentElevationLevel = targetFloor;
        if (shift > 0) {
            CollisionLayer = CollisionLayer << shift * CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask << shift * CollisionConfig.LAYERS_PER_FLOOR;
        } else if (shift < 0) {
            CollisionLayer = CollisionLayer >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
        }
    }

    public void SetInside(bool inside) {
        if (Material != null)
        {
            Material.Set("shader_parameter/is_inside", inside);
        }
    }
}
