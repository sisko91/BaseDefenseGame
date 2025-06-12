using Godot;
using System;

public partial class ChatBubble : Control
{
    // The node that this ChatBubble stays close to. Exact positioning is subject to the positioning rules below.
    [Export] public Node2D AnchorNode = null;

    protected RichTextLabel RichTextLabel = null;

    public string Text
    {
        get => RichTextLabel.Text;
        set => RichTextLabel.Text = value;
    }

    public override void _Ready()
    {
        RichTextLabel = GetNode<RichTextLabel>("%RichText");
        VisibilityChanged += OnVisibilityChange;
        SyncAnchorPosition();
    }

    protected virtual void OnVisibilityChange()
    {
        if (Visible)
        {
            SyncAnchorPosition();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Visible || !IsInstanceValid(AnchorNode))
        {
            return;
        }

        // We call the sync logic after the current physics frame ends so that all transforms have settled, without this
        // there's a noticeable jitter when the dialog box is moving each frame.
        Callable.From(SyncAnchorPosition).CallDeferred();
    }

    private void SyncAnchorPosition()
    {
        // Convert anchor node's world position to screen-space
        var camera = GetViewport().GetCamera2D();
        Vector2 screenSize = GetViewport().GetVisibleRect().Size;
        Vector2 offset = (AnchorNode.GlobalPosition - camera.GetScreenCenterPosition())*camera.Zoom;
        Vector2 screenPos = offset + screenSize * 0.5f;

        // Optional: Offset above the anchor (e.g. 40 pixels upward in screen space)
        //Vector2 offset = new Vector2(0, -40);
        //screenPos += offset;

        // Set chat bubble's screen position
        Position = screenPos;
    }
}
