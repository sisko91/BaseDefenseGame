using ExtensionMethods;
using Godot;
using System;

public partial class Grenade : Projectile, IInstigated
{
    [ExportCategory("> 'Physics'")]
    // Max number of times the grenade can bounce before it comes to a rest.
    [Export]
    protected int MaxBounces = 2;

    // How much of the grenade's velocity is preserved between bounces.
    [Export]
    protected float BounceFrictionCoefficient = 0.3f;

    // How many times this grenade has bounced so far.
    protected int CurrentBounce = 0;

    // Degrees per second that the grenade will rotate as it travels (at full speed). Does not affect velocity but MAY influence collisions.
    [Export]
    protected float FreeRotationRateDegrees = 360.0f;

    // Where the last bounce occurred.
    protected Vector2 lastBounceLocation;

    [ExportCategory("> Explosion")]
    // The explosion effect to spawn when the grenade timer expires.
    [Export]
    public PackedScene ExplosionTemplate { get; set; } = null;

    public override void _Ready() {
        base._Ready();
        // Grenades shouldn't destroy themselves after colliding. They handle this themselves within OnCollide(), Bounce(), and Settle().
        DestroyOnNextCollision = false;
        AffectedByGravity = true;
    }

    protected override void OnStart() {
        lastBounceLocation = GlobalPosition;
    }

    protected override void OnCollide(KinematicCollision2D collision) {
        base.OnCollide(collision);

        bool bMightBeAWall = !(collision.GetCollider() is Character or Projectile);
        if (bMightBeAWall && CurrentBounce < MaxBounces) {
            CurrentBounce += 1;
            Bounce(collision);
        }
        else {
            // Stop as soon as we hit something soft.
            Settle();
        }
    }

    // Common logic run when the grenade comes to rest, after being thrown but before expoding.
    protected void Settle() {
        // This will cause the grenade to rapidly decrease velocity.
        CurrentBounce = MaxBounces;

        // Disable collision with almost everything. Put in the interactions layer so explosions can detect this grenade
        CollisionLayer = (uint)Math.Pow(2, CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR + CollisionConfig.INTERACTIONS_LAYER - 1);
        CollisionMask = 0;

        if (CurrentRegion != null) {
            CurrentRegion.AddMonitoringException(this);
            Reparent(CurrentRegion.Background);
            CurrentRegion.RemoveMonitoringException(this);
        } else {
            Reparent(this.GetGameWorld().Background);
        }
    }

    protected void Bounce(KinematicCollision2D collision) {
        if(collision != null) {
            Velocity = Velocity.Bounce(collision.GetNormal()) * BounceFrictionCoefficient;
            lastBounceLocation = collision.GetPosition();
        }
        else {
            // We just bounced off the ground.
            // TODO: Add a small angular variance here to simulate rough terrain? For now just bounce forward.
            Velocity *= BounceFrictionCoefficient;
            lastBounceLocation = GlobalPosition;
        }
        // Reverse rotation direction
        FreeRotationRateDegrees *= -1;
    }

    protected override void OnLifetimeExpired() {
        SpawnExplosion();
        QueueFree();
    }

    private void SpawnExplosion() {
        var explosion = ExplosionTemplate?.Instantiate<Explosion>();
        if(explosion != null) {
            // Communicate the original instigator, so that characters receiving damage know who did it.
            explosion.Instigator = Instigator;

            //Ignore collisions until the explosion is placed in the desired position, otherwise it can trigger area entered events twice
            var collisionLayer = explosion.CollisionLayer << CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
            explosion.CollisionLayer = 0;
            explosion.CollisionMask = explosion.CollisionMask << CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;

            explosion.Visible = Visible;
            var parent = CurrentRegion?.Foreground ?? this.GetGameWorld().Foreground;
            parent.AddChild(explosion);
            explosion.GlobalPosition = GlobalPosition;
            explosion.CollisionLayer = collisionLayer;
        }
    }

    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);

        // Come to a rest once no more bounces are allowed.
        if (CurrentBounce == MaxBounces) {
            Velocity *= .8f;
        }

        var currentSpeed = Velocity.Length();
        if(CurrentBounce < MaxBounces) {
            // The grenade can travel at most its initial speed over 1 second as distance before bouncing. This is reduced each time the grenade bounces to simulate friction.
            var maxDistancePerBounce = currentSpeed / (CurrentBounce + 1);
            var distanceFromBounce = lastBounceLocation.DistanceTo(GlobalPosition);
            if (distanceFromBounce > maxDistancePerBounce) {
                // TODO: Add a small angular variance here to simulate rough terrain? For now just bounce forward.
                CurrentBounce += 1;
                Bounce(null);

                // If that was our final bounce then come to a rest.
                if(CurrentBounce == MaxBounces) {
                    Settle();
                }
            }
        }

        // Based on how fast/slow the projectile is going relative to its thrown speed, rotate through the air.
        float speedRatio = currentSpeed / InitialSpeed;
        float newRotationDegrees = FreeRotationRateDegrees * speedRatio * (float)delta;
        Rotation += Mathf.DegToRad(newRotationDegrees);
    }
}
