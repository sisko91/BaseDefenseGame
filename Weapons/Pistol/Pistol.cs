using ExtensionMethods;
using Godot;

public partial class Pistol : Weapon
{
    public override void Fire()
    {
        base.Fire();

        var bulletScene = GD.Load<PackedScene>("res://Weapons/Pistol/bullet.tscn");
        var bullet = bulletScene.Instantiate<Bullet>();

        bullet.Start(GlobalPosition, GlobalRotation, Instigator);
    }
}
