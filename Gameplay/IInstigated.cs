using Godot;

// The instigator is used for attribution. If a weapon fires a projectile, the Player holding that weapon would be the logical instigator.
// (This is an old concept stolen from Unreal Engine)
public interface IInstigated
{
    public Character Instigator { get; set; }
}
