using ExtensionMethods;
using Godot;
using System;

public partial class Merchant : Moveable
{
    MerchantUI merchantUI;

    public override void _Ready()
    {
        base._Ready();
        merchantUI = this.GetGameHUD().GetNode<MerchantUI>("MerchantUI");
    }

    public void OnInteract(InteractionArea area, Character character)
    {
        merchantUI.Load();
    }
}
