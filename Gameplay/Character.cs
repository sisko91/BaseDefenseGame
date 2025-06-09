using ExtensionMethods;
using Godot;
using Godot.Collections;
using System;

// HitResult is a simple data structure used to capture and communicate information about an impact.
// This is a lightweight and simpler alternative to KinematicCollision2D, and carries additional info not captured there.
public struct HitResult
{
    // The incidence angle of the hit. If a laser beam A strikes a flat target B, the normal is the angle that the laser beam approached
    // B at (in world-space). Typically calculated by the colliding objects as part of the hit calculation.
    public Vector2 ImpactNormal;

    // The location of impact for the hit. Typically calculated by the colliding objects as part of the hit calculation.
    // This is always in world-space coordinates.
    public Vector2 ImpactLocation;

    // The force of knockback to apply to the hit object. The units for this value is not defined. See Character.ReceiveHit for
    // interpretation of this parameter.
    public float KnockbackForce;

    public HitResult(KinematicCollision2D collision = null, float kbForce = 0) {
        if(collision != null) {
            ImpactNormal = collision.GetNormal();
            ImpactLocation = collision.GetPosition();
        }
        KnockbackForce = kbForce;
    }
}

public partial class Character : Moveable, IImpactMaterial
{
    #region Stats

    [Export]
    public float MovementSpeed = 400.0f;

    [Export]
    public float MaxHealth = 100.0f;

    public float CurrentHealth { get; protected set; }

    public bool Stunned { get; protected set; }

    public Vector2 Knockback {  get; set; }

    // A Signal that other elements can (be) subscribe(d) to in order to hear about updates to character health.
    [Signal]
    public delegate void HealthChangedEventHandler(Character character, float newHealth, float oldHealth);

    #endregion

    [Export]
    public bool CanDamageSelf = false;

    // Cached reference to the NearbyBodySensor defined on the .tscn
    public BodySensor NearbyBodySensor { get; protected set; }

    #region Interface: IImpactMaterial

    // ImpactSourceType satisfies IImpactMaterial interface.
    [Export]
    public IImpactMaterial.ImpactType ImpactSourceType { get; protected set; } = IImpactMaterial.ImpactType.Default;

    // DefaultResponseHint satisfies IImpactMaterial interface. For characters this should typically be null unless the character
    // ever impacts other things directly (such as one character being the source of an impact to another character, which is unlikely
    // because the attacking character would most likely have a weapon making the actual impact not themselves).
    public PackedScene DefaultResponseHint { get; protected set; } = null;

    // ImpactResponseTable satisfies IImpactMaterial interface.
    [Export]
    public Dictionary<IImpactMaterial.ImpactType, PackedScene> ImpactResponseTable { get; protected set; } = [];

    #endregion

    // Nearby interaction areas that have announced themselves to this character. Interaction areas do this automatically for characters detected in their proximity.
    public Godot.Collections.Array<InteractionArea> NearbyInteractions;

    protected float HitAnimationSeconds = 0.1f;
    protected Timer HitTimer;
    protected Timer StunTimer;
    
    // Overriding the default that all Moveables receive so that characters can have a different default size / shape.
    protected override PackedScene DefaultGrassDisplacementMarkerScene => GD.Load<PackedScene>("res://World/Environment/Rendering/DisplacementMasks/Grass/character_grass_displacement_marker.tscn");

    public override void _Ready()
    {
        base._Ready();
        
        AddToGroup("Character");
        
        CurrentHealth = MaxHealth;
        Stunned = false;

        NearbyBodySensor = GetNode<BodySensor>("NearbyBodySensor");
        NearbyInteractions = new Godot.Collections.Array<InteractionArea>();

        HitTimer = new Timer();
        HitTimer.OneShot = true;
        HitTimer.Timeout += DisableHitShader;

        StunTimer = new Timer();
        StunTimer.OneShot = true;
        StunTimer.Timeout += () => { Stunned = false; };

        AddChild(HitTimer);
        AddChild(StunTimer);
    }

    public override void _Process(double delta) {
        base._Process(delta);
        
        if (DebugConfig.Instance.DRAW_COLLISION_BODY_RADIUS) {
            DrawCollisionBodyRadius();
        }
        if (DebugConfig.Instance.DRAW_COLLISION_BOUNDING_BOX) {
            DrawCollisionBoundingBox();
        }
    }

    // Process an incoming impact from the sourceNode. The impact is calculated by the other collider, i.e. impact.Collider == this.
    public void ReceiveHit(HitResult hitResult, float damage, IInstigated source)
    {
        if (!CanDamageSelf && source?.Instigator == this)
        {
            // Disallow damage from anything instigated by ourself.
            return;
        }
        
        //Repeated calls reset the timer
        EnableHitShader();
        HitTimer.Start(HitAnimationSeconds);

        var oldHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
        //GD.Print($"{Name} taking {damage} damage, {hitResult.KnockbackForce} knockback from {source?.Instigator?.Name}");
        // Broadcast the damage received to anyone listening.
        EmitSignal(SignalName.HealthChanged, this, CurrentHealth, oldHealth);

        if (CurrentHealth > 0)
        {
            if(hitResult.KnockbackForce > 0) {
                var kbDirection = hitResult.ImpactNormal.Normalized();
                var kbVelocity = kbDirection * hitResult.KnockbackForce;

                // Render the impact angle if debugging is enabled.
                //this.DrawDebugLine(GlobalPosition, GlobalPosition + kbDirection * 100, new Color(1, 0, 0), 2.0f);

                Stunned = true;
                StunTimer.Start(1);
                Knockback += kbVelocity;
            }
        }
        else
        {
            Die();
        }
    }

    protected void Die()
    {
        //die
        QueueFree();
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
        var boundingRect = ((CollisionShape2D) CollisionShape).Shape.GetRect();
        var collisionDiameter = Mathf.Max(boundingRect.Size.X, boundingRect.Size.Y);
        return collisionDiameter / 2;
    }

    //The hitbox collisions and projectiles currently interact with
    public void DrawCollisionBoundingBox() {
        var boundingRect = ((CollisionShape2D)CollisionShape).Shape.GetRect();
        this.ClearDebugDrawCallGroup(GetPath() + "bb");
        this.DrawDebugRect(CollisionShape.GlobalPosition, boundingRect.Size, new Color(0, 0, 1), false, 1, GetPath() + "bb");
    }

    //The circle used for padding distance checks when checking if an enemy is in range for attacks
    public void DrawCollisionBodyRadius() {
        this.ClearDebugDrawCallGroup(GetPath() + "Radius");
        this.DrawDebugCircle(GlobalPosition, GetCollisionBodyRadius(), new Color(0, 0, 1), false, 1, GetPath() + "Radius");
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
