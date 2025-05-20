using Godot;
using System;

public partial class Window : Node2D
{
    public bool Open = true;
    private GradientTexture2D LightGradient;

    public override void _Ready() {
        base._Ready();
        LightGradient = GD.Load<GradientTexture2D>("res://art/World/daynight_window.tres");
    }
    public override void _Process(double delta) {
        base._Process(delta);

        var light = GetNode<PointLight2D>("Light");
        light.Color = DayNight.Instance.DayNightColor;
        var distFromNoon = 2 * Math.Abs(0.5f - DayNight.Instance.GetDayTime());
        var energyMult = 0.1 + 1 - distFromNoon;
        light.Energy = (float) (0.3 * energyMult);

        var pane = GetNode<Sprite2D>("Pane");
        var paneTex = (GradientTexture2D) pane.Texture;
        var panelColor = LightGradient.Gradient.Sample((float)(DayNight.Instance.GetDayTime()));
        paneTex.Gradient.SetColor(0, panelColor);
    }
}
