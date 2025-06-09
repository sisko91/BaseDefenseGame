using Godot;
using System;

// WeaponRing serves as the attachment root for the equipped weapon's scene. Conceptually this is the player's hand holding/aiming the gun.
public partial class WeaponRing : Node2D
{
	// The distance away from the center of the player that the equipped weapon will orbit as the aim is adjusted.
	[Export]
	public float AttachmentRadius = 25.0f;

    // The point of attachment that weapons are made children of. This allows the WeaponRing to maintain a static offset (set in the editor by us) and still control
    // the offset of the weapon separately (without manipulating the weapon node any time that it's changed/swapped).
    private Node2D AttachmentPoint = null;

	// Cached reference to the currently equipped weapon.
	public Weapon EquippedWeapon { get; private set; }

	// Returns the last-equipped weapon *before* the one currently equipped. Note that this weapon will most likely no longer be
	// part of the scene unless it was added back as a child elsewhere.
	public Weapon LastEquippedWeapon { get; private set; }

    // The current angle (radians) that the equipped weapon is being aimed in.
    // Aim angle for the weapon ring is wherever the ring's AttachmentPoint location is relative to the ring itself.
    public float AimAngle { 
		get => _aimAngle;
        set
		{
            _aimAngle = value;
            AttachmentPoint.Position = Vector2.FromAngle(_aimAngle)*AttachmentRadius;
            if(EquippedWeapon != null)
            {
                EquippedWeapon.Rotation = _aimAngle;
            }
        }
	}
	private float _aimAngle;

	public void Equip(Weapon weapon)
	{
		LastEquippedWeapon = EquippedWeapon;
		if(EquippedWeapon != null)
		{
			AttachmentPoint.RemoveChild(EquippedWeapon);
		}

		EquippedWeapon = weapon;
        AttachmentPoint.AddChild(weapon);
	}
    
	// Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        AttachmentPoint = new Node2D();
        AttachmentPoint.Name = "AttachmentPoint";
        AddChild(AttachmentPoint);
    }
}
