using Godot;
using System.Collections.Generic;

public partial class PathMeshGenerator : Node2D
{
    [ExportCategory("Path Description")]
    // The bezier curve defining the route the path should take through the level.
    [Export]
    public Path2D Path { get; protected set; }

    // The material to assign to the path's generated mesh.
    [Export]
    public Material PathMeshMaterial { get; protected set; } = null;

    // How wide the path is. The width of the path is uniform across its full length.
    [Export]
    public float PathWidth { get; protected set; } = 200.0f;

    [ExportCategory("Advanced")]
    // How long each segment along the path should be when the mesh triangles are generated.
    [Export]
    public float GenerationStepSize = 16.0f;

    // When true, the UV coordinates (specifically the V) will be restarted at 0 every `TexCoordsRepeatDistance` units along the path.
    // This will cause the material to be repeated along the path multiple times.
    //
    // When false this will cause the material to be stretched along the *entire* path length a single time (good for simple textures or
    // dynamic shaders).
    [Export]
    public bool StretchTexCoords = true;

    // When StretchTexCoords is false, the PathMeshMaterial's UV coordinates will be restarted along the path after this distance is reached.
    [Export]
    public float TexCoordsRepeatDistance = 64.0f;

    public MeshInstance2D PathMeshInstance { get; protected set; }

    public override void _Ready() {
        if(Path == null) {
            GD.PrintErr("No Path2D assigned to `Path`.");
            return;
        }

        PathMeshInstance = new MeshInstance2D();
        AddChild(PathMeshInstance);

        PathMeshInstance.Mesh = GeneratePathMesh();
        PathMeshInstance.Material = PathMeshMaterial;
    }

    protected virtual Mesh GeneratePathMesh() {
        var curve = Path.Curve;

        var vertices = new List<Vector2>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();

        // Get the total length of the curve (in world units).
        float length = curve.GetBakedLength();
        float currentOffset = 0f;
        int segmentIndex = 0;
        while (currentOffset + GenerationStepSize <= length) {
            // Sample current and next positions on the path. The `t` notation is just for "time" because that's how most curves are
            // discussed / sampled.
            var t0 = currentOffset;
            var t1 = currentOffset + GenerationStepSize;

            var transform0 = curve.SampleBakedWithRotation(t0, cubic: true);
            var transform1 = curve.SampleBakedWithRotation(t1, cubic: true);

            Vector2 center0 = transform0.Origin;
            Vector2 center1 = transform1.Origin;
            Vector2 normal0 = transform0.Y.Normalized();
            Vector2 normal1 = transform1.Y.Normalized();

            Vector2 left0 = center0 - normal0 * (PathWidth / 2f);
            Vector2 right0 = center0 + normal0 * (PathWidth / 2f);
            Vector2 left1 = center1 - normal1 * (PathWidth / 2f);
            Vector2 right1 = center1 + normal1 * (PathWidth / 2f);

            // Add unique vertices for this segment
            int baseIdx = vertices.Count;

            vertices.Add(left0);   // 0
            vertices.Add(right0);  // 1
            vertices.Add(left1);   // 2
            vertices.Add(right1);  // 3

            if (StretchTexCoords) {
                float v0 = t0 / length;
                float v1 = t1 / length;
                uvs.Add(new Vector2(0, v0));
                uvs.Add(new Vector2(1, v0));
                uvs.Add(new Vector2(0, v1));
                uvs.Add(new Vector2(1, v1));
            }
            else {
                // With a repeating texture, the V coordinate always starts at v = 0 ("top" of the image) and extends to V = 1 ("bottom").
                // But we must keep in mind that since TexCoordsRepeatDistance==GenerationStepSize is NOT guaranteed, we need to do a bit
                // of math to figure out how far into the current UV repeat distance we are.
                // We need to divide our current distance by the repeat distance, take the remainder, and convert that to a 0-1 ratio.
                float v0 = (t0 % TexCoordsRepeatDistance) / TexCoordsRepeatDistance;
                float v1 = (t1 % TexCoordsRepeatDistance) / TexCoordsRepeatDistance;
                uvs.Add(new Vector2(0, v0));
                uvs.Add(new Vector2(1, v0));
                uvs.Add(new Vector2(0, v1));
                uvs.Add(new Vector2(1, v1));
            }

            // Add two triangles per segment
            indices.Add(baseIdx + 0);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);

            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 3);

            currentOffset += GenerationStepSize;
            segmentIndex++;
        }

        // Build the blob that specifies all of this packed data to the GPU.
        var surfaceArrays = new Godot.Collections.Array();
        surfaceArrays.Resize((int)Mesh.ArrayType.Max);
        surfaceArrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        surfaceArrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        surfaceArrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        // Build an ArrayMesh, which is just builder pattern for meshes that accepts the individual arrays.
        var mesh = new ArrayMesh();
        // TODO: Could maybe consider optimizing this to use TriangleStrip instead of Triangles, but modern GPUs shouldn't need this.
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArrays);
        return mesh;
    }
}
