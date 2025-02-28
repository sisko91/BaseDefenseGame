using Godot;
using System;

public partial class Pistol : Weapon
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

    public override void Shoot() {
        var bulletScene = GD.Load<PackedScene>("res://Weapons/Pistol/bullet.tscn");
        var bullet = bulletScene.Instantiate<Bullet>();

        bullet.Start(GlobalPosition, GlobalRotation);
        var timer = GetTree().CreateTimer(bullet.LifetimeSeconds);
        timer.Timeout += bullet.QueueFree;

        ProjectileSpawner?.Invoke(bullet);
    }
}
