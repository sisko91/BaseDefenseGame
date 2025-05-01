using Godot;

// Singleton Node containing debug settings and configuration. Autoloaded by the game on startup (See Project -> Project Settings -> Globals -> Autoload)
public partial class DebugConfig : Node {
    public static DebugConfig Instance { get; private set; }

    #region DRAW_STEERING
    private bool _drawSteering = false;
    public bool DRAW_STEERING {
        get => _drawSteering;
        set => SetDrawSteering(value);
    }
    [Signal]
    public delegate void DrawSteeringChangedEventHandler();
    public void SetDrawSteering(bool bSet) {
        _drawSteering = bSet;
        EmitSignal(SignalName.DrawSteeringChanged);
    }
    #endregion

    #region DRAW_NAVIGATION
    private bool _drawNavigation = false;
    public bool DRAW_NAVIGATION {
        get => _drawNavigation;
        set => SetDrawNavigation(value);
    }
    [Signal]
    public delegate void DrawNavigationChangedEventHandler();
    public void SetDrawNavigation(bool bSet) {
        _drawNavigation = bSet;
        EmitSignal(SignalName.DrawNavigationChanged);
    }
    #endregion

    #region DRAW_COLLISION_BODIES
    private bool _drawCollisionBodies = false;
    public bool DRAW_COLLISION_BODY_RADIUS {
        get => _drawCollisionBodies;
        set => SetDrawCollisionBodies(value);
    }
    [Signal]
    public delegate void DrawCollisionBodiesChangedEventHandler();
    public void SetDrawCollisionBodies(bool bSet) {
        _drawCollisionBodies = bSet;
        EmitSignal(SignalName.DrawCollisionBodiesChanged);
    }
    #endregion

    #region DRAW_COLLISION_BOUNDING_BOX
    private bool _drawCollisionBoundingBox = false;
    public bool DRAW_COLLISION_BOUNDING_BOX  {
        get => _drawCollisionBoundingBox;
        set => SetDrawCollisionBoundingBox(value);
    }
    [Signal]
    public delegate void DrawCollisionBoundingBoxChangedEventHandler();
    public void SetDrawCollisionBoundingBox(bool bSet) {
        _drawCollisionBoundingBox = bSet;
        EmitSignal(SignalName.DrawCollisionBoundingBoxChanged);
    }
    #endregion

    public override void _Ready() {
        base._Ready();

        Instance = this;
    }
}
