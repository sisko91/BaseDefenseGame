using Godot;

// Impact is an FX class that can be spawned into the scene.
[GlobalClass]
public partial class Impact : Node2D
{
    // How long in seconds to wait before this impact instance frees itself and all spawned resources.
    // TODO: Investigate pooling.
    [Export]
    public float CleanupLifetime = 3.0f;

    [ExportCategory("Visuals")]
    [Export]
    public GpuParticles2D ParticleFX = null;

    // TODO: GroundDecalTemplate (e.g. blood splatter) with a short delay on spawn.
    
    // TODO: SoundFXTemplate.

    public override void _Ready() {
        base._Ready();
        if(ParticleFX != null) {
            ParticleFX.Emitting = true;
            ParticleFX.Restart();
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
