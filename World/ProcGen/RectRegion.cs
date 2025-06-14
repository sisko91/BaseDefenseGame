using ExtensionMethods;
using Godot;
using System;

// RectRegion is a placeable node containing a rectangle that can be dragged to size in the editor.
[GlobalClass]
[Tool]
public sealed partial class RectRegion : Node2D
{
    [Export]
    public Vector2 Size { get; set; }

    // Each string is a group that the region will be registered with automatically on startup.
    [Export] public Godot.Collections.Array<string> AutoRegisterGroups = [];
    
    // Each tag is a string local to this region that can be used to provide context and identication to other logic.
    [Export] public Godot.Collections.Array<string> Tags = [];

    [ExportCategory("Editor View")]
    [Export]
    public Color EditorDrawColor { get; private set; } = Colors.BlueViolet;

    [Export]
    public float EditorControlPointSize = 10.0f;

    private bool isDragging = false;
    private Vector2 dragAnchorOffset = Vector2.Zero;

    public override void _Ready() {
        base._Ready();

        foreach (var tag in AutoRegisterGroups)
        {
            AddToGroup(tag);
        }

        InputMap.LoadFromProjectSettings();
    }

    public override void _Process(double delta) {
        if(!Engine.IsEditorHint()) {
            return;
        }
        QueueRedraw();

        Vector2 mousePos = EditorInterface.Singleton.GetEditorViewport2D().GetMousePosition();
        var overControlPoint = IsMouseOverControlPoint(mousePos);

        if (Input.IsActionJustPressed("click") && overControlPoint) {
            GD.Print("Drag-resize started.");
            isDragging = true;
            var globalSize = GlobalTransform.BasisXform(Size);
            dragAnchorOffset = globalSize - mousePos;
            // We don't want to select ANYTHING when we're dragging around, so wipe the editor's selection list clean.
            EditorInterface.Singleton.GetSelection().Clear();

            // Register the original size with the editor's undo/redo system.
            var undoRedo = EditorInterface.Singleton.GetEditorUndoRedo();
            undoRedo.CreateAction("Resize RegionRect");
            undoRedo.AddUndoProperty(this, "Size", Size);
        }
        else if (isDragging && !Input.IsActionPressed("click")) {
            GD.Print("Drag-resize completed.");
            isDragging = false;
            // We want the region to be re-selected (and nothing else) after we're done dragging around.
            EditorInterface.Singleton.GetSelection().Clear();
            EditorInterface.Singleton.GetSelection().AddNode(this);

            // Register the change with the Editor's undo/redo system.
            var undoRedo = EditorInterface.Singleton.GetEditorUndoRedo();
            undoRedo.AddDoProperty(this, "Size", Size);
            undoRedo.CommitAction();
        }

        if(isDragging) {
            Vector2 localMousePos = GlobalTransform.AffineInverse().BasisXform(mousePos + dragAnchorOffset);
            Size = localMousePos;
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
            DrawRect(rect: new Rect2(Vector2.Zero, Size), color: drawColor, filled: true);

            // Early-exit from rendering control points at all if the region is very small.
            if (Size.LengthSquared() <= 10.0f) {
                return;
            }

            var zoom = EditorInterface.Singleton.GetEditorViewport2D().GlobalCanvasTransform.Scale;
            var color = isDragging ? Colors.Green : Colors.White;
            var controlPointSizeUnscaled = new Vector2(EditorControlPointSize, EditorControlPointSize) / Scale;
            var zoomedControlPointSize = controlPointSizeUnscaled / zoom;
            DrawRect(new Rect2(Size - zoomedControlPointSize / 2f, zoomedControlPointSize), color, filled: true);
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

    private bool IsMouseOverControlPoint(Vector2 mousePos) {
        if(Engine.IsEditorHint()) {
            var zoom = EditorInterface.Singleton.GetEditorViewport2D().GlobalCanvasTransform.Scale;
            float cpRadius = EditorControlPointSize / zoom.X;

            // Convert mouse position to local coordinates of the node
            Vector2 localMousePos = GlobalTransform.AffineInverse().BasisXform(mousePos - GlobalTransform.Origin);
            return Size.DistanceTo(localMousePos) <= cpRadius;
        }
        return false;
    }

    // Returns a Rect2 positioned and scaled to match the Size and GlobalTransform of this RectRegion.
    public Rect2 GetGlobalRect() {
        var regionSizeGlobal = GlobalTransform.BasisXform(Size);
        return new Rect2(GlobalTransform.Origin, regionSizeGlobal);
    }
}
