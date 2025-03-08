using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

public partial class NonPlayerCharacter : Character
{
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

    //Context-based steering
    private int Directions = 16;
    private List<float> Interest = new List<float>();
    private List<float> Danger = new List<float>();

    //TODO: Make project level flags
    private static bool DEBUG_STEERING = false;

    public override void _Ready()
    {
        base._Ready();

        AddToGroup(Group, true);
        CollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        CurrentHealth = MaxHealth;

        NearbyBodySensor.PlayerSensed += OnPlayerSensed;
        NearbyBodySensor.NpcSensed += OnNpcSensed;

        // SetupNavAgent awaits() a signal so we want to make sure we don't call it from _Ready().
        Callable.From(SetupNavAgent).CallDeferred();

        HitAnimationTimer = new Timer();
        HitAnimationTimer.OneShot = true;
        HitAnimationTimer.Timeout += RemoveHitMaterial;
        AddChild(HitAnimationTimer);

        for (int i = 0; i < Directions; i++) {
            Interest.Add(0);
            Danger.Add(0);
        }

        //Better for 2d top-down
        MotionMode = MotionModeEnum.Floating;
    }

    private async void SetupNavAgent()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        NavAgent = new NavigationAgent2D();
        NavAgent.DebugEnabled = false;
        NavAgent.AvoidanceEnabled = true;
        // TODO: Probably derive the radius from the CollisionShape or something?
        NavAgent.Radius = 75.0f;
        NavAgent.NeighborDistance = NavAgent.Radius * 1.5f; // Not sure how to set this smarter.
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
        if (Velocity.Length() < MovementSpeed + 0.1) { //Fudge factor since LimitLength() can return a vector with slightly higher length than specified (float precision)
            if (enemyTarget != null) { //Why is this null many frames?
                this.ClearLines(GetPath());
                var pathDirection = GlobalPosition.DirectionTo(NavAgent.GetNextPathPosition());
                SetInterest(pathDirection);
                SetDanger();

                var direction = ChooseDirection();
                Velocity = (Velocity + direction * MoveAccel).LimitLength(MovementSpeed);
                LastSeenDirection = pathDirection;
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
        // The engine uses a fixed timestep for physics, so this is always a constant value at runtime.
        float physicsTickDelta = 1.0f / Engine.PhysicsTicksPerSecond;

        // Orient to face the direction the NPC is moving. Lerping by the physicsTickDelta smooths this out so the rotation doesn't change erratically,
        // and multiplying by a constant speeds up the how fast they turn.
        const int turnConstant = 4;
        Velocity = Velocity.MoveToward(safeVelocity, 0.25f);
        var lookAngle = Velocity.Angle();
        //Look along the nav path if stuck
        if (GetRealVelocity().Length() < 0.1f * MovementSpeed) {
            lookAngle = LastSeenDirection.Angle();
        //Look at the player if close
        } else if (NearbyBodySensor.Players.Count > 0 && GlobalPosition.DistanceTo(NearbyBodySensor.Players[0].GlobalPosition) < 100f) {
            lookAngle = GlobalPosition.DirectionTo(NearbyBodySensor.Players[0].GlobalPosition).Angle();
        }

        GlobalRotation = Mathf.LerpAngle(GlobalRotation, lookAngle, physicsTickDelta * turnConstant);
        bool collided = MoveAndSlide();
        if (collided) {
            OnCollide(GetLastSlideCollision());
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

    // This is currently just to test with.
    private void OnNpcSensed(NonPlayerCharacter npc, bool bSensed)
    {
        //Color senseColor = bSensed ? new Color(0, 1, 0) : new Color(1, 0, 0);
        //this.DrawDebugLine(npc.Position, Position, senseColor, 0.5);
    }

    // These is currently just to test with.
    private void OnPlayerSensed(Player player, bool bSensed)
    {
        //Color senseColor = bSensed ? new Color(0, 1, 0) : new Color(1, 0, 0);
        //this.DrawDebugLine(player.Position, Position, senseColor, 0.5);
    }

    private Vector2 ChooseDirection() {
        Vector2 direction = Vector2.Zero;
        Vector2 highestInterestDirection = Vector2.Zero;
        float maxInterest = 0;

        //Interest[0] = 0;
        for (int i = 0; i < Directions; i++) {
            //TODO: Can make this more complex than just canceling out. e.g. enemies further away subtract less interest
            if (Danger[i] > 0) {
                Interest[i] = 0;
                Interest[Mod(i + Directions / 2, Directions)] = 0.5f; //Give an interest boost to the opposite direction
            }

            float angle = i * 2 * (float) Math.PI / Directions;
            Vector2 interestDirection = Vector2.Right.Rotated(angle).Rotated(Rotation);
            direction += interestDirection * Interest[i];

            if (DEBUG_STEERING) {
                var color = Interest[i] <= 0 ? new Color(1, 0, 0) : new Color(0, 1, 0);
                var line = (interestDirection * Math.Abs(Interest[i]));
                if (Interest[i] == 0) {
                    color = new Color(1, 0, 0);
                    line = interestDirection.Normalized();
                    if (i >= Directions / 4 && i <= 3 * Directions / 4) {
                        line *= 0.25f;
                    }
                }
                this.DrawDebugLine(Position, Position + line * 100, color, 0.1, GetPath());
            }

            if (Interest[i] > maxInterest) {
                maxInterest = Interest[i];
                highestInterestDirection = interestDirection;
            }
        }

        //Uncomment to pick the highest interest path instead of cumulative interest. It gets caught on walls right now because those aren't detected by steering
        direction = highestInterestDirection;

        if (DEBUG_STEERING) {
            this.DrawDebugLine(Position, Position + direction.Normalized() * 150, new Color(1, 1, 0), 0.1, GetPath());
        }

        return direction.Normalized();
    }

    private void SetInterest(Vector2 pathDirection) {
        for (int i = 0; i < Directions;  i++) {
            var angle = i * 2 * Math.PI / Directions;
            Vector2 interestDirection = Vector2.Right.Rotated((float)angle).Rotated(Rotation);
            Interest[i] = Math.Max(0.1f, interestDirection.Dot(pathDirection));
            //this.DrawDebugLine(Position, Position + interestDirection * Interest[i] * 100, new Color(0, 1, 0), 0.1, GetPath());
        }
    }

    private void SetDanger() {
        for (int i = 0; i < Directions; i++) {
            Danger[i] = 0;
        }

        List<Node2D> potentialDangers = new List<Node2D>();
        potentialDangers.AddRange(NearbyBodySensor.NPCs);
        potentialDangers.AddRange(NearbyBodySensor.Walls);

        foreach (Node2D potentialDanger in potentialDangers) {
            //TODO: Have body sensor exclude npc it's attached to
            if (this == potentialDanger) {
                continue;
            }

            var distTo = GlobalPosition.DistanceTo(potentialDanger.GlobalPosition);
            var dirTo = GlobalPosition.DirectionTo(potentialDanger.GlobalPosition);
            var bucketAngle = dirTo.Angle() - GlobalRotation;
            //Shift angle up for easier bucketing. For instance, with 8 directions, the first bucket should be everything from -337.5 degress to 22.5 degrees. This would shift those values to 0 - 45 degrees
            bucketAngle += (float) (Math.PI / Directions);
            bucketAngle = GetBoundedAngle(bucketAngle);

            //Put in multiple buckets depending on angle. Raycasting would see a close object at multiple angles, so simulating that
            for (int i = 0; i < Directions; i++) {
                var angle = i * 2 * Math.PI / Directions;

                //50% overlap on grouping min, scale with distance
                var distScale = 1 - distTo / 128; //TODO: access sensor size
                var modifier = 1.5 + 3 * distScale;
                //GD.Print(modifier);
                if (Math.Abs(bucketAngle - angle) < (modifier * Math.PI / Directions)) {
                    Vector2 interestDirection = Vector2.Right.Rotated((float)angle).Rotated(Rotation);

                    //this.DrawDebugLine(Position, Position + interestDirection * 150, new Color(1, 0, 0), 0.1, GetPath());
                    Danger[i] = 1; //TODO: Can make this more complex, e.g. scaling for distance
                }
            }
        }
    }

    private float GetBoundedAngle(float angle) {
        while (angle < 0) {
            angle += (float) (2 * Math.PI);
        }
        while (angle > 2 * Math.PI) {
            angle -= (float) (2 * Math.PI);
        }

        return angle;
    }
    private int Mod(int x, int m) {
        int r = x % m;
        return r < 0 ? r + m : r;
    }
}
