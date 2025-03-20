using Godot;

public partial class Thrower : Weapon
{
    [Export]
    public PackedScene ThrowableTemplate;

    [Export]
    public float MaxWindUpSeconds = 1.5f;

    [Export]
    public float MaxWindUpThrowDistance = 750.0f;

    // Determines what happens when the Press/Release fire actions occur very close together, i.e. a button click rather than press-and-hold.
    // If CanQuickThrow is true then the throwable will be thrown with QuickThrowStrengthRatio as multiplier rather than measuring the hold duration.
    // TODO: This is probably something we should dictate on the Throwable (i.e. the resource type) rather than the Thrower weapon itself.
    [Export]
    public bool CanQuickThrow = true;

    // The % of MaxWindUpThrowDistance to use if the thrower is engaged with a "pressed" rather than "press and hold".
    [Export]
    public float QuickThrowStrengthRatio = 0.35f;

    public double CurrentWindUpSeconds {
        get {
            return Time.GetTicksMsec() / 1000.0 - lastWindUpStartSeconds;
        }
    }

    // Cached reference to the scene node painted on the ground where the Thrower estimates the projectile to reach.
    public Node2D GroundTarget { get; private set; }

    // When the last wind up started
    protected double lastWindUpStartSeconds = -1;

    public override void _Ready() {
        base._Ready();

        // Ground target is optional. AI throwing things won't want or need one.
        if(HasNode("GroundTarget")) {
            GroundTarget = GetNode<Node2D>("GroundTarget");
            GroundTarget.Visible = false;
        }
    }

    public override void PressFire() {
        lastWindUpStartSeconds = Time.GetTicksMsec() / 1000.0;
    }

    public override void _Process(double delta) {
        base._Process(delta);

        if(GroundTarget == null) {
            // Nothing to do if we aren't using a ground target.
            return;
        }

        double windUpRatio = CurrentWindUpSeconds / MaxWindUpSeconds;
        if(CurrentWindUpSeconds > 0.10) {
            var cappedThrowDistance = (float)Mathf.Min(MaxWindUpThrowDistance, windUpRatio * MaxWindUpThrowDistance);
            GroundTarget.Position = Vector2.FromAngle(GroundTarget.Rotation) * cappedThrowDistance;
            GroundTarget.Visible = true;
        }
        else {
            GroundTarget.Visible = false;
        }
    }

    public override void ReleaseFire() {
        // How far can we throw?
        double windUpRatio = CurrentWindUpSeconds / MaxWindUpSeconds;
        // If the press/release cycle was <100ms then assume the player wants to quick-throw.
        if(CurrentWindUpSeconds < 0.1 && CanQuickThrow) {
            windUpRatio = QuickThrowStrengthRatio;
        }
        var cappedThrowDistance = (float)Mathf.Min(MaxWindUpThrowDistance, windUpRatio * MaxWindUpThrowDistance);

        // Where is that?
        var targetLocation = GlobalPosition + Vector2.FromAngle(GlobalRotation) * cappedThrowDistance;

        // How fast do we need to throw to reach there (in 1 second)?
        var throwable = ThrowableTemplate.Instantiate<Projectile>();
        throwable.InitialSpeed = CalculateRequiredSpeed(targetLocation, 1.0f);

        var instigator = this.FindCharacterAncestor();
        throwable.Start(GlobalPosition, GlobalRotation, instigator);
    }

    private float CalculateRequiredSpeed(Vector2 targetLocation, float timeframeSeconds) {
        return GlobalPosition.DistanceTo(targetLocation) / timeframeSeconds;
    }
}
