using Godot;

public partial class NonPlayerCharacter : Character
{
    [Export]
    public float MoveAccel = 5f;

    // Which logical grouping of characters in the scene this character is part of.
    // TODO: Should probably be an enum? Suggested values: [Hostile, Friendly, Player, Neutral].
    [Export]
    public string Group = "Hostile";

    public NavigationAgent2D NavAgent { get; private set; } = null;

    // Cached reference to the collision shape defined on the .tscn
    public CollisionShape2D CollisionShape { get; private set; }

    private Brain Brain = null;

    // RotationGoal is where the NPC should be looking / rotated towards. The character will rotate gradually towards their rotation goal.
    // TODO: Expose RotationSpeed as configureable? 
    public float RotationGoal = 0;

    public override void _Ready()
    {
        base._Ready();

        AddToGroup(Group, true);        

        NearbyBodySensor.PlayerSensed += OnPlayerSensed;
        NearbyBodySensor.NpcSensed += OnNpcSensed;

        // SetupNavAgent awaits() a signal so we want to make sure we don't call it from _Ready().
        Callable.From(SetupNavAgent).CallDeferred();

        //Better for 2d top-down
        MotionMode = MotionModeEnum.Floating;

        Brain = new Brain();
        Brain.Initialize(this);

        RotationGoal = GlobalRotation;
    }

    private async void SetupNavAgent()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        NavAgent = new NavigationAgent2D();
        NavAgent.DebugEnabled = DebugConfig.DRAW_NAVIGATION;
        // TODO: Probably derive the radius from the CollisionShape or something?
        NavAgent.Radius = 75.0f;
        NavAgent.NeighborDistance = NavAgent.Radius * 1.5f; // Not sure how to set this smarter.
        NavAgent.PathDesiredDistance = 5.0f;
        NavAgent.TargetDesiredDistance = 5.0f;
        AddChild(NavAgent);
    }

    public override void _Process(double delta)
    {
        if(Brain != null)
        {
            Brain.Think(delta);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if(Brain != null)
        {
            Brain.ThinkPhysics(delta);
        }

        // The brain may have set a rotation goal or our velocity.

        // The engine uses a fixed timestep for physics, so this is always a constant value at runtime.
        float physicsTickDelta = 1.0f / Engine.PhysicsTicksPerSecond;

        const int turnConstant = 4;
        if(!Mathf.IsEqualApprox(GlobalRotation, RotationGoal))
        {
            GlobalRotation = Mathf.LerpAngle(GlobalRotation, RotationGoal, physicsTickDelta * turnConstant);
        }
        bool collided = MoveAndSlide();
        if (collided)
        {
            OnCollide(GetLastSlideCollision());
        }
    }

    public void OnCollide(KinematicCollision2D collision)
    {
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
}
