using ExtensionMethods;
using Godot;
using Godot.Collections;
using System;

public partial class Projectile : Moveable, IInstigated, IImpactMaterial
{
    // Instigator property satisfies IInstigated interface.
    public Character Instigator { get; set; }

    [Export]
    public float InitialSpeed = 750.0f;

    // When true, the projectile's global rotation will be continuously updated to match its velocity each tick.
    [Export]
    public bool OrientToVelocity = false;

    [Export]
    public float LifetimeSeconds = 3.0f;

    [ExportCategory("Impacts")]
    // When true, the next collision for this projectile will result in it QueueFree()'ing itself.
    [Export]
    public bool DestroyOnNextCollision = true;

    // How much damage the projectile will do to targets it directly hits.
    [Export]
    public float Damage = 20.0f;

    // The base force of knockback applied to the struck object by this projectile.
    [Export]
    public float KnockbackForce = 0;

    // ImpactMaterialType satisfies IImpactMaterial interface.
    [Export]
    public IImpactMaterial.ImpactType ImpactSourceType { get; protected set; } = IImpactMaterial.ImpactType.Bullet;

    // DefaultResponseHint satisfies IImpactMaterial interface.
    [Export]
    public PackedScene DefaultResponseHint { get; protected set; } = null;

    [Export]
    public bool DestroyedByExplosions = false;

    // ImpactResponseTable satisfies IImpactMaterial interface.
    // Note this should somewhat rarely be populated for projectiles. These are *responses* so it only matters if the projectile itself
    // can be impacted by other things.
    public Dictionary<IImpactMaterial.ImpactType, PackedScene> ImpactResponseTable { get; protected set; } = [];

    public string AllProjectilesGroup = "Projectiles";

    private Timer ProjectileTimer;
    
    // Overriding the default that all Moveables receive so that projectiles can have a different default size / shape.
    protected override PackedScene DefaultGrassDisplacementMarkerScene => GD.Load<PackedScene>("res://World/Environment/Rendering/DisplacementMasks/Grass/projectile_grass_displacement_marker.tscn");

    public override void _Ready()
    {
        base._Ready();
        AddToGroup(AllProjectilesGroup, true);
        AffectedByGravity = false;
    }

    // Adds the projectile to the gameworld and initializes its position, velocity, etc.
    public void Start(Vector2 worldPosition, float worldDirection, Character instigator)
    {
        ChangeFloor(instigator.CurrentElevationLevel);
        this.GetGameWorld().Middleground.AddChild(this);

        Sprite.Rotation = worldDirection;
        CollisionShape.Rotation = worldDirection;
        GlobalPosition = worldPosition - Sprite.Position;
        Velocity = new Vector2(InitialSpeed, 0).Rotated(worldDirection);
        Instigator = instigator;

        ProjectileTimer = new Timer();
        ProjectileTimer.OneShot = true;
        ProjectileTimer.WaitTime = LifetimeSeconds;
        ProjectileTimer.Timeout += OnLifetimeExpired;
        AddChild(ProjectileTimer);

        Falling = instigator.Falling;
        //Handle edge case where player is standing on edge of roof and spawns projectile off-roof
        if (instigator.CurrentRegion != null && !instigator.CurrentRegion.OverlapsBodyAccurate(this)) {
            Falling = true;
        }

        if (Falling) {
            FallTime = instigator.FallTime;
        }

        ProjectileTimer.Start();
        OnStart();
    }

    public void ForceExpire() {
        ProjectileTimer.Stop();
        ProjectileTimer.EmitSignal("timeout");
    }

    // Like AddCollisionExceptionWith() but auto-expires the exception after a short duration.
    public void AddTemporaryCollisionException(PhysicsBody2D otherBody, float duration = 0.2f) {
        AddCollisionExceptionWith(otherBody);

        var timer = new Timer {
            WaitTime = duration,
            OneShot = true,
            Autostart = true
        };
        AddChild(timer);
        timer.Timeout += () => RemoveCollisionExceptionWith(otherBody);
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;
        if (Falling && AffectedByGravity) {
            velocity = HandleFalling(delta);
        }
        var collision = MoveAndCollide(velocity * (float)delta, false, 1);
        if (collision != null)
        {
            OnCollide(collision);
        }
        if(OrientToVelocity && !Velocity.IsZeroApprox()) {
            CollisionShape.Rotation = Velocity.Angle();
            Sprite.Rotation = Velocity.Angle();
        }
    }

    protected virtual void OnStart() {
        // Override in children.
    }

    protected virtual void OnCollide(KinematicCollision2D collision)
    {
        var hr = new HitResult(collision, KnockbackForce);
        // KinematicCollision2D's normal is going to be the surface normal of the thing this projectile hit, and will point back toward the projectile.
        // In order for this to be useful as a HitResult, the normal needs to be reversed to point in the direction of impact.
        hr.ImpactNormal *= -1;
        if(collision.GetCollider() is Node collidedWith) {
            this.TryRegisterImpact(collidedWith, hr, Damage);
        }

        if(DestroyOnNextCollision) {
            QueueFree();
        }
    }

    // Children can override this to decide what happens when the projectile times out. Default behavior is deletion.
    protected virtual void OnLifetimeExpired() {
        QueueFree();
    }
}
