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
        groupsList.TooltipText = $"Enabled Calls Overhead: {dbgRenderer.LastFrameDrawTime * 1000.0}ms\n" +
                                 $"Expired Calls Overhead: {dbgRenderer.LastFramePruneTime * 1000.0}ms";
        foreach (var groupName in dbgRenderer.DebugDrawCallGroupNames) {
            int drawCallCount = dbgRenderer.GetGroupSize(groupName);
            if (drawCallCount == 0) {
                continue; // don't bother listing empty groups.
            }
            var groupIdx = groupsList.AddItem(groupName);
            // There doesn't seem to be a good way to add additional text that isn't part of the item's identity/key (which is its text). But the tooltip is a handy place
            // to put the total drawcall count (if someone wants to see it).
            groupsList.SetItemTooltip(groupIdx, $"Draw calls in group: {drawCallCount}");
            if (dbgRenderer.DisabledDrawCallGroups.Contains(groupName)) {
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
