using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

public partial class Merchant : Moveable
{
    MerchantUI merchantUI;

    [Export]
    public Godot.Collections.Array<ShopItem> ShopItems { get; set; }

    private bool ShopLoaded = false;

    public override void _Ready()
    {
        merchantUI = this.GetGameHUD().GetNode<MerchantUI>("MerchantUI");
    }

    public override void _Process(double delta)
    {
        //We load the shop here and not _Ready() because the shop UI clears items in it's _Ready(), which runs after this node
        //The shop UI does this to support dummy data in the editor for better preview capability
        if (ShopItems != null && !ShopLoaded)
        {
            foreach (ShopItem item in ShopItems)
            {
                merchantUI.AddWeapon(item);
            }

            ShopLoaded = true;
        }
    }

    public void OnInteract(InteractionArea area, Character character) {
        merchantUI.Show();
    }

    public void OnCharacterCannotInteract(InteractionArea area, Character character)
    {
        merchantUI.Hide();
    }
}
