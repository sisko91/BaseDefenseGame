using ExtensionMethods;
using Godot;
using System;

public partial class Main : Node
{
    // Cached reference to the world node instantiated as part of the main.tscn.
    private World world;
    // Cached reference to the player node instantiated below during scene startup.
    private Player player;

    [Export]
    public bool bEnableDebugRendering = true;

    // Configurable template for the player to instantiate within the world during startup.
    [Export]
    public PackedScene PlayerTemplate {  get; private set; }

    // The template scene to instantiate for the Pause Menu.
    [Export]
    public PackedScene PauseMenuTemplate { get; private set; }

    // During play, when the pause menu is open this reference tracks the node instance.
    private Node instancedPauseMenu = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        world = GetNode<World>("World");

        player = PlayerTemplate.Instantiate<Player>();
        player.Name = "Player";
        world.AddChild(player);

        player.HealthChanged += OnPlayerHealthChanged;

        //Weapon.ProjectileSpawner += OnPlayerShoot;

        if(bEnableDebugRendering)
        {
            DebugNodeExtensions.EnableDebugRenderers();
        }
    }

    private void OnPlayerHealthChanged(Character character, float newHealth, float oldHealth)
    {
        if(newHealth <= 0)
        {
            // ded
            GetTree().Quit();
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if(Input.IsActionJustPressed("pause_menu")) {
            // The menu removes itself as a child and deletes itself, but in case that expectation changes we can do it here as well.
            if(IsInstanceValid(instancedPauseMenu)) {
                RemoveChild(instancedPauseMenu);
                instancedPauseMenu.QueueFree();
                instancedPauseMenu = null;
            }
            else {
                instancedPauseMenu = PauseMenuTemplate.Instantiate();
                AddChild(instancedPauseMenu);
            }
        }
    }
}
