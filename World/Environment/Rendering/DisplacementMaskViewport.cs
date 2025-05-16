using Godot;
using System;
using ExtensionMethods;

// A custom viewport that renders opaque displacement markers to a virtual screen texture. The texture produced by this
// viewport can then be sampled from other shaders to render screen-space displacement effects.
// For now, the only use-case for this is rendering displacements to grass when characters walk through. Other potential
// uses (with necessary enhancements):
// - Wakes when moving through water.
// - Trails through deep snow / mud.
public partial class DisplacementMaskViewport : SubViewport
{
    // The main camera in the scene.
    [Export] public Camera2D MainCamera { get; set; }
    public Camera2D DisplacementCamera { get; private set; }

    // The rect that this viewport is capturing/rendering, in global coordinates.
    public Rect2 WorldRect { get; private set; }
    
    private Node2D markerRoot { get; set; }
    
    public override void _Ready()
    {
        markerRoot = GetNode<Node2D>("MarkerRoot");
        DisplacementCamera = GetNode<Camera2D>("DisplacementCamera");
        
        // DisplacementCamera MUST be configured with these options in order to work.
        DisplacementCamera.Enabled = true;
        DisplacementCamera.MakeCurrent();
    }

    public override void _Process(double delta)
    {
        if (MainCamera == null || DisplacementCamera == null) return;
        
        // Sync the DisplacementCamera with the MainCamera so that both "see" the same thing.
        // There's a few interesting quirks here:
        // First, we can't just assign the same zoom scale to both cameras. For performance reasons, the viewport and
        // texture resolution of the DisplacementCamera is lower than the main game (currently it's 512x288), so we need
        // to make sure that we factor that ratio into the zoom level applied to the DisplacementCamera as well.
        // (We don't adjust DisplacementMaskViewport.Size to do this because it would reallocate a new texture buffer
        // every single frame that the zoom level changed, which is the *opposite* of performance).
        Vector2 screenSize = Main.Instance.GetViewport().GetVisibleRect().Size;
        Vector2 zoomScale = screenSize / GetVisibleRect().Size;
        DisplacementCamera.Zoom = MainCamera.Zoom / zoomScale;
        
        // Second, we can't set the GlobalPositions to be the same either. This is because Camera2D.LimitTop/LimitRight/etc.
        // are not enforced to clamp the node transforms, they are used just before rendering time to clamp the viewports.
        // This is a fancy way of saying that unless the DisplacementCamera is in the same World2D as the MainCamera (it's not)
        // and/or if the DisplacementCamera is currently the only active camera in the scene (it's not), then LimitTop et al
        // will never be enforced.
        //
        // TL;DR: When the MainCamera is clamped based on defined pixel Limits, its GlobalPosition will be a lie and
        // GetScreenCenterPosition() is the final computed location where we want our tracking camera to reposition itself.
        DisplacementCamera.GlobalPosition = MainCamera.GetScreenCenterPosition();
        DisplacementCamera.Offset = MainCamera.Offset; // this is rarely used but might as well sync it, too.
        
        // Calculate & cache the WorldRect for the viewport for this frame so that it's available for other logic.
        Vector2 viewportSize = GetVisibleRect().Size;
        Vector2 camZoom = DisplacementCamera.Zoom;
        
        Vector2 worldSize = viewportSize * camZoom;
        Vector2 worldPos = DisplacementCamera.GlobalPosition - worldSize / 2.0f;
        WorldRect = new Rect2(worldPos, worldSize);
        
        // Update the displacement mask texture.
        RenderingServer.GlobalShaderParameterSet("displacement_mask_tex", GetTexture());
        
        // Make sure the screen world rect is available for shaders.
        // TODO: This could probably live somewhere else and not be tied to displacement.
        Vector2 screenWorldTopLeft = MainCamera.GetScreenCenterPosition() - screenSize * 0.5f / MainCamera.Zoom;
        Vector2 screenWorldSize = screenSize / MainCamera.Zoom;
        RenderingServer.GlobalShaderParameterSet("screen_world_rect", new Rect2(screenWorldTopLeft, screenWorldSize));
    }

    public void RegisterMarkerChild(Node2D displacementMarker)
    {
        if (displacementMarker.GetParent() != null)
        {
            GD.PushWarning($"Re-parenting registered displacement marker `{displacementMarker.Name}` from its original parent ({displacementMarker.GetParent().Name}).");
        }
        markerRoot.AddChild(displacementMarker);
    }
}
