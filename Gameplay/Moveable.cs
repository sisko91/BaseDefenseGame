using Godot;
using System;

//Common functionality for any moveable object that implements CharacterBody2D (like projectiles and characters)
public partial class Moveable : CharacterBody2D, IEntity {

    // The entity's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // entities enter their regions
    public int CurrentElevationLevel = 0;
    public BuildingRegion CurrentRegion { get; set; }

    private int FallSpeed = 600;
    public double FallTime = 0f;
    public bool AffectedByGravity = true;
    private bool _falling;
    public bool Falling {
        get {
            return _falling;
        }
        set {
            _falling = value;
            if (!_falling) {
                StartedFallingAction = null;
            }
        }
    }
    public event Action<Moveable> StartedFallingAction;

    private Vector2 CollisionShapePosition;
    private CollisionShape2D CollisionShape;
    private Vector2 ShadowPosition;
    private Sprite2D Shadow;

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

    public Delegate[] GetStartFallingCallbacks() {
        if (StartedFallingAction != null) {
            return StartedFallingAction.GetInvocationList();
        }
        return new Delegate[] { };
    }

    public override void _Ready()
    {
        base._Ready();
        if (DisplaceGrass)
        {
            // Create our grass displacement marker. This tracks the character itself so we don't need a stored reference.
            // Note: We currently create two of these and register one with each displacement viewport (global and screenspace).
            //       This will likely be simplified over time but right now comparing the two approaches is useful.
            var grassMarker = GrassDisplacementMarkerOverride?.Instantiate<DisplacementMaskMarker>() ?? 
                              DefaultGrassDisplacementMarkerScene?.Instantiate<DisplacementMaskMarker>();
            
            grassMarker?.RegisterWithViewport(Main.GetScreenSpaceDisplacementViewport(),this);
            if (grassMarker == null)
            {
                GD.PushError($"{Name} had DisplaceGrass configured but provided no GrassDisplacementMarker (via Override or default).");
            }
            else
            {
                var secondMarker = grassMarker.Duplicate() as DisplacementMaskMarker;
                secondMarker?.RegisterWithViewport(Main.GetGlobalDisplacementViewport(), this);
            }
        }

        Shadow = GetNodeOrNull<Sprite2D>("Shadow");
        if (Shadow != null) {
            ShadowPosition = Shadow.Position;
        }
        CollisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (CollisionShape != null) {
            CollisionShapePosition = CollisionShape.Position;
        }
    }

    public Vector2 HandleFalling(double delta) {
        float fallDist = Building.FloorHeight * 1;
        var maxFallTime = fallDist / FallSpeed;
        var distLeft = fallDist * (maxFallTime - FallTime) / maxFallTime;

        
        if (Shadow != null) {
            Shadow.Position = new Vector2(ShadowPosition.X, ShadowPosition.Y + (float) distLeft);
        }
        if (CollisionShape != null) {
            var offset = new Vector2(0, (float)distLeft + 5).Rotated(-Rotation);
            CollisionShape.Position = new Vector2(CollisionShapePosition.X, CollisionShapePosition.Y) + offset;
        }

        if (FallTime == 0) {
            StartedFallingAction(this);
            StartedFallingAction = null;
        }

        FallTime += delta;

        //TODO: Handle falling multiple floors

        if (FallTime > maxFallTime) {
            Falling = false;
            FallTime = 0;
            if (Shadow != null) {
                Shadow.Position = ShadowPosition;
            }
            if (CollisionShape != null) {
                CollisionShape.Position = CollisionShapePosition;
            }
        }

        return Velocity + new Vector2(0, FallSpeed);
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
