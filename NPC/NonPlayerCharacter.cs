using ExtensionMethods;
using Godot;
using System;

public partial class NonPlayerCharacter : CharacterBody2D
{
    [Export]
    public float MoveSpeed = 100.0f;

    [Export]
    public float MoveAccel = 5f;

    [Export]
    public float MaxHealth = 100.0f;

    // Which logical grouping of characters in the scene this character is part of.
    // TODO: Should probably be an enum? Suggested values: [Hostile, Friendly, Player, Neutral].
    [Export]
    public string Group = "Hostile";

    public float CurrentHealth { get; private set; }

    public NavigationAgent2D NavAgent { get; private set; } = null;

    public Vector2 MovementTarget
    {
        get { return NavAgent.TargetPosition; }
        set { NavAgent.TargetPosition = value; }
    }

    // Cached reference to the collision shape defined on the .tscn
    public CollisionShape2D CollisionShape { get; private set; }

    // A Signal that other elements can (be) subscribe(d) to in order to hear about updates to character health.
    [Signal]
    public delegate void HealthChangedEventHandler(NonPlayerCharacter character, float newHealth, float oldHealth);

    private Node2D enemyTarget = null;

    private float HitAnimationSeconds = 0.1f;
    private Timer HitAnimationTimer;

    private Vector2 LastSeenDirection = Vector2.Zero;

    public override void _Ready()
    {
        AddToGroup(Group, true);
        CollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        CurrentHealth = MaxHealth;

        // SetupNavAgent awaits() a signal so we want to make sure we don't call it from _Ready().
        Callable.From(SetupNavAgent).CallDeferred();

        HitAnimationTimer = new Timer();
        HitAnimationTimer.OneShot = true;
        HitAnimationTimer.Timeout += RemoveHitMaterial;
        AddChild(HitAnimationTimer);
    }

    private async void SetupNavAgent()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        NavAgent = new NavigationAgent2D();
        NavAgent.AvoidanceEnabled = true;
        // TODO: Probably derive the radius from the CollisionShape or something?
        NavAgent.Radius = 50.0f;
        NavAgent.NeighborDistance = NavAgent.Radius * 1.05f; // Not sure how to set this smarter.
        // We tell the NavAgent what velocity we want to go and it computes a slightly different velocity ensuring we don't run into other NPCs while still getting where
        // we want to go.
        NavAgent.VelocityComputed += OnVelocityComputed;

        //NavAgent.DebugEnabled = true;
        NavAgent.PathDesiredDistance = 5.0f;
        NavAgent.TargetDesiredDistance = 5.0f;
        AddChild(NavAgent);
    }

    public override void _Process(double delta)
    {
        if(enemyTarget == null)
        {
            enemyTarget = FindTarget();
        }

        if(NavAgent != null)
        {
            UpdateNavigation();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        //If moving faster than allowed (e.g. from explosion), just slow down
        if (Velocity.Length() < MoveSpeed + 0.1) { //Fudge factor since LimitLength() can return a vector with slightly higher length than specified (float precision)
            if (enemyTarget != null) { //Why is this null many frames?
                Vector2 nextPathPosition = NavAgent.GetNextPathPosition();
                var direction = (nextPathPosition - GlobalPosition).Normalized();
                Velocity = (Velocity + direction * MoveAccel).LimitLength(MoveSpeed);
                LastSeenDirection = direction;
            }
        } else {
            Velocity = (Velocity + -Velocity.Normalized() * MoveAccel);
        }

        // Request a new velocity from the NavAgent. It will provide the same/adjusted velocity back to us for actual movement during OnNavAgentVelocityComputed().
        if (NavAgent != null)
        {
            NavAgent.Velocity = Velocity;
        }
        else
        {
            OnVelocityComputed(Velocity);
        }
    }

    // Called each frame (after _PhysicsProcess) after RVO avoidance adjusts our requested velocity to avoid other NPCs.
    // Note: safeVelocity has *already* been multiplied by the frame delta-time, which is why that parameter isn't present.
    private void OnVelocityComputed(Vector2 safeVelocity)
    {
        Velocity = safeVelocity;
        // The engine uses a fixed timestep for physics, so this is always a constant value at runtime.
        float physicsTickDelta = 1.0f / Engine.PhysicsTicksPerSecond;

        // Orient to face the direction the NPC is moving. Lerping by the physicsTickDelta smooths this out so the rotation doesn't change erratically,
        // and multiplying by a constant speeds up the how fast they turn.
        const int turnConstant = 4;
        GlobalRotation = Mathf.LerpAngle(GlobalRotation, LastSeenDirection.Angle(), physicsTickDelta * turnConstant);
        var collision = MoveAndCollide(safeVelocity * physicsTickDelta);
        if (collision != null)
        {
            OnCollide(collision);
        }
    }

    public void OnCollide(KinematicCollision2D collision)
    {
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

        if(CurrentHealth > 0)
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

    private Node2D FindTarget()
    {
        foreach(var player in this.GetGameWorld().Players)
        {
            if(player != null)
            {
                // TODO: Only select players that are within an aggro radius.
                return (Node2D)player;
            }
        }

        // TODO: Target the crystal(s) if no player is within aggro radius.
        return null;
    }

    private void UpdateNavigation()
    {
        if(enemyTarget == null && NavAgent.IsNavigationFinished())
        {
            GD.Print("reached");
            return;
        }

        if (enemyTarget != null)
        {
            MovementTarget = enemyTarget.GlobalPosition;
        }
    }

    private void SetHitMaterial() {
        ShaderMaterial hitMaterial = new ShaderMaterial();
        hitMaterial.Shader = GD.Load<Shader>("res://Shaders/hit.gdshader");
        Material = hitMaterial;
    }

    private void RemoveHitMaterial() {
        Material = null;
    }
}
