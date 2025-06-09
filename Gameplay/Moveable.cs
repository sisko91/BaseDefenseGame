using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

//Common functionality for any moveable object that implements CharacterBody2D (like projectiles and characters)
public partial class Moveable : CharacterBody2D, IEntity {

    // The entity's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // entities enter their regions
    public int CurrentElevationLevel = 0;
    public BuildingRegion CurrentRegion { get; set; }

    private int FallSpeed = 600;
    public double FallTime = 0f;
    public bool AffectedByGravity = true;
    public bool Falling;

    protected Vector2 CollisionShapePosition;
    protected Node2D CollisionShape;

    protected Node2D Sprite;

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

    public override void _Ready() {
        base._Ready();
	    CollisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (CollisionShape == null) {
            CollisionShape = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
        }
        CollisionShapePosition = CollisionShape.Position;

        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (Sprite == null) {
            Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        }

        if (DisplaceGrass) {
            // Create our grass displacement marker. This tracks the character itself so we don't need a stored reference.
            var grassMarker = GrassDisplacementMarkerOverride?.Instantiate<DisplacementMaskMarker>() ?? 
                              DefaultGrassDisplacementMarkerScene?.Instantiate<DisplacementMaskMarker>();
            if (grassMarker != null)
            {
                grassMarker.GetNode<Sprite2D>("Sprite2D").Position = CollisionShapePosition;
                grassMarker.RegisterWithViewport(Main.GetGlobalDisplacementViewport(),this);
            }
            else
            {
                GD.PushError($"{Name} had DisplaceGrass configured but provided no GrassDisplacementMarker (via Override or default).");
            }
        }
    }

    public Vector2 HandleFalling(double delta) {
        float fallDist = Building.FloorHeight * 1;
        var maxFallTime = fallDist / FallSpeed;
        var distLeft = fallDist * (maxFallTime - FallTime) / maxFallTime;
        var offset = new Vector2(0, (float)distLeft + 5).Rotated(-Rotation);

        if (CollisionShape != null) {
            CollisionShape.Position = CollisionShapePosition + offset;
        }

        if (FallTime ==  0) {
            //TODO: Handle falling multiple floors
            ChangeFloor(0);
        }

        FallTime += delta;
        if (FallTime >= maxFallTime) {
            Falling = false;
            FallTime = 0;

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
        List<CollisionObject2D> collisionObjects = new List<CollisionObject2D>() { this };
        /*
        If a moveable has other moveables as children, make sure they change floors with it
        For example, barbs stuck in a character

        If a moveable has collision objects as children, shift their masks as well
        For example, a character's NearbyBodySensor Area2D
        */
        foreach (var node in this.GetChildren()) {
            if (node is Moveable m) {
                m.ChangeFloor(targetFloor);
            } else if (node is CollisionObject2D collisionObject) {
                collisionObjects.Add(collisionObject);
            }
        }
        
        //Avoid shifting the world bound mask
        var worldBoundMask = (uint)Math.Pow(2, CollisionConfig.WORLD_BOUNDS_LAYER - 1);

        foreach (var collisionObject in collisionObjects) {
            collisionObject.CollisionMask -= worldBoundMask;
            if (shift > 0) {
                collisionObject.CollisionLayer = collisionObject.CollisionLayer << shift * CollisionConfig.LAYERS_PER_FLOOR;
                collisionObject.CollisionMask = collisionObject.CollisionMask << shift * CollisionConfig.LAYERS_PER_FLOOR;
            } else if (shift < 0) {
                collisionObject.CollisionLayer = collisionObject.CollisionLayer >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
                collisionObject.CollisionMask = collisionObject.CollisionMask >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
            }
            collisionObject.CollisionMask += worldBoundMask;
        }
    }

    public void SetInside(bool inside) {
        if (Material != null)
        {
            Material.Set("shader_parameter/is_inside", inside);
        }

        Sprite.LightMask = inside ? 2 : 1;
    }
}
