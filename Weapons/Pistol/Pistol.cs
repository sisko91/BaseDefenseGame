using ExtensionMethods;
using Godot;

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

    protected override Node2D DoShoot() {
        var bulletScene = GD.Load<PackedScene>("res://Weapons/Pistol/bullet.tscn");
        var bullet = bulletScene.Instantiate<Bullet>();

        bullet.Start(GlobalPosition, GlobalRotation);
        var timer = GetTree().CreateTimer(bullet.LifetimeSeconds);
        timer.Timeout += bullet.QueueFree;

        return bullet;
    }
}
