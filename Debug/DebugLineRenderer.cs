using Godot;
using System.Collections.Generic;

public partial class DebugLineRenderer : Control
{
    public override void _Ready()
    {
        // Ensure DebugDraw fills the entire screen
        Size = GetViewportRect().Size;
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
    }
    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // Keep redrawing.
        if (debugLines.Count > 0)
        {
            QueueRedraw();
        }
    }

    private struct DebugLineEntry
    {
        public Vector2 Origin;
        public Vector2 Endpoint;
        public Color Color;
        public double SpawnTime;
        public double LifeTime;
    }

    private List<DebugLineEntry> debugLines = new List<DebugLineEntry>();

    public void PushLine(Vector2 origin, Vector2 endpoint, Color color, double lifeTime = -1)
    {
        debugLines.Add(new DebugLineEntry
        {
            Origin = origin,
            Endpoint = endpoint,
            Color = color,
            LifeTime = lifeTime,
            SpawnTime = Time.GetTicksMsec() / 1000.0,
        });
        QueueRedraw();
    }

    public override void _Draw()
    {
        var currentTime = Time.GetTicksMsec() / 1000.0;
        // Iterate backwards so that removing elements doesn't shift indices
        for (int i = debugLines.Count - 1; i >= 0; i--)
        {
            var lineEntry = debugLines[i];
            if (lineEntry.LifeTime > 0)
            {
                if (currentTime - lineEntry.SpawnTime > lineEntry.LifeTime)
                {
                    debugLines.RemoveAt(i);
                    continue;
                }
            }

            DrawLine(lineEntry.Origin, lineEntry.Endpoint, lineEntry.Color, 5.0f);
        }
    }
}
