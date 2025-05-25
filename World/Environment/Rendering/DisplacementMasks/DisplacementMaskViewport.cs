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
    // The clear color for the viewport texture each frame.
    [Export] public Color ClearColor = Colors.Black;

    // The optional FrameBuffer to use. If not null, the contents of this buffer will be rendered before any
    // displacements this frame. The primary use-case for this is to have a FrameBufferViewport capturing this
    // displacement mask each frame and serving it back into the rendering loop for things like trail FX.
    [Export] public FrameBufferViewport FrameBuffer = null;
    
    [ExportCategory("Perspective")]
    // Controls if the displacement mask is calculated based on screen or global coordinates. A Screen-Space mask covers
    // the current screen and updates each frame. The contents of the mask that moves offscreen is immediately and
    // permanently lost. The benefit is that this is much cheaper and can work well for local effects.
    [Export] protected bool ScreenSpaceMask = false;
    // The main camera in the scene. Mandatory for ScreenSpaceMasks.
    [Export] public Camera2D MainCamera { get; set; }
    public Camera2D DisplacementCamera { get; private set; }
    
    private Node2D markerRoot { get; set; }
    private Sprite2D clearColorSprite = null;
    private Sprite2D frameBufferSprite = null;
    
    public override void _Ready()
    {
        markerRoot = GetNode<Node2D>("MarkerRoot");
        
        // Set up the clear color sprite as a 1x1 pixel image that gets resized to fit the viewport each frame.
        var clearColorTexture = new GradientTexture2D();
        clearColorTexture.Gradient = new Gradient();
        clearColorTexture.Gradient.AddPoint(0, ClearColor);
        clearColorTexture.Gradient.AddPoint(1, ClearColor);
        clearColorTexture.Width = 1;
        clearColorTexture.Height = 1;
        clearColorSprite = new Sprite2D();
        clearColorSprite.Name = "ClearColorSprite";
        clearColorSprite.SelfModulate = ClearColor;
        clearColorSprite.Texture = clearColorTexture;
        markerRoot.AddChild(clearColorSprite);

        if (FrameBuffer != null)
        {
            if (ScreenSpaceMask)
            {
                GD.PushError($"Cannot use DisplacementMaskViewport (ScreenSpace) {Name} with FrameBuffer {FrameBuffer.Name}; Only Global DisplacementMaskViewports can use a FrameBuffer.");
            }
            else
            {
                frameBufferSprite = new Sprite2D();
                frameBufferSprite.Name = "FrameBufferSprite";
                frameBufferSprite.Texture = FrameBuffer.GetTexture();
                // Add the frame buffer right on top of the clear color.
                clearColorSprite.AddSibling(frameBufferSprite);
            }
        }

        DisplacementCamera = GetNode<Camera2D>("DisplacementCamera");
        
        // DisplacementCamera MUST be configured with these options in order to work.
        DisplacementCamera.Enabled = true;
        DisplacementCamera.MakeCurrent();
    }

    public override void _Process(double delta)
    {
        if (DisplacementCamera == null) return;

        if (ScreenSpaceMask)
        {
            // Screen-Space masks require the main camera reference. This exits quietly because setup may not be done.
            if (MainCamera == null)
            {
                return;
            }

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

            if (clearColorSprite != null)
            {
                clearColorSprite.Scale = screenSize * (zoomScale);
            }
        }
        else
        {
            DisplacementCamera.Zoom = Vector2.One;
            DisplacementCamera.Offset = Vector2.Zero;

            var world = this.GetGameWorld();
            var worldSize = world.RegionBounds;
            DisplacementCamera.GlobalPosition = world.GlobalPosition;
            DisplacementCamera.Zoom = GetVisibleRect().Size / worldSize;

            if (clearColorSprite != null)
            {
                clearColorSprite.Scale = worldSize;
            }
            
            if (frameBufferSprite != null)
            {
                frameBufferSprite.Scale = worldSize / FrameBuffer.GetSize();
            }
        }
    }

    // Reparents the specified Marker under this Viewport and ensures it will render each frame.
    public void RegisterMarker(DisplacementMaskMarker marker)
    {
        if (marker.GetParent() != null)
        {
            GD.PushWarning($"Re-parenting registered displacement marker `{marker.Name}` from its original parent ({marker.GetParent().Name}).");
        }
        markerRoot.AddChild(marker);
    }
}
