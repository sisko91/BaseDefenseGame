using Godot;
using System;

[GlobalClass]
public abstract partial class AreaEffectFilter : Resource
{
    // Returns true if the filter passes for the node and the node should be included. Returns false to exclude the node from further
    // processing.
    public abstract bool FilterNode(Node2D node, AreaEffect areaEffect);
}
