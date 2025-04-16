using Godot;

// Impact is an FX class that can be spawned into the scene.
[GlobalClass]
public partial class Impact : Node2D
{
    // Determines how the impact orients itself in the 2d world when initialized.
    public enum OrientationRuleType {
        HitResultNormal = 0,
        HitResultNormalInverted = 1,
        // Custom = 2,
    }

    [ExportCategory("Behavior")]
    [Export]
    public OrientationRuleType OrientationRule = Impact.OrientationRuleType.HitResultNormalInverted;

    // How long in seconds to wait before this impact instance frees itself and all spawned resources.
    // TODO: Investigate pooling.
    [Export]
    public float CleanupLifetime = 3.0f;

    // Particle FX that should be started automatically by the impact when it is spawned.
    [ExportCategory("Visuals")]
    [Export]
    public Godot.Collections.Array<GpuParticles2D> ParticleSystems = [];

    // TODO: GroundDecalTemplate (e.g. blood splatter) with a short delay on spawn.

    // TODO: SoundFXTemplate.

    // Configures the Impact based on a HitResult for the event that spawned it.
    public virtual void Initialize(HitResult sourceHit) {
        GlobalPosition = sourceHit.ImpactLocation;
        switch(OrientationRule) {
            case OrientationRuleType.HitResultNormal:
                GlobalRotation = sourceHit.ImpactNormal.Angle();
                break;
            case OrientationRuleType.HitResultNormalInverted:
                GlobalRotation = (sourceHit.ImpactNormal * -1).Angle();
                break;
            default:
                break; // Use whatever rotation is configured elsewhere.
        }
    }

    public override void _Ready() {
        base._Ready();
        foreach(var system in ParticleSystems) {
            system.Emitting = true;
            system.Restart();
        }

        if (CleanupLifetime > 0) {
            var timer = new Timer();
            timer.WaitTime = CleanupLifetime;
            timer.OneShot = true;
            timer.Timeout += QueueFree;
            AddChild(timer);
            timer.Start();
        }
    }
}