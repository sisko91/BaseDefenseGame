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
    public IImpactMaterial.MaterialType ImpactMaterialType { get; protected set; } = IImpactMaterial.MaterialType.Bullet;

    // DefaultResponseHint satisfies IImpactMaterial interface.
    [Export]
    public PackedScene DefaultResponseHint { get; protected set; } = null;

    [Export]
    public bool DestroyedByExplosions = false;

    // ImpactResponseTable satisfies IImpactMaterial interface.
    // Note this should somewhat rarely be populated for projectiles. These are *responses* so it only matters if the projectile itself
    // can be impacted by other things.
    public Dictionary<IImpactMaterial.MaterialType, PackedScene> ImpactResponseTable { get; protected set; } = [];

    public string AllProjectilesGroup = "Projectiles";

    private Timer ProjectileTimer;

    public override void _Ready() {
        AddToGroup(AllProjectilesGroup, true);
    }

    // Adds the projectile to the gameworld and initializes its position, velocity, etc.
    public void Start(Vector2 worldPosition, float worldDirection, Character instigator)
    {
        GlobalRotation = worldDirection;
        GlobalPosition = worldPosition;
        Velocity = new Vector2(InitialSpeed, 0).Rotated(GlobalRotation);
        Instigator = instigator;

        if (Instigator is Character c) {
            CurrentElevationLevel = c.CurrentElevationLevel;
            CollisionLayer = CollisionLayer << c.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask << c.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
        }
        
        ProjectileTimer = new Timer();
        ProjectileTimer.OneShot = true;
        ProjectileTimer.WaitTime = LifetimeSeconds;
        ProjectileTimer.Timeout += OnLifetimeExpired;
        AddChild(ProjectileTimer);

        this.GetGameWorld().AddChild(this);
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
        var collision = MoveAndCollide(Velocity * (float)delta);
        if (collision != null)
        {
            OnCollide(collision);
        }
        if(OrientToVelocity && !Velocity.IsZeroApprox()) {
            GlobalRotation = Velocity.Angle();
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
            collidedWith.TryRegisterImpact(hr, this, Damage);
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
