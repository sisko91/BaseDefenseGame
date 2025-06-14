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

        //Restrict zooming out past the world bounds
        Vector2 viewportSize = GetViewportRect().Size;
        var absoluteCameraZoomMin = viewportSize / this.GetGameWorld().RegionBounds;
        CameraZoomMin = Math.Max(CameraZoomMin, Math.Max(absoluteCameraZoomMin.X, absoluteCameraZoomMin.Y));
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        HandleZoom(delta);

        if (Target != null && IsInstanceValid(Target))
        {
            this.GlobalPosition = Target.GlobalPosition.Round();
        }
        
        // Make sure the screen world rect is available for shaders.
        // Note: we have to calculate the screen points manually because GetViewportRect().Position will lie to us. It
        //       does not consider LimitTop/LimitBottom/etc.
        Vector2 screenSize = GetViewportRect().Size;
        Vector2 screenWorldTopLeft = GetScreenCenterPosition() - screenSize * 0.5f / Zoom;
        Vector2 screenWorldSize = screenSize / Zoom;
        RenderingServer.GlobalShaderParameterSet("screen_world_rect", new Rect2(screenWorldTopLeft, screenWorldSize));
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
        Zoom = Zoom.Lerp(targetZoom, CameraZoomRate  * (float) delta);
    }
}
