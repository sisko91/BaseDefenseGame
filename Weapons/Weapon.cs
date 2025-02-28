using Godot;
using System;

public partial class Weapon : Node2D
{
    public delegate void ProjectileHandler(Node2D projectile);
	public static ProjectileHandler ProjectileSpawner;

	// Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public virtual void Shoot() { }
}
