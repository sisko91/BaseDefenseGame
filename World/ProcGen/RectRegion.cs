using Godot;
using System;

// RectRegion is a placeable node containing a rectangle that can be dragged to size in the editor.
[GlobalClass]
[Tool]
public sealed partial class RectRegion : Node2D
{
    [Export]
    public Rect2 Region { get; set; }

    [ExportCategory("Editor View")]
    [Export]
    public Color EditorDrawColor { get; private set; } = Colors.BlueViolet;

    [Export]
    public float EditorControlPointSize = 10.0f;

    private bool isDragging = false;
    private Vector2 dragAnchorOffset = Vector2.Zero;

    public override void _Ready() {
        base._Ready();

        if(Engine.IsEditorHint()) {
            // This doesn't enable _Process() in the editor for this node, but it DOES cause _Notification() to fire for that event.
            SetProcessInternal(true);
        }
    }

    public override void _Notification(int what) {
        base._Notification(what);

        if(!Engine.IsEditorHint()) {
            return;
        }

        if (what == NotificationInternalProcess) {
            QueueRedraw();

            bool isMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            if(isMousePressed) {
                if(!isDragging) {
                    Vector2 mousePos = EditorInterface.Singleton.GetEditorViewport2D().GetMousePosition();
                    if (IsMouseOverHandle(mousePos)) {
                        GD.Print("Drag-resize started.");
                        isDragging = true;
                        dragAnchorOffset = Region.End - mousePos;
                        // We don't want to select ANYTHING when we're dragging around, so wipe the editor's selection list clean.
                        EditorInterface.Singleton.GetSelection().Clear();

                        // Register the original size with the editor's undo/redo system.
                        var undoRedo = EditorInterface.Singleton.GetEditorUndoRedo();
                        undoRedo.CreateAction("Resize RegionRect");
                        undoRedo.AddUndoProperty(this, "Region", Region);
                    }
                }
            }
            else {
                if(isDragging) {
                    GD.Print("Drag-resize completed.");
                    isDragging = false;
                    // We want the region to be re-selected (and nothing else) after we're done dragging around.
                    EditorInterface.Singleton.GetSelection().Clear();
                    EditorInterface.Singleton.GetSelection().AddNode(this);

                    // Register the change with the Editor's undo/redo system.
                    var undoRedo = EditorInterface.Singleton.GetEditorUndoRedo();
                    undoRedo.AddDoProperty(this, "Region", Region);
                    undoRedo.CommitAction();
                }
            }

            if(isDragging) {
                Vector2 mousePos = EditorInterface.Singleton.GetEditorViewport2D().GetMousePosition();
                var newSize = mousePos + dragAnchorOffset - Region.Position;
                Region = new Rect2(Region.Position, newSize);
            }
        }
    }

    public override void _Draw() {
        if(!Engine.IsEditorHint()) {
            return;
        }

        if (isDragging || IsSelectedInEditor()) {
            // Always half-transparent.
            var drawColor = EditorDrawColor;
            drawColor.A = 0.5f;
            DrawRect(rect: Region, color: drawColor, filled: true);

            // Early-exit from rendering control points at all if the region is very small.
            if (Region.Size.LengthSquared() <= 10.0f) {
                return;
            }

            var zoom = EditorInterface.Singleton.GetEditorViewport2D().GlobalCanvasTransform.Scale;
            var color = isDragging ? Colors.Green : Colors.White;
            var controlPointSizeUnscaled = new Vector2(EditorControlPointSize, EditorControlPointSize) / Scale;
            var zoomedControlPointSize = controlPointSizeUnscaled / zoom;
            DrawRect(new Rect2(Region.End - zoomedControlPointSize / 2f, zoomedControlPointSize), color, filled: true);
        }
    }

    // Returns true if this node is currently selected in the editor.
    private bool IsSelectedInEditor() {
        if(Engine.IsEditorHint()) {
            var selection = EditorInterface.Singleton.GetSelection();
            return selection?.GetSelectedNodes().Contains(this) ?? false;
        }
        return false;
    }

    private bool IsMouseOverHandle(Vector2 mousePos) {
        if(Engine.IsEditorHint()) {
            var zoom = EditorInterface.Singleton.GetEditorViewport2D().GlobalCanvasTransform.Scale;
            float handleRadius = EditorControlPointSize / zoom.X;
            // Get the node's world position to calculate the inverse transform
            var nodeTransform = GlobalTransform;

            // Convert mouse position to local coordinates of the node
            Vector2 localMousePos = nodeTransform.AffineInverse().BasisXform(mousePos - nodeTransform.Origin);
            return Region.End.DistanceTo(localMousePos) <= handleRadius;
        }
        return false;
    }
}
