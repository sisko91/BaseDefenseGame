using ExtensionMethods;
using Godot;
using Godot.Collections;

public partial class Explosion : Area2D, IInstigated, IImpactMaterial
{
    // Instigator property satisfies IInstigated interface.
    public Character Instigator { get; set; }

    // The outer-most distance that the blast will cover, in world units.
    [Export]
    public float MaximumRadius { get; set; } = 100.0f;

    [Export]
    public float InitialRadius { get; set; } = 10.0f;

    // The duration in seconds that the explosion takes to grow from its initial radius to its maximum radius.
    [Export]
    public float ExpansionDuration { get; set; } = 0.5f;

    // How long in seconds before this explosion is removed from the scene and all effects are halted.
    [Export]
    public float CleanupLifetime = 3.0f;

    [ExportCategory("Impact")]

    // The maximum damage to characters that the explosion will do close to its epicenter. As distance from the epicenter is increased, the blast does less damage.
    [Export]
    public float BaseDamage { get; set; } = 50.0f;

    // The minimum damage to characters that the explosion will do at its outer edge.
    [Export]
    public float MinimumDamage { get; set; } = 10.0f;

    // The power to raise the gradient to when calculating exponential falloff for the blast force applied to distant targets. Generally keep this > 0.5 and < 3.
    [Export]
    public float ForceGradientExponent { get; set; } = 2.0f;

    // ImpactMaterialType satisfies IImpactMaterial.
    // Explosions are generally of impact type "Explosion".
    [Export]
    public IImpactMaterial.MaterialType ImpactMaterialType { get; protected set; } = IImpactMaterial.MaterialType.Explosion;

    // DefaultResponseHit satisfies IImpactMaterial.
    public PackedScene DefaultResponseHint { get; protected set; } = null;

    // ImpactResponseTable satisfies IImpactMaterial.
    // Explosions cannot be impacted by other things, so this always returns an empty table.
    public Dictionary<IImpactMaterial.MaterialType, PackedScene> ImpactResponseTable => [];

    // All character bodies detected in the vicinity of the explosion.
    public Godot.Collections.Array<Character> NearbyCharacters { get; private set; }
    // All character bodies already damaged by this explosion.
    public Godot.Collections.Array<Character> DamagedCharacters { get; private set; }

    public double SpawnEpochSeconds { get; private set; }

    // The collision shape for this explosion. Created dynamically when the explosion is spawned into the scene tree.
    public CollisionShape2D Collider { get; private set; }

    // The raycaster node spawned for this explosion, used to check line-of-sight for calculating blast force on targets behind walls and other obstructions.
    protected RayCast2D Raycaster { get; private set; }

    public override void _Ready() {
        base._Ready();

        // Set up to detect bodies in radius.
        NearbyCharacters = new Godot.Collections.Array<Character>();
        DamagedCharacters = new Godot.Collections.Array<Character>();
        BodyEntered += Explosion_BodyEntered;
        BodyExited += Explosion_BodyExited;

        // Spawn the collider.
        var sensorShape = new CircleShape2D();
        sensorShape.Radius = MaximumRadius;
        Collider = new CollisionShape2D();
        Collider.Shape = sensorShape;
        AddChild(Collider);

        // Spawn the raycaster, this is used to check that line-of-sight exists before applying blast damage.
        Raycaster = new RayCast2D();
        Raycaster.CollisionMask = CollisionMask;
        Raycaster.Enabled = false; // We only want to use this on-demand so disable otherwise.
        AddChild(Raycaster);

        // Start explosion.
        SpawnEpochSeconds = Time.GetTicksMsec() / 1000.0;
        if(CleanupLifetime > 0) {
            var timer = new Timer();
            timer.WaitTime = CleanupLifetime;
            timer.OneShot = true;
            timer.Timeout += QueueFree;
            AddChild(timer);
            timer.Start();
        }

        // OneShot particle effects don't seem to emit by default so we have to turn them all on.
        foreach(var node in GetChildren()) {
            if(node is GpuParticles2D particles) {
                particles.Emitting = true;
            }
        }
    }

