using ExtensionMethods;
using Godot;
using System;

public partial class Barb : Projectile
{
    // What percentage of the barb's total length should be embedded within targets it strikes. 0.5 = 50%
    [Export]
    protected float EmbeddedRatio = 0.10f;

    // The explosion scene to instantiate when the barb explodes.
    [Export]
    public PackedScene ExplosionTemplate { get; protected set; }

    protected float BarbLength { get; private set; }

    // Returns true if the barb instance has stuck inside of something.
    public bool Stuck { get; private set; } = false;

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

        Stuck = true;

        // Stop all motion immediately.
        Velocity = Vector2.Zero;
        // Disable collisions so we don't keep hitting things.
        CollisionLayer = 0;
        CollisionMask = 0;

        if(collision.GetCollider() is Node2D colliderNode) {
            this.Reparent(colliderNode);
            // Embed the barb within the surface it strikes (slightly).
            var embeddingOffset = Vector2.FromAngle(GlobalRotation) * BarbLength * EmbeddedRatio;
            GlobalPosition = GlobalPosition + embeddingOffset;

            //Move to the first child so the barb renders underneath the target
            colliderNode.MoveChild(this, 0);
        }
    }

    protected override void OnLifetimeExpired() {
        // Detonate ALL barbs attached to this object.
        if(Stuck) {
            foreach (var child in GetParent()?.GetChildren()) {
                if (child is Barb barb) {
                    barb.Detonate();
                    barb.QueueFree();
                }
            }
        }
        else {
            Detonate();
            QueueFree();
        }
    }

    protected void Detonate() {
        var explosion = ExplosionTemplate?.Instantiate<Explosion>();
        if (explosion != null) {
            // Communicate the original instigator, so that characters receiving damage know who did it.
            explosion.Instigator = Instigator;
            explosion.CollisionLayer = explosion.CollisionLayer << CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
            explosion.CollisionMask = explosion.CollisionMask << CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;

            GetParent().AddChild(explosion);
            explosion.GlobalPosition = GlobalPosition;
        }
    }
}
