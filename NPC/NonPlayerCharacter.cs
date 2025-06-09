using System.Linq;
using ExtensionMethods;
using Godot;
using static Godot.HttpClient;

public partial class NonPlayerCharacter : Character, IWorldLifecycleListener
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

        if (Brain == null)
        {
            Brain = this.GetChildrenOfType<Brain>().FirstOrDefault();
            //GD.Print($"NPC {GetPath()}: using discovered Brain: {Brain?.GetPath()}");
        }

        NearbyBodySensor.PlayerSensed += OnPlayerSensed;
        NearbyBodySensor.NpcSensed += OnNpcSensed;

        RotationGoal = GlobalRotation;
    }
    
    // Satisfies IWorldLifecycleListener; Used to initialize NavAgent so that it happens after the world is
    // fully initialized.
    public void PostWorldInit(World gameWorld)
    {
        SetupNavAgent();
    }

    private void SetupNavAgent()
    {
        NavAgent = new NavigationAgent2D();
        NavAgent.DebugEnabled = DebugConfig.Instance.DRAW_NAVIGATION;
        // Update the NavAgent any time the debug config changes.
        DebugConfig.Instance.DrawNavigationChanged += RefreshConfig;

        NavAgent.PathDesiredDistance = NavigationConfig.PATH_DESIRED_DISTANCE;
        NavAgent.TargetDesiredDistance = NavigationConfig.DEFAULT_TARGET_DESIRED_DISTANCE; // Updated by the AI depending on what the target is.

        //Default to the current elevation nav map. NPCs spawned in upstairs regions should automatically switch to the right map on game load
        NavAgent.SetNavigationMap(this.GetGameWorld().NavMaps[CurrentElevationLevel]);
        NavAgent.TargetPosition = GlobalPosition;
        AddChild(NavAgent);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        // Don't do anything until the NavAgent is fully initialized.
        if (NavAgent == null)
        {
            return;
        }
        
        if (Brain != null)
        {
            Brain.Think(delta);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        // Don't do anything until the NavAgent is fully initialized.
        if (NavAgent == null)
        {
            return;
        }
        
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

        Vector2 velocity;
        if (Falling && AffectedByGravity) {
            velocity = HandleFalling(delta);
        } else {
            velocity = Velocity;
        }

        var collision = MoveAndCollide(velocity * (float)delta);
        if (collision != null) {
            HandleCollision(collision);
        }
    }

    public void HandleCollision(KinematicCollision2D collision)
    {
        if (collision.GetCollider() is Character character) {
            Callable.From(() => {
                //Make knocked back npcs bounce off other npcs
                if (!Knockback.IsZeroApprox()) {
                    Velocity = Velocity.Bounce(collision.GetNormal());
                    Knockback = Knockback.Length() * Velocity.Normalized();
                }
            }).CallDeferred();
        }
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
        base.ChangeFloor(targetFloor);

        // This may be called early before NavAgent is set up. It will get corrected if so when SetupNavAgent() runs.
        if (NavAgent != null)
        {
            NavAgent.SetNavigationMap(this.GetGameWorld().NavMaps[CurrentElevationLevel]);
        }
    }

    public void RefreshConfig() {
        if (!IsInstanceValid(this)) {
            DebugConfig.Instance.DrawNavigationChanged -= RefreshConfig;
            return;
        }

        NavAgent.DebugEnabled = DebugConfig.Instance.DRAW_NAVIGATION;
    }
}
