using ExtensionMethods;
using Godot;
using Godot.Collections;
using System;
using static Godot.AnimationMixer;

public partial class LevelTransition : InteractionArea
{
    [Export]
    public string LevelKey;

    [Export]
    public string TargetLevelKey;

    [Export]
    public PackedScene TargetLevelScene;

    public String TargetLevelName;

    private World NewLevel;
    private World CurrentLevel;

    public override void _Ready() {
        base._Ready();
        if (String.IsNullOrEmpty(LevelKey)) {
            GD.PushError("Missing level key");
            return;
        }

        BodyEntered += OnBodyEntered;

        this.GetGameWorld().LevelKeyMap.Add(LevelKey, this);

        CurrentLevel = this.GetGameWorld();

    }

    private void OnBodyEntered(Node2D body) {
        //TODO: Add a spawn offset, then this can be enabled. This would
        //work well for things like doors where the player walks through them to a new area
        //LoadNewLevel();
    }

    protected override void OnInteract(Character character) {
        LoadNewLevel();
    }

    private void LoadNewLevel() {
        GD.Print("Loading new level!");

        if (!String.IsNullOrEmpty(TargetLevelName)) {
            NewLevel = Main.Instance.GetNode<World>(TargetLevelName);
        } else {
            NewLevel = TargetLevelScene.Instantiate<World>();
        }

        AnimationPlayer transitionAnimationPlayer = Main.Instance.TransitionScreen.GetNode<AnimationPlayer>("AnimationPlayer");
        transitionAnimationPlayer.Play("fade_out");
        transitionAnimationPlayer.AnimationFinished += FinishLoadNewLevel;
    }

    private void FinishLoadNewLevel(StringName animationName) {
        Main.GetActiveCamera().PositionSmoothingEnabled = false;
        Main.Instance.World = NewLevel;
        CurrentLevel.Hide();
        CurrentLevel.ProcessMode = ProcessModeEnum.Disabled;

        if (NewLevel.GetParent() == Main.Instance) {
            NewLevel.ProcessMode = ProcessModeEnum.Pausable;
            NewLevel.Show();
        } else {
            Main.Instance.AddChild(NewLevel);
        }

        LevelTransition target = NewLevel.LevelKeyMap[TargetLevelKey];
        target.TargetLevelName = CurrentLevel.Name;

        Player player = Main.Instance.Player;
        player.CollisionLayer = 1;
        player.CurrentElevationLevel = 0;
        player.CurrentRegion = null;
        player.Reparent(NewLevel);
        player.GlobalPosition = target.GlobalPosition;

        AnimationPlayer transitionAnimationPlayer = Main.Instance.TransitionScreen.GetNode<AnimationPlayer>("AnimationPlayer");
        transitionAnimationPlayer.AnimationFinished -= FinishLoadNewLevel;
        transitionAnimationPlayer.Play("fade_in");
    }
}
