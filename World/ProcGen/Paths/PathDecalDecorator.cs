using ExtensionMethods;
using Godot;
using System;
using System.Drawing;

// Decorates a PathMesh with ground decals. This is mostly prototyping.
[GlobalClass]
public partial class PathDecalDecorator : PathDecorator
{
    // The textures to use for decal sprites rendered over the path.
    [Export]
    public Godot.Collections.Array<Texture2D> DecalTextures { get; set; } = [];

    // Scaling factor to apply to decal sprites placed by this decorator.
    [Export]
    public float DecalScale { get; protected set; } = 1.0f;

    // The length of each segment along the path that we want to base Decal placement calculations on.
    [Export]
    public float LinearSegmentLength { get; protected set; } = 200.0f;

    // The 
    [Export]
    public float MinDecalsPerSegment = 1.0f;

    [Export]
    public float MaxDecalsPerSegment = 2.0f;

    public override void ApplyTo(PathMesh pathMesh) {
        float length = pathMesh.Length;
        GD.Print("LENGTH: " + length);

        for (float distance = 0; distance < length; distance += LinearSegmentLength) {
            float endDistance = distance + LinearSegmentLength;
            float segmentLength = LinearSegmentLength;
            float minDecals = MinDecalsPerSegment;
            float maxDecals = MaxDecalsPerSegment;
            // Handle partial segments at the end of the path, which need a percentage of the decals spawned in full segments.
            if(endDistance > length) {
                endDistance = length;
                segmentLength = (endDistance - length);
                float segmentRatio = segmentLength / LinearSegmentLength;
                minDecals *= segmentRatio;
                maxDecals *= segmentRatio;
            }

            Vector2 left0 = pathMesh.SampleEdge(distance, -pathMesh.PathWidth  /2f);
            Vector2 left1 = pathMesh.SampleEdge(endDistance, -pathMesh.PathWidth / 2f);
            Vector2 right0 = pathMesh.SampleEdge(distance, pathMesh.PathWidth / 2f);
            Vector2 right1 = pathMesh.SampleEdge(endDistance, pathMesh.PathWidth / 2f);

            // Decide how many decals to spawn in this segment.
            float decalsToSpawn = Mathf.Max(minDecals, GD.Randf() * maxDecals);
            for(int i = 0; i < decalsToSpawn; i++) {
                // Pick a random distance along the segment.
                float sampleDist = distance + GD.Randf() * segmentLength;

                // Pick a random width coordinate at that sample distance
                float sampleWidth = ((GD.Randf() * 2) - 1f) * (pathMesh.PathWidth / 2f);

                Vector2 point = pathMesh.SampleEdge(sampleDist, sampleWidth);

                // Triangle A (left0), B (right0), C (right1)
                // Triangle D (right1), E (left1), F (left0)
                /*Vector2 point;
                if (u + v <= 1f) {
                    // Interpolate within triangle (left0, right0, right1)
                    point = left0 + u * (right0 - left0) + v * (right1 - left0);
                }
                else {
                    // Flip u,v to get barycentric coords for the second triangle (right1, left1, left0)
                    u = 1f - u;
                    v = 1f - v;

                    point = right1 + u * (left1 - right1) + v * (left0 - right1);
                }*/

                var sprite = new Sprite2D();
                sprite.GlobalPosition = point;
                sprite.Texture = DecalTextures.PickRandom();
                pathMesh.AddRuntimeGeneratedChild(sprite);
                sprite.Scale = new Vector2(DecalScale, DecalScale);
            }
        }
    }
}
