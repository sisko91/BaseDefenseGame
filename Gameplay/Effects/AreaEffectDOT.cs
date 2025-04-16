using Godot;

// AreaEffectDOT is a damage-over-time effect which applies to bodies within the defined area.
public partial class AreaEffectDOT : AreaEffect, IImpactMaterial
{
    [ExportCategory("Impact")]
    // ImpactMaterialType satisfies IImpactMaterial.
    [Export]
    public IImpactMaterial.ImpactType ImpactSourceType { get; protected set; } = IImpactMaterial.ImpactType.Default;

    [Export]
    // DefaultResponseHit satisfies IImpactMaterial.
    public PackedScene DefaultResponseHint { get; protected set; } = null;

    // ImpactResponseTable satisfies IImpactMaterial.
    // Instanced effects like AreaEffect cannot be impacted by other things, so this always returns an empty table.
    public Godot.Collections.Dictionary<IImpactMaterial.ImpactType, PackedScene> ImpactResponseTable => [];

    // How often, in seconds, between consecutive damage ticks applied to characters within the cone.
    [Export]
    public float DamageTickPeriod { get; protected set; } = 0.5f;

    [Export]
    public float DamagePerTick { get; protected set; } = 5.0f;

    [Export]
    public float KnockbackPerTick { get; protected set; } = 0;

    private double lastTickTimeSeconds = 0;

    public override void _Ready() {
        base._Ready();

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

        var timeNow = GetTimeSeconds();
        if(lastTickTimeSeconds == 0 || timeNow - lastTickTimeSeconds > DamageTickPeriod) {
            TickDamage();
            lastTickTimeSeconds = timeNow;
        }
    }

    protected void TickDamage() {
        foreach(var character in NearbyCharacters) {
            HitResult hr = new HitResult();
            hr.ImpactLocation = character.GlobalPosition;
            hr.ImpactNormal = (character.GlobalPosition - GlobalPosition).Normalized();
            hr.KnockbackForce = KnockbackPerTick;
            this.TryRegisterImpact(character, hr, DamagePerTick);
        }
    }
}
