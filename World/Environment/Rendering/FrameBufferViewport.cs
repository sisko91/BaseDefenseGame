using Godot;

// FrameBufferViewport is a self-managing buffer that mirrors the contents of another viewport each frame.
public partial class FrameBufferViewport : SubViewport
{
    [Export] public SubViewport Source;

    public Sprite2D BufferSprite { get; private set; }

    public override void _Ready()
    {
        if (Source == null)
        {
            GD.PushError($"FrameBufferViewport {Name} has no upstream Source viewport and will not be functional.");
            return;
        }
        
        // Make sure this viewport size and format matches the source.
        Size = Source.Size;
        UseHdr2D = Source.UseHdr2D;
        
        // Construct a sprite that renders the source's viewport, and covers this viewport completely.
        // Note: We don't have a camera so there's no scaling to worry about. Just raw pixel size.
        BufferSprite = new Sprite2D();
        BufferSprite.Name = "BufferSprite";
        BufferSprite.Texture = Source.GetTexture();
        BufferSprite.Offset = BufferSprite.Texture.GetSize() / 2f;
        AddChild(BufferSprite);
    }
}
