using ExtensionMethods;
using Godot;
using System;

// Stairs teleport entities to a specified point and elevation when walked over
public partial class Stairs : InteractionArea {
    [Export]
    public Stairs TargetStairs;

    //TODO: This isn't needed anymore, but I don't know why. When changing floors currently,
    //a new enter event is not triggered for the destination stairs for some reason
    //bool ignoreNextInteraction = false;

    public BuildingRegion OwningRegion { get; set; }

    public override void _Ready() {
        base._Ready();
        if (TargetStairs == null) {
            GD.PushError("Missing target stairs");
            return;
        }

        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body) {
        if (body is Character c && c.CurrentRegion != OwningRegion) {
            return;
        }

        /*
        if (ignoreNextInteraction) {
            ignoreNextInteraction = false;
            return;
        }
        */

        if (body is Character character) {
            TakeStairs(character);
        }
    }

    protected override void OnInteract(Character character) {
        TakeStairs(character);
    }

    private void TakeStairs(Character character) {
        //TargetStairs.ignoreNextInteraction = true;
        character.GlobalPosition = TargetStairs.GlobalPosition;

        var level = TargetStairs.OwningRegion == null ? 0 : TargetStairs.OwningRegion.ElevationLevel;

        //If the target stairs are not in a building, assume they are on the bottom floor
        character.ChangeFloor(TargetStairs.OwningRegion == null ? 0 : TargetStairs.OwningRegion.ElevationLevel);
    }
}
