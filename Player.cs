using Godot;
using System;

public partial class Player : CharacterBody2D
{
    // How fast the player can move in any direction.
    [Export]
    public float MovementSpeed = 400.0f;

    // Cached camera reference from the player.tscn.
    public PlayerCamera Camera { get; private set; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Camera = GetNode<PlayerCamera>("Camera2D");
    }

    // Called every tick of the physics thread.
    public override void _PhysicsProcess(double delta)
    {
        HandleMovement(delta);
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

    private void HandleCollision(KinematicCollision2D collision)
    {
        // For now we just default to sliding along surfaces the player collides with.
        Velocity = Velocity.Slide(collision.GetNormal());
    }
}
