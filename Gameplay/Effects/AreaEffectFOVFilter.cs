using Godot;
using System;

[GlobalClass]
public partial class AreaEffectFOVFilter : AreaEffectFilter
{
    // The angle, in degrees, defining the field of view for detecting bodies. The cone/angle is centered on this node's GlobalRotation.
    // Even if the collision shape for this AreaEffect is not a perfect circle, detected bodies must still reside within this cone.
    [Export]
    public float FieldOfView { get; protected set; } = 90.0f;

    public override bool FilterNode(Node2D node, AreaEffect areaEffect) {
        // Vector from origin to point
        Vector2 toNode = (node.GlobalPosition - areaEffect.GlobalPosition).Normalized();
        Vector2 dir = Vector2.FromAngle(areaEffect.GlobalRotation); // pre-normalized

        // Half of the cone angle, converted to radians
        float halfAngleRads = Mathf.DegToRad(FieldOfView / 2.0f);

        // Use dot product to get angle between vectors
        float dot = Mathf.Clamp(dir.Dot(toNode), -1.0f, 1.0f);

        // Compute angle between vectors
        float angleBetweenRads = Mathf.Acos(dot);

        return angleBetweenRads <= halfAngleRads;
    }
}