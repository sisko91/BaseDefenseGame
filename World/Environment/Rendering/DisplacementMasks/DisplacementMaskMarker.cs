using Godot;
using System.Linq;
using ExtensionMethods;

// DisplacementMaskMarker renders a custom sprite to DisplacementMaskViewports in order to update where a tracked entity
// should affect locally-displaced/influenced entities (grass, snow, water, etc.).
public partial class DisplacementMaskMarker : Node2D
{
    // The Node2D that this marker tracks the GlobalPosition of in the game world.
    public Node2D TrackedNode { get; private set; } = null;
    
    public override void _Ready()
    {
        var sprite = this.GetChildrenOfType<Sprite2D>().FirstOrDefault();
        if (sprite == null)
        {
            GD.PushError($"No sprite child found for DisplacementMaskMarker: {Name}");
            return;
        }

        if (TrackedNode == null)
        {
            GD.PushError($"No tracked node found for DisplacementMaskMarker: {Name}");
            return;
        }
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(TrackedNode))
        {
            GlobalPosition = TrackedNode.GlobalPosition;
        }
        else
        {
            QueueFree();
        }
    }
    
    // Updates the tracking information for this Marker and registers it with the DisplacementMaskViewport for rendering.
    public void RegisterOwner(Node2D owner)
    {
        if (TrackedNode != null)
        {
            GD.PushError($"Duplicate registration: DisplacementMaskMarker {Name} is already registered to track node {TrackedNode.Name}.");
            return;
        }
        TrackedNode = owner;
        Main.GetDisplacementMaskViewport().RegisterMarker(this);
    }
}
