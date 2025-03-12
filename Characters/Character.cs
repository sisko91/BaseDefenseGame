using Godot;
using System;

public partial class Character : CharacterBody2D
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

    // The character's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // characters enter their InteriorRegions.
    public int CurrentElevationLevel = 0;
    public InteriorRegion CurrentRegion { get; set; }

    // Cached reference to the NearbyBodySensor defined on the .tscn
    public BodySensor NearbyBodySensor { get; protected set; }

    // Nearby interaction areas that have announced themselves to this character. Interaction areas do this automatically for characters detected in their proximity.
    public Godot.Collections.Array<InteractionArea> NearbyInteractions;

    protected float HitAnimationSeconds = 0.1f;
    protected Timer HitAnimationTimer;

    public Character() : base()
    {
        //Better for 2d top-down
        MotionMode = MotionModeEnum.Floating;
    }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;

        NearbyBodySensor = GetNode<BodySensor>("NearbyBodySensor");
        NearbyInteractions = new Godot.Collections.Array<InteractionArea>();

        HitAnimationTimer = new Timer();
        HitAnimationTimer.OneShot = true;
        HitAnimationTimer.Timeout += RemoveHitMaterial;
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
        SetHitMaterial();


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

    private void SetHitMaterial()
    {
        ShaderMaterial hitMaterial = new ShaderMaterial();
        hitMaterial.Shader = GD.Load<Shader>("res://Shaders/hit.gdshader");
        Material = hitMaterial;
    }

    private void RemoveHitMaterial()
    {
        Material = null;
    }

    public virtual void ChangeFloor(bool goingUp) {
        if (goingUp) {
            CollisionLayer = CollisionLayer << CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask << CollisionConfig.LAYERS_PER_FLOOR;
            NearbyBodySensor.CollisionLayer  = NearbyBodySensor.CollisionLayer << CollisionConfig.LAYERS_PER_FLOOR;
            NearbyBodySensor.CollisionMask = NearbyBodySensor.CollisionMask << CollisionConfig.LAYERS_PER_FLOOR;
            CurrentElevationLevel += 1;
        } else {
            CollisionLayer = CollisionLayer >> CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask >> CollisionConfig.LAYERS_PER_FLOOR;
            NearbyBodySensor.CollisionLayer = NearbyBodySensor.CollisionLayer >> CollisionConfig.LAYERS_PER_FLOOR;
            NearbyBodySensor.CollisionMask = NearbyBodySensor.CollisionMask >> CollisionConfig.LAYERS_PER_FLOOR;
            CurrentElevationLevel -= 1;
        }
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
