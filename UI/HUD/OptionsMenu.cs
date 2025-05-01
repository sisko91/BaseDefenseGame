using ExtensionMethods;
using Godot;
using System;

public partial class OptionsMenu : TabContainer
{
    public override void _Ready() {
        base._Ready();
        SyncDevOptions();

        VisibilityChanged += UpdateFocus;

        ItemList groupsList = GetNode<ItemList>("%Debug Draw Calls");
        groupsList.MultiSelected += OnDebugDrawCallGroupSelectionChanged;
    }

    private void UpdateFocus()
    {
        if (Visible) {
            GrabFocus();
            SyncDebugDrawCallGroups();
        } else {
            ReleaseFocus();
        }
    }

    private void SyncDevOptions() {
        CheckButton drawSteering = GetNode<CheckButton>("%DrawSteeringToggle");
        drawSteering.SetPressedNoSignal(DebugConfig.Instance.DRAW_STEERING);
        drawSteering.Toggled += DebugConfig.Instance.SetDrawSteering;

        CheckButton drawNav = GetNode<CheckButton>("%DrawNavToggle");
        drawNav.SetPressedNoSignal(DebugConfig.Instance.DRAW_NAVIGATION);
        drawNav.Toggled += DebugConfig.Instance.SetDrawNavigation;

        CheckButton drawCollisionBodies = GetNode<CheckButton>("%DrawCollisionBodyToggle");
        drawCollisionBodies.SetPressedNoSignal(DebugConfig.Instance.DRAW_COLLISION_BODY_RADIUS);
        drawCollisionBodies.Toggled += DebugConfig.Instance.SetDrawCollisionBodies;

        CheckButton drawCollisionBoundingBox = GetNode<CheckButton>("%DrawCollisionBoundingBoxToggle");
        drawCollisionBoundingBox.SetPressedNoSignal(DebugConfig.Instance.DRAW_COLLISION_BOUNDING_BOX);
        drawCollisionBoundingBox.Toggled += DebugConfig.Instance.SetDrawCollisionBoundingBox;
    }

    private void SyncDebugDrawCallGroups() {
        ItemList groupsList = GetNode<ItemList>("%Debug Draw Calls");
        groupsList.Clear();

        var dbgRenderer = DebugNodeExtensions.GetDebugDrawCallRenderer();
        foreach(var groupName in dbgRenderer.DebugDrawCallGroupNames) {
            if (dbgRenderer.IsGroupEmpty(groupName)) {
                continue; // don't bother listing empty groups.
            }
            var groupIdx = groupsList.AddItem(groupName);
            if(dbgRenderer.DisabledDrawCallGroups.Contains(groupName)) {
                groupsList.Deselect(groupIdx);
            }
            else {
                groupsList.Select(groupIdx, single: false);
            }
        }
    }

    private void OnDebugDrawCallGroupSelectionChanged(long index, bool selected) {
        ItemList groupsList = GetNode<ItemList>("%Debug Draw Calls");
        var groupName = groupsList.GetItemText((int)index); 
        if(selected) {
            DebugNodeExtensions.EnableDebugDrawCallGroup(groupName);
        }
        else {
            DebugNodeExtensions.DisableDebugDrawCallGroup(groupName);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event.IsActionPressed("Cancel")) {
            AcceptEvent();
            Visible = false;
        }
    }
}
