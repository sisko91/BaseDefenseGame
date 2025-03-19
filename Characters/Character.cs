using ExtensionMethods;
using Godot;
using System;

public partial class Character : Moveable
{
    #region Stats

    [Export]
    public float MovementSpeed = 400.0f;

    [Export]
    public float MaxHealth = 100.0f;

    public float CurrentHealth { get; protected set; }

    // A Signal that other elements can (be) subscribe(d) to in order to hear about updates to character health.
    [Signal]
    public delegate void HealthChangedEventHandler(Character character, float newHealth, float oldHealth);

    #endregion

    [Export]
    public bool CanDamageSelf = false;

    // Cached reference to the NearbyBodySensor defined on the .tscn
    public BodySensor NearbyBodySensor { get; protected set; }

    // Cached reference to the collision shape defined on the .tscn
    public CollisionShape2D CollisionShape { get; private set; }

    // Nearby interaction areas that have announced themselves to this character. Interaction areas do this automatically for characters detected in their proximity.
    public Godot.Collections.Array<InteractionArea> NearbyInteractions;

    protected float HitAnimationSeconds = 0.1f;
    protected Timer HitAnimationTimer;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;

        CollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        NearbyBodySensor = GetNode<BodySensor>("NearbyBodySensor");
        NearbyInteractions = new Godot.Collections.Array<InteractionArea>();

        HitAnimationTimer = new Timer();
        HitAnimationTimer.OneShot = true;
        HitAnimationTimer.Timeout += DisableHitShader;
        AddChild(HitAnimationTimer);
    }

    // Process an incoming impact from the sourceNode. The impact is calculated by the other collider, i.e. impact.Collider == this.
    public void ReceiveHit(KinematicCollision2D impact, float damage, Node2D source)
    {
        // TODO: This is shitty but I haven't worked out the right way to bake this into a base "DamageSource" type that doesn't shoehorn every damage source into
        // being the same thing (e.g. CharacterBody2D).
        if(source is Projectile projectile)
        {
            if (!CanDamageSelf && (source.FindCharacterAncestor() == this || projectile.Instigator == this))
            {
                // Disallow damage from anything 
                return;
            }
        }
        
        //Repeated calls reset the timer
        HitAnimationTimer.Start(HitAnimationSeconds);
        EnableHitShader();


        var oldHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
        // Broadcast the damage received to anyone listening.
        EmitSignal(SignalName.HealthChanged, this, CurrentHealth, oldHealth);

        if (CurrentHealth > 0)
        {
            // knockback - neither of the computations of kbDirection below actually give us what we want so we just calculate
            // the incident angle from positions.
            //var kbDirection = bullet.Velocity.Normalized();
            //var kbDirection = impact.GetAngle() - Mathf.Pi;
            var kbDirection = (GlobalPosition - source.GlobalPosition).Normalized();
            var kbVelocity = kbDirection * damage * 5;
            // Render the impact angle if debugging is enabled.
            //this.DrawDebugLine(GlobalPosition, GlobalPosition + kbVelocity, new Color(1, 0, 0), 2.0f);

            // Just use the damage as the momentum transferred, essentially.
            Velocity += kbVelocity;
        }
        else
        {
            //die
            QueueFree();
        }
    }

    public void InteractWithNearestObject() {
        InteractionArea target = null;
        foreach (var candidate in NearbyInteractions) {
            if (target == null) {
                target = candidate;
            } else {
                var targetSq = target.GlobalPosition.DistanceSquaredTo(GlobalPosition);
                var candidateSq = candidate.GlobalPosition.DistanceSquaredTo(GlobalPosition);
                if (targetSq > candidateSq) {
                    target = candidate;
                }
            }
        }

        if (target != null) {
            target.Interact(this);
        }
    }

    public virtual void ChangeFloor(int targetFloor) {
        if (targetFloor == CurrentElevationLevel) {
            return;
        }

        NearbyInteractions.Clear();
        int shift = targetFloor - CurrentElevationLevel;

        //Don't shift the world layer when changing floors
        var worldBoundMask = (uint)Math.Pow(2, CollisionConfig.WORLD_BOUNDS_LAYER - 1);
        CollisionMask -= worldBoundMask;
        base.ChangeFloor(targetFloor);
        CollisionMask += worldBoundMask;

        if (shift > 0) {
            NearbyBodySensor.CollisionLayer = NearbyBodySensor.CollisionLayer << shift * CollisionConfig.LAYERS_PER_FLOOR;
            NearbyBodySensor.CollisionMask = NearbyBodySensor.CollisionMask << shift * CollisionConfig.LAYERS_PER_FLOOR;
        } else if (shift < 0) {
            NearbyBodySensor.CollisionLayer = NearbyBodySensor.CollisionLayer >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
            NearbyBodySensor.CollisionMask = NearbyBodySensor.CollisionMask >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
        }
    }

    private void EnableHitShader()
    {
        if (Material != null) {
            Material.Set("shader_parameter/is_hit", true);
        }
    }

    private void DisableHitShader()
    {
        if (Material != null) {
            Material.Set("shader_parameter/is_hit", false);
        }
    }

    public float GetCollisionBodyRadius()
    {
        var boundingRect = CollisionShape.Shape.GetRect();
        if (DebugConfig.Instance.DRAW_COLLISION_BODY_RADIUS) {
            this.ClearLines(GetPath() + "bb");
            this.DrawDebugRect(GlobalPosition, boundingRect, new Color(0, 0, 1), 1, GetPath() + "bb");
        }
        var collisionDiameter = Mathf.Max(boundingRect.Size.X, boundingRect.Size.Y);
        return collisionDiameter / 2;
    }
}

public static class CharacterNodeExtensions
{
    // Walks the node's local tree hierarchy until it finds the parent Character. May return null if no character exists.
    public static Character FindCharacterAncestor(this Node node)
    {
        while(node != null)
        {
            if(node is Character character)
            {
                return character;
            }
            node = node.GetParent();
        }
        return null;
    }
}
