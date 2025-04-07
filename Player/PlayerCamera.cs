using ExtensionMethods;
using Godot;
using System;

public partial class PlayerCamera : Camera2D
{
    // How fast the camera should zoom in or out. At 1.0, the camera zooms by a factor of 100% per /second/ of continuous zoom input.
    public float CameraZoomRate = 10.0f;

    // The largest camera zoom factor permitted
    public float CameraZoomMax = 2f;

    // The smallest camera zoom factor permitted
    public float CameraZoomMin = 0.25f;

    public Node2D Target;

    private Vector2 targetZoom;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (CameraZoomRate <= 0)
        {
            CameraZoomRate = 1.0f;
        }

        targetZoom = Zoom;

        //Dont allow the camera to look outside of world bounds
        LimitTop = -(int) this.GetGameWorld().RegionBounds.Y / 2;
        LimitBottom = (int)this.GetGameWorld().RegionBounds.Y / 2;
        LimitRight = (int)this.GetGameWorld().RegionBounds.X / 2;
        LimitLeft = -(int)this.GetGameWorld().RegionBounds.X / 2;
        LimitSmoothed = true;

        PositionSmoothingEnabled = true;
        DragHorizontalEnabled = true;
        DragVerticalEnabled = true;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        HandleZoom(delta);

        if (Target != null && IsInstanceValid(Target))
        {
            this.GlobalPosition = Target.GlobalPosition;
        }
    }

    private void HandleZoom(double delta)
    {
        // Apply a fraction of total zoom based on the frame time.
        float zoomIncrement = CameraZoomRate * (float)delta;
        var zoomIncrementVec = new Vector2(zoomIncrement, zoomIncrement);
        if (Input.IsActionJustPressed("camera_zoom_in") && !Input.IsKeyPressed(Key.Ctrl))
        {
            targetZoom += zoomIncrementVec;
        }
        else if (Input.IsActionJustPressed("camera_zoom_out") && !Input.IsKeyPressed(Key.Ctrl))
        {
            targetZoom -= zoomIncrementVec;
        }

        // Enforce min/max zoom.
        targetZoom = targetZoom.Clamp(Vector2.One * CameraZoomMin, Vector2.One * CameraZoomMax);

        //Smooth zoom in / out with linear interpolation
        Zoom = Zoom.Slerp(targetZoom, CameraZoomRate  * (float) delta);
    }
}
