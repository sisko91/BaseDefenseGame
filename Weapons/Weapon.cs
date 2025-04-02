using ExtensionMethods;
using Godot;
using System;

public partial class Weapon : Node2D
{
    // How long the gun must wait between consecutive shots
    [Export]
    public float FireCooldownSeconds = 1f;

    //0 means no clip / no reload
    [Export]
    public int ClipSize = 0;

    //How long the gun must wait to reload the clip once empty
    [Export]
    public float ReloadSeconds = 1f;
    private int LastReloadTime = -1;
    private int RoundsLeft;

    [Export]
    public bool EnableAutofire = false;
    private double LastFireTime = -1;
    private bool IsFiring = false;

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

    public override void _Ready()
    {
        base._Ready();

        if (ClipSize > 0)
        {
            RoundsLeft = ClipSize;
        }
    }

    public override void _Process(double delta)
    {
        if (IsFiring && CanFire())
        {
            TryFire();
        }
    }

    // Called when the user presses the input action to fire
    public virtual void PressFire()
    {
        if (EnableAutofire)
        {
            IsFiring = true;
        }

        TryFire();
    }

    public void TryFire()
    {
        if (CanFire())
        {
            Fire();
        }
    }

    public virtual void Fire()
    {
        LastFireTime = Time.GetTicksUsec() / 1000000.0;
        if (ClipSize > 0)
        {
            RoundsLeft--;
        }
    }

    // Called when the user releases the input action to fire
    public virtual void ReleaseFire()
    {
        IsFiring = false;
    }

    private bool CanFire()
    {
        var timeSeconds = Time.GetTicksUsec() / 1000000.0;
        if (timeSeconds - FireCooldownSeconds < LastFireTime)
        {
            return false;
        }

        if (ClipSize > 0 && RoundsLeft == 0)
        {
            if (timeSeconds - ReloadSeconds < LastFireTime)
            {
                return false;
            }
            RoundsLeft = ClipSize;
        }

        return true;
    }
}
