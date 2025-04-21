using Godot;
using System;
using System.Collections.Generic;

public partial class PathMesh : Node2D
{
    [ExportCategory("Path Description")]
    // The bezier curve defining the route the path should take through the level.
    [Export]
    public Path2D Path { get; protected set; }

    // The material to assign to the path's generated mesh.
    [Export]
    public Material PathMeshMaterial { get; protected set; } = null;

    [Export]
    public Godot.Collections.Array<PathDecorator> Decorators { get; protected set; } = [];

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

    public float Length { 
        get {
            return Path.Curve.GetBakedLength();
        } 
    }

    // Direct reference to the generated mesh instance for the path itself.
    protected MeshInstance2D RuntimePathMeshInstance { get; set; }

    // Access the parent container that the generated path mesh is added to during runtime.
    protected Node RuntimeContainerForPath { 
        get {
            return GetNode("RuntimeGenerated/PathContainer");
        } 
    }

    // Access the parent container for generated nodes that should always render below the path mesh at runtime. Populated primarily
    // by decorators.
    protected Node RuntimeContainerBelowPath {
        get {
            return GetNode("RuntimeGenerated/BelowPathContainer");
        }
    }

    // Access the parent container for generated nodes that should always render above the path mesh at runtime. Populated primarily
    // by decorators.
    protected Node RuntimeContainerAbovePath {
        get {
            return GetNode("RuntimeGenerated/AbovePathContainer");
        }
    }

    // Internal reference to every child node added to this PathMesh via AddRuntimeGeneratedChild().
    private Godot.Collections.Array<Node2D> AllRuntimeGeneratedChildren = [];

    public override void _Ready() {
        if(Path == null) {
            GD.PrintErr("No Path2D assigned to `Path`.");
            return;
        }
        Regenerate();
    }

    // Regenerates the path mesh instance, throwing away and freeing any previously-generated data this node owns in the process.
    public void Regenerate() {
        // Use the existence of the runtime mesh to determine if everything needs to be reset first.
        if(IsInstanceValid(RuntimePathMeshInstance)) {
            GD.Print($"Regenerating path mesh {GetParent()?.Name}.{Name}");
            RuntimeContainerForPath.RemoveChild(RuntimePathMeshInstance);
            RuntimePathMeshInstance?.QueueFree();

            // Loop over anything Runtime-generated and delete it. This is done instead of looping over each container's children
            // so that we can design PathMesh scenes with things in those containers statically and have those retained even when the
            // path gets regenerated.
            foreach (var child in AllRuntimeGeneratedChildren) {
                if(IsInstanceValid(child)) {
                    child.GetParent()?.RemoveChild(child);
                    child.QueueFree();
                }
            }
            AllRuntimeGeneratedChildren.Clear();
        }

        RuntimePathMeshInstance = new MeshInstance2D();
        RuntimePathMeshInstance.Mesh = GeneratePathMesh();
        RuntimePathMeshInstance.Material = PathMeshMaterial;

        RuntimeContainerForPath.AddChild(RuntimePathMeshInstance);

        foreach (var decorator in Decorators) {
            decorator.ApplyTo(this);
        }
    }

    // Should be used instead of AddChild() when adding generated objects to this PathMesh. Primarily used by PathDecorators, this ensures
    // that children are added at the correct relative location in the PathMesh's scene hierarchy.
    // By default, objects are added above the generated path, but can be added below by passing belowPath: true.
    public void AddRuntimeGeneratedChild(Node2D newChildNode, bool belowPath = false) {
        AllRuntimeGeneratedChildren.Add(newChildNode);
        if (belowPath) {
            RuntimeContainerBelowPath.AddChild(newChildNode);
        }
        else {
            RuntimeContainerAbovePath.AddChild(newChildNode);
        }
    }

    // Samples the edge of the PathMesh at a relative alpha value along its length.
    // lengthAlpha: [0 -> 1.0] percentage of the overall path length, defines the relative point along the path to sample the edge at.
    // widthAlpha: [-1.0 to 1.0] alpha determining where along the path's width to sample. -1.0 indicates to sample the left edge of the
    //             path, while 1.0 indicates to sample the right edge, and 0.0 indicates sampling the direct middle.
    // cubic: When true cubic interpolation is used, otherwise linear is used. Cubic interpolation tends to follow the curves better, but linear is faster (and often, precise enough).
    public Vector2 SampleEdgeAlpha(float lengthAlpha, float widthAlpha, bool cubic = false) {
        float length = Path.Curve.GetBakedLength();
        float offset = length * lengthAlpha;
        float widthOffset = PathWidth * widthAlpha;
        return SampleEdge(offset, widthOffset, cubic);
    }

    // Samples the edge of the PathMesh at a set point along its length.
    // lengthAlpha: [0 -> 1.0] percentage of the overall path length, defines the relative point along the path to sample the edge at.
    // widthOffset: Where along the width of the path to sample from at the designated distance. This value is clamped to [-PathWidth/2, PathWidth/2].
    // cubic: When true cubic interpolation is used, otherwise linear is used. Cubic interpolation tends to follow the curves better, but linear is faster (and often, precise enough).
    public Vector2 SampleEdge(float distance, float widthOffset, bool cubic = false) {
        var sampledTransform = Path.Curve.SampleBakedWithRotation(distance, cubic: cubic);
        var normal = sampledTransform.Y.Normalized();
        widthOffset = Mathf.Clamp(widthOffset, -PathWidth / 2f, PathWidth / 2f);
        return sampledTransform.Origin + normal * widthOffset;
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

            var left0 = SampleEdge(t0, -PathWidth/2f, cubic: true);
            var right0 = SampleEdge(t0, PathWidth / 2f, cubic: true);
            var left1 = SampleEdge(t1, -PathWidth / 2f, cubic: true);
            var right1 = SampleEdge(t1, PathWidth / 2f, cubic: true);

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
