using Godot;
using System;

// ProjectileSpray is a directed effect which sprays projectiles in an angle for a duration.
public partial class ProjectileSpray : Node2D, IInstigated, IImpactMaterial, IEntity
{
    // Instigator property satisfies IInstigated interface.
    public Character Instigator { get; set; }

    //Satisfy the IEntity interface
    public BuildingRegion CurrentRegion { get; set; }

    // ImpactMaterialType satisfies IImpactMaterial.
    // ProjectileSpray is generally of impact type "Bullet" but may be different depending on what the projectiles are.
    [Export]
    public IImpactMaterial.MaterialType ImpactMaterialType { get; protected set; } = IImpactMaterial.MaterialType.Bullet;

    // DefaultResponseHit satisfies IImpactMaterial.
    public PackedScene DefaultResponseHint { get; protected set; } = null;

    // ImpactResponseTable satisfies IImpactMaterial.
    // Instanced effects like ProjectileSpray cannot be impacted by other things, so this always returns an empty table.
    public Godot.Collections.Dictionary<IImpactMaterial.MaterialType, PackedScene> ImpactResponseTable => [];

    // How long in seconds before this spray is removed from the scene and all effects are halted.
    [Export]
    public float CleanupLifetime = 3.0f;

    public double SpawnEpochSeconds { get; private set; }

    [ExportCategory("Spray")]
    [Export]
    public PackedScene ProjectileTemplate { get; protected set; } = null;

    // How far the projectiles can spray before they will disappate / hit the ground / etc.
    [Export]
    public float MaxDistance { get; protected set; } = 150.0f;

    public override void _Ready() {
        base._Ready();

        // Start spray.
        SpawnEpochSeconds = Time.GetTicksMsec() / 1000.0;
        if (CleanupLifetime > 0) {
            var timer = new Timer();
            timer.WaitTime = CleanupLifetime;
            timer.OneShot = true;
            timer.Timeout += QueueFree;
            AddChild(timer);
            timer.Start();
        }

        // OneShot particle effects don't seem to emit by default so we have to turn them all on.
        foreach (var node in GetChildren()) {
            if (node is GpuParticles2D particles) {
                particles.Emitting = true;
            }
        }

        CurrentRegion = null;
    }

    public override void _Process(double delta) {
        base._Process(delta);
    }
}
