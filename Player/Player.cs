using ExtensionMethods;
using Godot;
using System;
using static Godot.TextServer;

public partial class Player : CharacterBody2D
{
    // How fast the player can move in any direction.
    [Export]
    public float MovementSpeed = 400.0f;

    // Cached camera reference from the player.tscn.
    public PlayerCamera Camera { get; private set; }

    #region Weapons

    // The default / starter weapons that the player always spawns with. May be empty.
    // The first template in the array will be the weapon initially equipped to the player's weapon ring.
    [Export]
    public Godot.Collections.Array<PackedScene> StarterWeaponTemplates;

    // Cached weapon ring reference from the player.tscn.
    public WeaponRing WeaponRing { get; private set; }

    // Runtime list of weapon instances the player owns.
    public Godot.Collections.Array<Weapon> Weapons { get; private set; }
    
    protected Weapon EquippedWeapon { get { return WeaponRing.EquippedWeapon; } }
    protected Weapon NextWeapon { 
        get 
        {
            var idx = Weapons.IndexOf(EquippedWeapon) + 1;
            if(idx >= Weapons.Count)
            {
                idx = 0;
            }
            return Weapons[idx];
        } 
    }
    protected Weapon PreviousWeapon
    {
        get
        {
            var idx = Weapons.IndexOf(EquippedWeapon) - 1;
            if(idx < 0)
            {
                idx = Weapons.Count - 1;
            }
            return Weapons[idx];
        }
    }

    #endregion Weapons

    // Whether the player is using a gamepad (when false, keyboard+mouse is assumed).
    public bool bUsingGamepad { get; private set; } = false;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        AddToGroup("Player", true);

        Camera = GetNode<PlayerCamera>("Camera2D");
        WeaponRing = GetNode<WeaponRing>("WeaponRing");

        Weapons = new Godot.Collections.Array<Weapon>();

        foreach(var starterWeaponTemplate in StarterWeaponTemplates)
        {
            var starterWeapon = starterWeaponTemplate.Instantiate<Weapon>();
            Weapons.Add(starterWeapon);
            if(WeaponRing.EquippedWeapon == null)
            {
                WeaponRing.Equip(starterWeapon);
            }
        }
    }

    // Called every tick of the physics thread.
    public override void _PhysicsProcess(double delta)
    {
        HandleMovement(delta);
        HandleAction(delta);
    }

    // Called every rendered frame.
    public override void _Process(double delta)
    {
        HandleAim(delta);
    }

    public override void _Input(InputEvent @event)
    {
        switch(@event)
        {
            // KB+M
            case InputEventKey:
            case InputEventMouse:
                bUsingGamepad = false;
                break;
            case InputEventJoypadButton:
            case InputEventJoypadMotion:
                bUsingGamepad = true;
                break;
            default:
                break;
        }
    }

    private void HandleMovement(double delta)
    {
        // Below is based loosely on: https://docs.godotengine.org/en/stable/tutorials/physics/using_character_body_2d.html#platformer-movement
        Vector2 movement = Input.GetVector("player_move_left", "player_move_right", "player_move_up", "player_move_down");
        
        Velocity = movement * MovementSpeed * (float)delta;

        var collision = MoveAndCollide(Velocity);
        if (collision != null)
        {
            HandleCollision(collision);
        }
    }

    private void HandleAim(double delta)
    {
        if(bUsingGamepad)
        {
            Vector2 aim = Input.GetVector("player_aim_left", "player_aim_right", "player_aim_up", "player_aim_down");
            if(aim.IsZeroApprox())
            {
                // Just use the velocity instead when no aiming is provided.
                // TODO: Lerp?
                aim = Velocity;
            }
            WeaponRing.AimAngle = aim.Angle();
        }
        else
        {
            float mouseAim = (GetGlobalMousePosition() - GlobalPosition).Angle();
            WeaponRing.AimAngle = mouseAim;
        }
    }

    private void HandleCollision(KinematicCollision2D collision)
    {
        // For now we just default to sliding along surfaces the player collides with.
        Velocity = Velocity.Slide(collision.GetNormal());
    }

    private void HandleAction(double delta) {
        if (Input.IsActionJustPressed("shoot")) {
            WeaponRing.EquippedWeapon?.PressFire();
        }
        else if(Input.IsActionJustReleased("shoot"))
        {
            WeaponRing.EquippedWeapon?.ReleaseFire();
        }

        if (Input.IsActionJustPressed("throw_grenade"))
        {
            //TODO: Make Throwables container
            var grenadeScene = GD.Load<PackedScene>("res://Weapons/FragGrenade/FragGrenade.tscn");
            var grenade = grenadeScene.Instantiate<FragGrenade>();

            //Spawn a bit in front of the player aim dir
            var rot = (GetGlobalMousePosition() - GlobalPosition).Angle();
            var dir = new Vector2((float)Math.Cos(rot), (float)Math.Sin(rot)).Normalized();
            var pos = GlobalPosition + dir * new Vector2(100, 100);
            grenade.Start(pos, rot);
            var timer = GetTree().CreateTimer(grenade.LifetimeSeconds);
            timer.Timeout += grenade.Explode;

            this.GetGameWorld().AddChild(grenade);
        }

        if(Input.IsActionJustPressed("cycle_weapon_forward"))
        {
            WeaponRing.Equip(NextWeapon);
        }
        else if (Input.IsActionJustPressed("cycle_weapon_back"))
        {
            WeaponRing.Equip(PreviousWeapon);
        }
    }
}
