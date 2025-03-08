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

    // The character's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // characters enter their InteriorRegions.
    public int CurrentElevationLevel = 0;

    // Cached reference to the NearbyBodySensor defined on the .tscn
    public BodySensor NearbyBodySensor { get; protected set; }

    protected float HitAnimationSeconds = 0.1f;
    protected Timer HitAnimationTimer;


    public override void _Ready()
    {
        CurrentHealth = MaxHealth;

        NearbyBodySensor = GetNode<BodySensor>("NearbyBodySensor");

        HitAnimationTimer = new Timer();
        HitAnimationTimer.OneShot = true;
        HitAnimationTimer.Timeout += RemoveHitMaterial;
        AddChild(HitAnimationTimer);
    }

    // Process an incoming impact from the sourceNode. The impact is calculated by the other collider, i.e. impact.Collider == this.
    public void ReceiveHit(KinematicCollision2D impact, Node2D sourceNode, float damage)
    {
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
            var kbDirection = (GlobalPosition - sourceNode.GlobalPosition).Normalized();
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
}
