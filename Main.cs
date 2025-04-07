using ExtensionMethods;
using Godot;
using System;

public partial class Main : Node
{
    // Cached reference to the world node instantiated as part of the main.tscn.
    private World world;
    // Cached reference to the player node instantiated below during scene startup.
    private Player player;
    private PlayerCamera playerCamera;

    [Export]
    public bool bEnableDebugRendering = true;

    // Configurable template for the player to instantiate within the world during startup.
    [Export]
    public PackedScene PlayerTemplate {  get; private set; }

    // The template scene to instantiate for the Pause Menu.
    [Export]
    public PackedScene PauseMenuTemplate { get; private set; }

    // Cached reference to the PauseMenu scene node defined on main.tscn.
    private PauseMenu pauseMenu = null;

    public Main() {
        // The main scene is NEVER paused, as it handles top-level input processing for the game (menus, terminal signals, etc.)
        ProcessMode = ProcessModeEnum.Always;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        world = GetNode<World>("World");

        player = PlayerTemplate.Instantiate<Player>();
        player.Name = "Player";
        //Place player in a dummy node so we can control the render order in the world
        world.PlayerContainerNode.AddChild(player);

        player.HealthChanged += OnPlayerHealthChanged;

        //Weapon.ProjectileSpawner += OnPlayerShoot;

        if(bEnableDebugRendering)
        {
            DebugNodeExtensions.EnableDebugRenderers();
        }

        pauseMenu = GetNode<PauseMenu>("PauseMenu");
        // Refresh the game's pause state any time the pause menu is opened or closed.
        pauseMenu.VisibilityChanged += CheckAndUpdatePause;

        playerCamera = new PlayerCamera();
        playerCamera.Target = player;
        AddChild(playerCamera);
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
    }

    // Reviews current game state and determines if the main scene should be paused or unpaused.
    // Today only the PauseMenu causes the game to pause, but eventually other parameters (e.g. world events, messages to players, etc.)
    // might want to momentarily pause the game as well.
    private void CheckAndUpdatePause() {
        if(IsInstanceValid(pauseMenu) && pauseMenu.Visible) {
            GetTree().Paused = true;
        }
        else {
            GetTree().Paused = false;
        }
    }
}