    private void Explosion_BodyEntered(Node2D body) {
        if (body is Character character) {
            NearbyCharacters.Add(character);
        }
    }

    private void Explosion_BodyExited(Node2D body) {
        if (body is Character character) {
            NearbyCharacters.Remove(character);
        }
    }

    /*public override void _Draw() {
        double timeSeconds = Time.GetTicksMsec() / 1000.0;
        var spawnDeltaRatio = (timeSeconds - SpawnEpochSeconds) / ExpansionDuration;
        var drawRadius = Mathf.Lerp(InitialRadius, MaximumRadius, spawnDeltaRatio);
        //GD.Print($"Elapsed: {timeSeconds - SpawnEpochSeconds}, Duration: {ExpansionDuration}, Ratio: {spawnDeltaRatio}, drawRadius: {drawRadius}");
        DrawCircle(Vector2.Zero, (float)drawRadius, new Color(1, 0, 0, 0.25f));
    }*/

    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);

        // Every physics tick, we re-calculate bodies within the radius. We do this not by detecting physical collisions but by distance-checking bodies and their
        // collision volumes, since it's a lot faster (and simpler than dynamically-growing the collision body).

        double timeSeconds = Time.GetTicksMsec() / 1000.0;
        var spawnDeltaRatio = (timeSeconds - SpawnEpochSeconds) / ExpansionDuration;
        var testRadius = Mathf.Lerp(InitialRadius, MaximumRadius, spawnDeltaRatio);

        // Makes the debug circle show up.
        //QueueRedraw();

        if (spawnDeltaRatio < 1) {
            foreach (var character in NearbyCharacters) {
                // TODO: This goes to the centerpoint of the character, but maybe we should subtract the collision radius?
                var distance = character.GlobalPosition.DistanceTo(GlobalPosition);
                if (distance < testRadius) {
                    if (DamagedCharacters.Contains(character)) {
                        // Don't damage the same character twice.
                        continue;
                    }

                    // Adjust the raycaster to check the current character. TargetPosition is *relative* so we need to calculate the offset.
                    Raycaster.TargetPosition = (character.GlobalPosition - Raycaster.GlobalPosition);
                    Raycaster.ForceRaycastUpdate();
                    // If we hit something, it will block damage to the character (unless what we hit was another soft target like another character).
                    if (Raycaster.GetCollider() != null) {
                        //this.DrawDebugLine(GlobalPosition, Raycaster.GetCollisionPoint(), new Color(1, 1, 0));
                        //GD.Print($"Hit {((Node)Raycaster.GetCollider()).Name}");
                        if (Raycaster.GetCollider() is not Character) {
                            continue; // block damage to tested character
                        }
                    }

                    HitResult hr = new HitResult();
                    hr.ImpactLocation = character.GlobalPosition;
                    hr.ImpactNormal = (character.GlobalPosition - GlobalPosition).Normalized();
                    // TODO: Add a knockback force for the explosion if we decide the damage is too much to use for it.
                    hr.KnockbackForce = CalculateBlastStrength(distance, BaseDamage, MinimumDamage);
                    character.TryRegisterImpact(hr, this, CalculateBlastStrength(distance, BaseDamage, MinimumDamage));
                    DamagedCharacters.Add(character);
                }
            }
        }
    }

    protected float CalculateBlastStrength(float distance, float basePower, float minimumPower) {
        // Never divide by 0.
        distance = Mathf.Max(distance, InitialRadius + Mathf.Epsilon);

        // Note: I tried a bunch of equations for this, and all have different tradeoffs.

        // Inverse-Square Law: Too aggressive with the falloff. This works well for point effects like light propagation or gravity.

        // Quadratic Law: Better than Inverse-Square but still somewhat aggressive.

        // Linear: Very unnatural. Nothing scales down linearly.

        // Exponential Falloff: Nice and smooth, and we can control the falloff factor to get different gradients of damage (while still controlling min/max damage dealt).

        float alpha = Mathf.Pow(distance / MaximumRadius, ForceGradientExponent);
        float finalDamage = Mathf.Lerp(basePower, minimumPower, alpha);

        // Avoid negative force.
        return Mathf.Max(finalDamage, 0);
    }
}
