using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

public partial class Player : Character
{
    #region Weapons

    // The default / starter weapons that the player always spawns with. May be empty.
    // The first template in the array will be the weapon initially equipped to the player's weapon ring.
    [Export]
    public Godot.Collections.Array<PackedScene> StarterWeaponTemplates;

    [Export]
    public float DashDuration = 0.25f;

    [Export]
    public float DashSpeed = 1000f;

    [Export]
    public float DashCoolDown = 1f;

    // Cached weapon ring reference from the player.tscn.
    public WeaponRing WeaponRing { get; private set; }

    // Runtime list of weapon instances the player owns.
    public Godot.Collections.Array<Weapon> Weapons { get; set; }

    protected Weapon EquippedWeapon { get { return WeaponRing.EquippedWeapon; } }
    protected Weapon NextWeapon {
        get
        {
            var idx = Weapons.IndexOf(EquippedWeapon) + 1;
            if (idx >= Weapons.Count)
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
            if (idx < 0)
            {
                idx = Weapons.Count - 1;
            }
            return Weapons[idx];
        }
    }

    #endregion Weapons

    #region Throwables
    // A specialized weapon type for throwing projectiles at a target / target location.
    protected Thrower Thrower { get; private set; }

    // Scene description for the Thrower that the player should use.
    [Export]
    protected PackedScene ThrowerTemplate { get; private set; }

    // TODO: Throwables inventory data structure. We should store a stack count for each template (# held)
    //       PackedScene is so vague, maybe think about defining a `Throwable` Resource type that captures stuff in a descriptive way.

    #endregion

    // Whether the player is using a gamepad (when false, keyboard+mouse is assumed).
    public bool bUsingGamepad { get; private set; } = false;

    private bool isDashing = false;
    private Vector2 dashDirection;
    private Timer dashTimer;
    private Timer dashGhostTimer;
    double lastDashTime;
    private HashSet<Sprite2D> ghostSprites;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();

        AddToGroup("Player", true);

        WeaponRing = GetNode<WeaponRing>("WeaponRing");
        Weapons = new Godot.Collections.Array<Weapon>();

        foreach (var starterWeaponTemplate in StarterWeaponTemplates)
        {
            var starterWeapon = starterWeaponTemplate.Instantiate<Weapon>();
            Weapons.Add(starterWeapon);
            if (WeaponRing.EquippedWeapon == null)
            {
                WeaponRing.Equip(starterWeapon);
            }
        }

        dashTimer = new Timer();
        dashTimer.OneShot = true;
        dashTimer.Timeout += () =>
        {
            isDashing = false;
            lastDashTime = Time.GetTicksUsec() / 1000000.0;
            dashGhostTimer.Stop();
        };
        AddChild(dashTimer);

        dashGhostTimer = new Timer();
        dashGhostTimer.Timeout += SpawnDashGhost;
        AddChild(dashGhostTimer);
        lastDashTime = Time.GetTicksUsec() / 1000000.0 - DashCoolDown;

        // Note: This remains unequipped until we're about to use it.
        if (ThrowerTemplate != null) {
            Thrower = ThrowerTemplate.Instantiate<Thrower>();
        }

