using ExtensionMethods;
using Godot;
using System;

// Stairs teleport entities up or down a floor when walked over
public partial class Stairs : Area2D {
    [Export]
    public Stairs TargetStairs;

    [Export]
    bool goingUp;

    bool ignoreNextInteraction = false;

    public override void _Ready() {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node2D body) {
        if (ignoreNextInteraction) {
            ignoreNextInteraction = false;
            return;
        }

        if (body is Character character) {
            TargetStairs.ignoreNextInteraction = true;
            body.Position = TargetStairs.GlobalPosition;

            character.ChangeFloor(goingUp);
        }
    }

    private void OnBodyExited(Node2D body) {

    }
}
