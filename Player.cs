using Godot;
using System;

public partial class Player : CharacterBody2D
{
    // How fast the camera should zoom in or out. At 1.0, the camera zooms by a factor of 100% per /second/ of continuous zoom input.
    [Export]
    public float CameraZoomRate = 10.0f;

    // The largest camera zoom factor permitted. At 2.0, the camera is zoomed in 2x (from default).
    [Export]
    public float CameraZoomMax = 2.0f;

    // The smallest camera zoom factor permitted. At 0.5, the camrea is zoomed out 2x (from default).
    [Export]
    public float CameraZoomMin = 0.5f;

    // How fast the player can move in any direction.
    [Export]
    public float MovementSpeed = 400.0f;

    // Cached camera reference from the player.tscn.
    public Camera2D Camera { get; private set; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Camera = GetNode<Camera2D>("Camera2D");

        if(CameraZoomRate <= 0)
        {
            CameraZoomRate = 1.0f;
        }
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

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        HandleZoom(delta);
    }

    private void HandleZoom(double delta)
    {
        // Apply a fraction of total zoom based on the frame time.
        float zoomIncrement = CameraZoomRate * (float)delta;
        var zoomIncrementVec = new Vector2(zoomIncrement, zoomIncrement);
        if (Input.IsActionJustPressed("camera_zoom_in"))
        {
            Camera.Zoom += zoomIncrementVec;
        }
        else if (Input.IsActionJustPressed("camera_zoom_out"))
        {
            Camera.Zoom -= zoomIncrementVec;
        }
        // Enforce min/max zoom.
        Camera.Zoom = Camera.Zoom.Clamp(new Vector2(CameraZoomMin, CameraZoomMin), Vector2.One * CameraZoomMax);
    }
}