        ghostSprites = new HashSet<Sprite2D>();
    }       

    // Called every tick of the physics thread.
    public override void _PhysicsProcess(double delta)
    {
        HandleMovement(delta);
    }

    // Called every rendered frame.
    public override void _Process(double delta)
    {
        base._Process(delta);
        HandleAim(delta);
        SetShaderScreenUV();
    }

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            // KB+M
            case InputEventKey:
            case InputEventMouse:
                if (@event is InputEventMouseMotion) {
                    // High-DPI mice (and maybe others) push zero-velocity events periodically and it interferes with gamepad use.
                    var motionEvent = @event as InputEventMouseMotion;
                    if (motionEvent.Velocity.IsZeroApprox()) {
                        break;
                    }
                }
                bUsingGamepad = false;
                //Input.MouseMode = Input.MouseModeEnum.Confined;
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
        Vector2 movement = dashDirection;
        float speed = DashSpeed;
        if (!isDashing) {
            movement = Input.GetVector("player_move_left", "player_move_right", "player_move_up", "player_move_down");
            speed = MovementSpeed;
        }

        Velocity = movement * speed * (float)delta;

        var collision = MoveAndCollide(Velocity);
        if (collision != null)
        {
            HandleCollision(collision);
        }
    }

    private void HandleAim(double delta)
    {
        if (bUsingGamepad)
        {
            Vector2 aim = Input.GetVector("player_aim_left", "player_aim_right", "player_aim_up", "player_aim_down");
            if (aim.IsZeroApprox())
            {
                // Just use the velocity instead when no aiming is provided.
                // TODO: Lerp?
                aim = Velocity;
            }
            WeaponRing.AimAngle = aim.Angle();
        }
        else
        {
            float mouseAim = (GetGlobalMousePosition() - WeaponRing.GlobalPosition).Angle();
            WeaponRing.AimAngle = mouseAim;
        }
    }

    private void HandleCollision(KinematicCollision2D collision)
    {
        // For now we just default to sliding along surfaces the player collides with.
        Velocity = Velocity.Slide(collision.GetNormal());
    }

    //"For gameplay input, Node._unhandled_input() is generally a better fit, because it allows the GUI to intercept the events"
    //https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html
    public override void _UnhandledInput(InputEvent @event) {
        if (@event.IsActionPressed("shoot") && !@event.IsEcho()) {
            WeaponRing.EquippedWeapon?.PressFire();
        } else if (@event.IsActionReleased("shoot")) {
            WeaponRing.EquippedWeapon?.ReleaseFire();
        }

        if (@event.IsActionPressed("throw_grenade") && !@event.IsEcho()) {
            WeaponRing.Equip(Thrower);
            WeaponRing.EquippedWeapon?.PressFire();
        } else if (@event.IsActionReleased("throw_grenade")) {
            if (WeaponRing.EquippedWeapon == Thrower) {
                WeaponRing.EquippedWeapon?.ReleaseFire();
                WeaponRing.Equip(WeaponRing.LastEquippedWeapon);
            } else {
                GD.PushError($"Thrower should be equipped but was: {WeaponRing.EquippedWeapon}");
            }
        }

        if (@event.IsActionPressed("cycle_weapon_forward") && !@event.IsEcho()) {
            WeaponRing.Equip(NextWeapon);
        } else if (@event.IsActionPressed("cycle_weapon_back") && !@event.IsEcho()) {
            WeaponRing.Equip(PreviousWeapon);
        }

        if (@event.IsActionPressed("player_confirm") && !@event.IsEcho()) {
            InteractWithNearestObject();
        }

        if (@event.IsActionPressed("dash") && !@event.IsEcho()) {
            Dash();
        }
    }

    private void Dash()
    {
        double now = Time.GetTicksUsec() / 1000000.0;
        if (!isDashing && now - lastDashTime >= DashCoolDown) {
            dashDirection = Input.GetVector("player_move_left", "player_move_right", "player_move_up", "player_move_down");
            if (dashDirection.IsZeroApprox()) {
                dashDirection = Vector2.Right.Rotated(WeaponRing.AimAngle);
            }
            isDashing = true;
            dashTimer.Start(DashDuration);

            SpawnDashGhost();
            dashGhostTimer.Start(2f / 60f); //spawn every 2 frames
        }
    }

    private void SpawnDashGhost() {
        Sprite2D ghostSprite = GetSpriteCopy();
        ghostSprite.ZIndex = ZIndex;

        var tween = CreateTween();
        tween.TweenProperty(ghostSprite, "modulate:a", 0.0, 0.5);
        tween.Finished += () => {
            ghostSprites.Remove(ghostSprite);
            ghostSprite.QueueFree();
        };

        ghostSprites.Add(ghostSprite);
        GetParent().AddChild(ghostSprite);
        ghostSprite.GlobalPosition = GlobalPosition;
        ghostSprite.GlobalRotation = GlobalRotation;
    }

    public override void ChangeFloor(int targetFloor) {
        base.ChangeFloor(targetFloor);

        //Hide dash ghosts when changing floors
        foreach (var ghostSprite in ghostSprites) {
            ghostSprite.Hide();
        }
    }

    private Sprite2D GetSpriteCopy() {
        var playerSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        var currentTexture = playerSprite.SpriteFrames.GetFrameTexture(playerSprite.Animation, playerSprite.Frame);

        Sprite2D copy = new Sprite2D();
        copy.Texture = currentTexture;
        copy.Scale = playerSprite.Scale;
        copy.LightMask = playerSprite.LightMask;
        copy.UseParentMaterial = false;
        copy.Material = Material;

        return copy;
    }

    private void SetShaderScreenUV() {
        Main.GetActiveCamera().ForceUpdateTransform();

        var currentSprite = GetSpriteCopy();

        var screenTopLeft = GetGlobalTransformWithCanvas() * (currentSprite.GetRect().Position * currentSprite.Scale);
        var screenBottomRight = GetGlobalTransformWithCanvas() * (currentSprite.GetRect().End * currentSprite.Scale);
        var normalizedStart = screenTopLeft / GetViewport().GetVisibleRect().Size;
        var normalizedEnd = screenBottomRight / GetViewport().GetVisibleRect().Size;

        RenderingServer.GlobalShaderParameterSet("player_screen_uv_start", normalizedStart);
        RenderingServer.GlobalShaderParameterSet("player_screen_uv_end", normalizedEnd);

        //TODO: Just update this when the animation texture changes
        var imageTex = ImageTexture.CreateFromImage(currentSprite.Texture.GetImage());
        RenderingServer.GlobalShaderParameterSet("player_texture", imageTex);
    }
}