using Godot;
using System;

public partial class Barb : Projectile
{
    // What percentage of the barb's total length should be embedded within targets it strikes. 0.5 = 50%
    [Export]
    protected float EmbeddedRatio = 0.10f;

    protected float BarbLength { get; private set; }

    public Barb() {
        // Barbs stick into surfaces and shouldn't destroy when they collide with stuff.
        DestroyOnNextCollision = false;
    }

    public override void _Ready() {
        base._Ready();

        var collider = GetNode<CollisionShape2D>("CollisionShape2D");
        BarbLength = collider.Shape.GetRect().Size.X;
    }

    protected override void OnCollide(KinematicCollision2D collision) {
        base.OnCollide(collision);

        // Stop all motion immediately.
        Velocity = Vector2.Zero;
        // Disable collisions so we don't keep hitting things.
        CollisionLayer = 0;
        CollisionMask = 0;

        if(collision.GetCollider() is Node2D colliderNode) {
            // Attach to the object the barb hit using the current location, rotation, etc.
            var globalPos = GlobalPosition;
            var globalRot = GlobalRotation;
            var globalScale = GlobalScale;
            GetParent()?.RemoveChild(this);
            
            colliderNode.AddChild(this);
            GlobalRotation = globalRot;
            GlobalScale = globalScale;
            // Embed the barb within the surface it strikes (slightly).
            var embeddingOffset = Vector2.FromAngle(globalRot) * BarbLength * EmbeddedRatio;
            GlobalPosition = globalPos + embeddingOffset;
            

            // TODO: Adjust Z-layer to be just below the target (so that we don't render on top).
            //       This can't be enabled right now because we don't have enough Z-layers defined (Ref: https://app.asana.com/0/0/1209778766176007).
            //ZIndex = colliderNode.ZIndex - 1;
        }
    }

    protected override void OnLifetimeExpired() {
        // TODO: Explode, doing damage to the target and applying knockback.
        
    }

    protected void Detonate() {
        GD.Print("BOOM!");

        QueueFree();
    }
}
