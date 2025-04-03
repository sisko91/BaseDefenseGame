using ExtensionMethods;
using Godot;
using System;

public partial class TestFireplace : StaticBody2D
{
    double FlickerMinTime = 0.05;
    double FlickerMaxTime = 0.3;
    Timer FlickerTimer;

    private double Energy = 3.0;
    private double TextureScale = 3.0;
    private double FlickerMinMultipler = 0.9;
    private double FlickerMaxMultiplier = 1.1;
    private double FlickerMultiplier = 1.0;

    private PointLight2D light;
    private AnimatedSprite2D sprite;

    public override void _Ready()
    {
        base._Ready();
        FlickerTimer = new Timer();
        FlickerTimer.OneShot = true;
        FlickerTimer.Timeout += Flicker;
        AddChild(FlickerTimer);

        light = GetNode<PointLight2D>("PointLight2D");
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }
    public override void _Process(double delta)
    {
        base._Process(delta);

        var dayTime = this.GetGameWorld().DayNight.GetDayTime();
        if (dayTime > 0.25 && dayTime < 0.75)
        {
            sprite.Animation = "off";
            light.Visible = false;
            return;
        }
        sprite.Animation = "on";
        light.Visible = true;

        if (FlickerTimer.TimeLeft == 0)
        {
            var nextFlickerTime = new Random().NextDouble() * (FlickerMaxTime - FlickerMinTime) + FlickerMaxTime;
            FlickerTimer.WaitTime = nextFlickerTime;
            FlickerTimer.Start();
        }

        light.Energy = Mathf.Lerp(light.Energy, (float)(Energy * FlickerMultiplier), 0.05f);
        light.TextureScale = Mathf.Lerp(light.TextureScale, (float)(TextureScale * FlickerMultiplier), 0.05f);
    }

    private void Flicker()
    {
        FlickerMultiplier = new Random().NextDouble() * (FlickerMaxMultiplier - FlickerMinMultipler) + FlickerMaxMultiplier;
    }
}
