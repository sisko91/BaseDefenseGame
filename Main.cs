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

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        world = GetNode<World>("World");

        player = PlayerTemplate.Instantiate<Player>();
        player.Name = "Player";
        world.AddChild(player);

        //Weapon.ProjectileSpawner += OnPlayerShoot;

        if(bEnableDebugRendering)
        {
            DebugNodeExtensions.EnableDebugRenderers();
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
