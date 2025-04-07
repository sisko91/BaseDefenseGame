using Godot;
using Godot.Collections;
using System;

public partial class MerchantUI : Control
{

    Label ItemDescription;
    public override void _Ready()
    {
        base._Ready();
        FocusMode = FocusModeEnum.All;
        VisibilityChanged += UpdateFocus;

        //Remove the boilerplate buttons we have in the editor
        //Keeping these in the editor helps with previewing what the UI looks like
        var items = GetNode<Container>("%Items");
        foreach (Node node in items.GetChildren()) {
            items.RemoveChild(node);
        }

        ItemDescription = GetNode<Label>("%ItemDescription");
        ItemDescription.Text = "";
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event.IsActionPressed("Cancel")) {
            AcceptEvent();
            Hide();
        }

        //Steal mouse input while open - the player can still move, but cant shoot
        //Otherwise they shoot when they click buttons, which is jank
        if (@event is InputEventMouseButton) {
            AcceptEvent();
        }
    }

    public void AddWeapon(ShopItem item)
    {
        var itemButton = GD.Load<PackedScene>("res://UI/MerchantUI/ItemButton.tscn").Instantiate<Button>();

        var texture = itemButton.GetNode<TextureRect>("%ItemTexture");
        var itemName = itemButton.GetNode<Label>("%ItemName");
        var itemPrice = itemButton.GetNode<Label>("%ItemPrice");

        //We probably want a custom texture for what shoes in the shop, this is just a placeholder
        var weapon = item.Weapon.Instantiate<Weapon>();
        texture.Texture = weapon.GetNode<Sprite2D>("Sprite2D").Texture;
        itemName.Text = item.Name;
        itemPrice.Text = item.Price.ToString();

        var items = GetNode<Container>("%Items");
        items.AddChild(itemButton);

        itemButton.MouseEntered += () => ItemHovered(item);
        itemButton.MouseExited += () => ItemExited(item);
    }

    private void UpdateFocus()
    {
        if (Visible) {
            GrabFocus();
        } else {
            ReleaseFocus();
        }
    }

    private void ItemHovered(ShopItem item)
    {
        ItemDescription.Text = item.Description;
    }

    private void ItemExited(ShopItem item)
    {
        ItemDescription.Text = "";
    }
}
