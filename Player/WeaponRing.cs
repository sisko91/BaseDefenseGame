using Godot;
using System;

// WeaponRing owns the weapons available to the player and serves as the root for the equipped weapon's scene attachment. Conceptually this is the player's hand holding/aiming the gun, as well their equipment belt.
public partial class WeaponRing : Node2D
{
	// TODO: Add Weapons.

	// The distance away from the center of the player that the equipped weapon will orbit as the aim is adjusted.
	[Export]
	public float AttachmentRadius = 50.0f;

	// Cached reference to the currently equipped weapon.
	private Weapon equippedWeapon;

    // The current angle (radians) that the equipped weapon is being aimed in.
    // Aim angle for the weapon ring is wherever the ring's Node2D location is relative to its parent. If for some reason the parent doesn't have a position then we assume the rotation of the ring directs the aim.
    public float AimAngle { 
		get
		{
			return _aimAngle;
		}
		set
		{
            if (GetParent() is Node2D parent2D)
            {
				Position = Vector2.FromAngle(value)*AttachmentRadius;
            }
			else
			{
				Rotation = value;
            }
			_aimAngle = value;
        }
	}
	private float _aimAngle;

	public void Equip(Weapon weapon)
	{
		if(equippedWeapon != null)
		{
			RemoveChild(equippedWeapon);
		}

		equippedWeapon = weapon;
		AddChild(weapon);
	}
    
	// Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Update the rotation of the equipped weapon so that it stays oriented parallel to the aim vector.
		if(equippedWeapon != null)
		{
            equippedWeapon.Rotation = _aimAngle;
        }
    }
}
