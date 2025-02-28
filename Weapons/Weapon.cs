using ExtensionMethods;
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

    public void Shoot() {
        var newProj = DoShoot();
        if (newProj != null)
        {
            // Register the new projectile with the world, and notify any other listeners.
            this.GetGameWorld().AddChild(newProj);
            ProjectileSpawner?.Invoke(newProj);
        }
    }

    protected virtual Node2D DoShoot()
    {
        return null;
    }
}
