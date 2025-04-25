using Godot;
using System.Collections.Generic;

public partial class DebugDrawCallRenderer : Control
{
    public override void _Ready()
    {
        // Ensure DebugDraw fills the entire screen
        Size = GetViewportRect().Size;
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        FocusMode = FocusModeEnum.None; //Dont steal focus from other controls. This should never be focused
        MouseFilter = MouseFilterEnum.Ignore; //Dont eat mouse clicks
    }
    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    private enum DrawCallType {
        Line,
        Rect,
        Circle
    }

    private struct DebugDrawCallEntry
    {
        public DrawCallType Type;
        public Vector2 Origin;
        public Vector2 Endpoint;
        public Color Color;
        public double SpawnTime;
        public double LifeTime;
        public bool FillInterior; // ignored for shapes without an interior (e.g. lines)
    }

    private Dictionary<string, List<DebugDrawCallEntry>> DebugDrawCallGroups = new Dictionary<string, List<DebugDrawCallEntry>>();

    private void PushDrawCall(DrawCallType type, Vector2 origin, Vector2 endpoint, Color color, bool fillInterior, double lifetime, string group) {
        if (!DebugDrawCallGroups.ContainsKey(group)) {
            DebugDrawCallGroups[group] = new List<DebugDrawCallEntry>();
        }

        var callsOfGroup = DebugDrawCallGroups[group];
        callsOfGroup.Add(new DebugDrawCallEntry {
            Type = type,
            Origin = origin,
            Endpoint = endpoint,
            Color = color,
            LifeTime = lifetime,
            SpawnTime = Time.GetTicksMsec() / 1000.0,
            FillInterior = fillInterior,
        });
        QueueRedraw();
    }

    public void PushLine(Vector2 origin, Vector2 endpoint, Color color, double lifeTime = -1, string group = "default") {
        PushDrawCall(
            type: DrawCallType.Line,
            origin: origin, 
            endpoint: endpoint, 
            color: color, 
            fillInterior: false, 
            lifetime: lifeTime, 
            group: group);
    }

    public void PushRect(Vector2 origin, Vector2 size, Color color, bool fillInterior = false, double lifeTime = -1, string group = "default", bool centerOrigin = true) {
        if(centerOrigin) {
            origin = origin - size/2f;
        }
        PushDrawCall(
            type: DrawCallType.Rect,
            origin: origin,
            endpoint: origin + size,
            color: color,
            fillInterior: fillInterior,
            lifetime: lifeTime,
            group: group);
    }

    public void PushCircle(Vector2 origin, float radius, Color color, bool fillInterior = false, double lifeTime = -1, string group = "default") {
        PushDrawCall(
            type: DrawCallType.Circle,
            origin: origin,
            endpoint: origin + Vector2.One * radius,
            color: color,
            fillInterior: fillInterior,
            lifetime: lifeTime,
            group: group);
    }

    public override void _Draw()
    {
        var currentTime = Time.GetTicksMsec() / 1000.0;
        foreach(KeyValuePair<string, List<DebugDrawCallEntry>> entry in DebugDrawCallGroups) {
            var drawCallsInGroup = entry.Value;
            // Iterate backwards so that removing elements doesn't shift indices
            for (int i = drawCallsInGroup.Count - 1; i >= 0; i--) {
                var drawCall = drawCallsInGroup[i];
                if (drawCall.LifeTime >= 0) {
                    if (currentTime - drawCall.SpawnTime > drawCall.LifeTime) {
                        drawCallsInGroup.RemoveAt(i);
                        continue;
                    }
                }

                if (drawCall.Endpoint != drawCall.Origin) {
                    switch (drawCall.Type) {
                        case DrawCallType.Line:
                            DrawLine(drawCall.Origin, drawCall.Endpoint, drawCall.Color);
                            break;
                        case DrawCallType.Rect:
                            DrawRect(
                                rect: new Rect2(drawCall.Origin, drawCall.Endpoint - drawCall.Origin), 
                                color: drawCall.Color,
                                filled: false);
                            break;
                        case DrawCallType.Circle:
                            DrawCircle(
                                position: drawCall.Origin, 
                                radius: drawCall.Origin.DistanceTo(drawCall.Endpoint), 
                                color:drawCall.Color,
                                filled: false);
                            break;
                        default:
                            GD.PrintErr($"Unknown DebugDrawCallType encountered: {drawCall.Type}, did you forget to add support?");
                            break;
                    }
                }
                else {
                    if (drawCall.Type != DrawCallType.Circle) {
                        // Points should be pushed as circles. Anything else is weird but we can technically do it.
                        GD.PushWarning($"Debug DrawCall for {drawCall.Type} was a single point. (Group={entry.Key}, Point={drawCall.Origin}, Color={drawCall.Color})");
                    }
                    DrawCircle(drawCall.Origin, 1, drawCall.Color);
                }
            }
        }
    }

    public void Clear(string group) {
        if (!DebugDrawCallGroups.ContainsKey(group)) { return; }
        DebugDrawCallGroups[group].Clear();
    }
}
