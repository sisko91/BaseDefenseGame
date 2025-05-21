using Godot;
using System.Linq;
using ExtensionMethods;

// DisplacementMaskMarker renders a custom sprite to DisplacementMaskViewports in order to update where a tracked entity
// should affect locally-displaced/influenced entities (grass, snow, water, etc.).
public partial class DisplacementMaskMarker : Node2D
{
    // The sprite associated with this marker. If not assigned in the editor this will be discovered as the first child.
    [Export] protected Sprite2D DisplacementSprite = null;
    
    // The Node2D that this marker tracks the GlobalPosition of in the game world.
    public Node2D TrackedNode { get; private set; } = null;

    // The color assigned to the displacement sprite as SelfModulate.
    private Color DisplacementColor = Colors.White;
    
    public override void _Ready()
    {
        if (DisplacementSprite == null)
        {
            DisplacementSprite = this.GetChildrenOfType<Sprite2D>().FirstOrDefault();
        }
        if (DisplacementSprite == null)
        {
            GD.PushError($"No DisplacementSprite found or assigned for DisplacementMaskMarker: {Name}");
            return;
        }

        if (TrackedNode == null)
        {
            GD.PushError($"No tracked node assigned for DisplacementMaskMarker: {Name}");
        }
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(TrackedNode))
        {
            GlobalPosition = TrackedNode.GlobalPosition;
            DisplacementSprite.SelfModulate = DisplacementColor;
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
