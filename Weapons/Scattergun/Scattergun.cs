using ExtensionMethods;
using Godot;
using System;

public partial class Scattergun : Weapon
{
    [Export]
    public PackedScene RoundTemplate = null;

    [Export]
    public uint RoundsPerFire = 4;

    // Maximum angle - in degrees - that any round within a fired payload may deviate from straight ahead (in either direction).
    [Export]
    public float MaxRoundSpreadDegrees = 20.0f;

    // How long the gun must wait between consecutive volleys.
    [Export]
    public float FireCooldownSeconds = 1.4f;

    private double lastFireTime = -1;
    private bool bFiring = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if(bFiring)
        {
            TryFire();
        }
    }

    protected void TryFire()
    {
        var timeSeconds = Time.GetTicksUsec() / 1000000.0;
        if (timeSeconds - FireCooldownSeconds < lastFireTime)
        {
            // Premature
            return;
        }

        lastFireTime = timeSeconds;

        var roundSpreadRads = Mathf.DegToRad(Mathf.Abs(MaxRoundSpreadDegrees));
        for (int i = 0; i < RoundsPerFire; i++)
        {
            var round = RoundTemplate.Instantiate<Projectile>();
            var randNegativeOneToOne = (GD.Randf() - 0.5f) * 2;
            // Less randomness for the first rounds in the payload. This just makes the cone tighter toward the center with falloff.
            float randInfluence = (float)(RoundsPerFire - i) / (float)RoundsPerFire;
            float rotationOffset = roundSpreadRads * randNegativeOneToOne * randInfluence;
            round.Start(GlobalPosition, GlobalRotation + rotationOffset);
        }
    }

    public override void PressFire()
    {
        bFiring = true;
        TryFire();
    }

    public override void ReleaseFire()
    {
        bFiring = false;
    }
}
