using ExtensionMethods;
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

    [Export]
    public Brain Brain { get; protected set; }

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

        if(Brain != null)
        {
            Brain.Initialize(this);
        }

        RotationGoal = GlobalRotation;
    }

    private async void SetupNavAgent()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        NavAgent = new NavigationAgent2D();
        NavAgent.DebugEnabled = DebugConfig.DRAW_NAVIGATION;
        NavAgent.PathDesiredDistance = NavigationConfig.PATH_DESIRED_DISTANCE;
        NavAgent.TargetDesiredDistance = NavigationConfig.DEFAULT_TARGET_DESIRED_DISTANCE; // Updated by the AI depending on what the target is.
        NavAgent.Radius = GetCollisionBodyRadius();

        //Default to the first floor nav map. NPCs spawned in upstairs regions should automatically switch to the right map on game load
        NavAgent.SetNavigationMap(this.GetGameWorld().NavMaps[0]);
        NavAgent.TargetPosition = GlobalPosition;
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

    public override void ChangeFloor(int targetFloor) {
        //Handle edge case where mob spawns on stairs at game load, but maps are not initialized yet
        if (CurrentElevationLevel >= this.GetGameWorld().NavMaps.Count) {
            return;
        }

        base.ChangeFloor(targetFloor);
        NavAgent.SetNavigationMap(this.GetGameWorld().NavMaps[CurrentElevationLevel]);
    }
}
