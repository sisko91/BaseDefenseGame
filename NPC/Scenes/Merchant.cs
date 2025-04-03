using Godot;
using System;

public partial class Merchant : Moveable
{
    public void OnInteract(InteractionArea area, Character character)
    {
        GD.Print("Interacted with merchant");
    }
}
