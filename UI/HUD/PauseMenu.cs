using Godot;
using System;

// Pause Menu is... the pause menu. It shows up when you pause.
public partial class PauseMenu : CanvasLayer
{

    public PauseMenu() {
        // Only run _Process() when the tree is otherwise paused.
        this.ProcessMode = ProcessModeEnum.WhenPaused;
    }

    // Runs when the menu is added to the scene tree.
    public override void _EnterTree() {
        base._EnterTree();

        GetTree().Paused = true;
    }

    // Runs when the menu is removed from the scene tree.
    public override void _ExitTree() {
        base._ExitTree();

        GetTree().Paused = false;
    }

    // Runs after the menu is completely added to the scene tree.
    public override void _Ready() {
        base._Ready();
    }

    public override void _Process(double delta) {
        base._Process(delta);

        // Since everything else is paused when this menu is open we need to detect closure requests.
        if (Input.IsActionJustPressed("pause_menu")) {
            CloseMenu();
        }
    }

    // Signal binding for button presses.
    public void CloseMenu() {
        GetParent()?.RemoveChild(this);
        QueueFree();
    }

    // Signal binding for button presses.
    public void QuitGame() {
        GetTree().Quit();
    }
}
