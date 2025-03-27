using ExtensionMethods;
using Godot;
using System;

public partial class Weapon : Node2D
{
    private Character _instigator;
    // Returns the character who has equipped this weapon and should be the instigator for any damage it has caused.
    protected Character Instigator {
        get {
            if(_instigator == null) {
                _instigator = this.FindCharacterAncestor();
            }
            return _instigator;
        }
    }
    // Called when the weapon starts firing (i.e. in response to an input action being pressed).
    public virtual void PressFire()
    {
        
    }

    // Called when the weapon stops firing (i.e. in response to an input action being released).
    public virtual void ReleaseFire()
    {

    }
}
