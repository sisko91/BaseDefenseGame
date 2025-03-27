using Godot;
using System;

// The barber fires barbs at targets. The barbs do minor damage on impact but stick into the target that they hit. After a short delay stuck barbs will detonate.
public partial class Barber : Weapon
{
    [Export]
    public PackedScene BarbTemplate;

    // How long the gun must wait before firing another barb.
    [Export]
    public float FireCooldownSeconds = 0.25f;

    private double lastFireTime = -1;
    private bool bFiring = false;

    public Barber() {
        // Default barb to fire.
        BarbTemplate = GD.Load<PackedScene>("res://Weapons/Barber/barb_proj.tscn");
    }

    public override void _Ready() {
        base._Ready();
    }

    public override void PressFire() {
        bFiring = true;
        TryFire();
    }

    protected void TryFire() {
        var timeSeconds = Time.GetTicksUsec() / 1000000.0;
        if (timeSeconds - FireCooldownSeconds < lastFireTime) {
            // Premature
            return;
        }

        lastFireTime = timeSeconds;
        var barb = BarbTemplate.Instantiate<Projectile>();
        barb.Start(GlobalPosition, GlobalRotation, Instigator);
    }

    public override void _Process(double delta) {
        if(bFiring) {
            TryFire();
        }
    }

    public override void ReleaseFire() {
        bFiring = false;
    }
}
