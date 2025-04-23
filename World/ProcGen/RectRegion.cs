using Godot;
using System;

// RectRegion is a placeable node containing a rectangle that can be dragged to size in the editor.
[GlobalClass]
[Tool]
public sealed partial class RectRegion : Node2D
{
    private Rect2 _region;

    [Export]
    public Rect2 Region {
        get => _region;
        set {
            _region = value;
            QueueRedraw();
        }
    }

    public override void _Ready() {
        base._Ready();
    }

    public override void _Draw() {
        if(Engine.IsEditorHint()) {
            // Partially-transparent
            var drawColor = new Color(0, 0.5f, 0.5f, 0.5f);
            DrawRect(rect: Region, color: drawColor, filled: true);
        }
    }
}
