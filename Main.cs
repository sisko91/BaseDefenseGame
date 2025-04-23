using ExtensionMethods;
using Godot;
using System;

public partial class Main : Node
{
    [Export]
    public PackedScene WorldScene { get; private set; }

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

    private Timer ShaderTimer;

    public static Main Instance { get; private set; }

    public Main() {
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        world = WorldScene.Instantiate<World>();
        world.Name = "World";
        world.ProcessMode = ProcessModeEnum.Pausable;
        AddChild(world);

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
        MoveChild(playerCamera, 0);

        ShaderTimer = new Timer();
        ShaderTimer.WaitTime = 3600;
        ShaderTimer.Autostart = true;
        AddChild(ShaderTimer);

        Instance = this;
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
        //https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/canvas_item_shader.html
        //The TIME variable in shaders it not affected by pausing. The framework recommendation is to use your own global parameter for time if you want to pause shader effects that use TIME
        RenderingServer.GlobalShaderParameterSet("time", ShaderTimer.WaitTime - ShaderTimer.TimeLeft);
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

    public static Camera2D GetActiveCamera() {
        return Instance.playerCamera;
    }
}
