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
        QueueRedraw();
    }

    private struct DebugLineEntry
    {
        public Vector2 Origin;
        public Vector2 Endpoint;
        public Color Color;
        public double SpawnTime;
        public double LifeTime;
    }

    private Dictionary<string, List<DebugLineEntry>> DebugLineGroups = new Dictionary<string, List<DebugLineEntry>>();

    public void PushLine(Vector2 origin, Vector2 endpoint, Color color, double lifeTime = -1, string group = "default")
    {
        if (!DebugLineGroups.ContainsKey(group)) {
            DebugLineGroups[group] = new List<DebugLineEntry>();
        }

        var debugLines = DebugLineGroups[group];

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
        foreach(KeyValuePair<string, List<DebugLineEntry>> entry in DebugLineGroups) {
            var debugLines = entry.Value;
            // Iterate backwards so that removing elements doesn't shift indices
            for (int i = debugLines.Count - 1; i >= 0; i--) {
                var lineEntry = debugLines[i];
                if (lineEntry.LifeTime >= 0) {
                    if (currentTime - lineEntry.SpawnTime > lineEntry.LifeTime) {
                        debugLines.RemoveAt(i);
                        continue;
                    }
                }

                if (lineEntry.Endpoint != lineEntry.Origin) {
                    DrawLine(lineEntry.Origin, lineEntry.Endpoint, lineEntry.Color);
                } else {
                    DrawCircle(lineEntry.Origin, 1, lineEntry.Color);
                }
            }
        }
    }

    public void Clear(string group) {
        if (!DebugLineGroups.ContainsKey(group)) { return; }
        DebugLineGroups[group].Clear();
    }
}
