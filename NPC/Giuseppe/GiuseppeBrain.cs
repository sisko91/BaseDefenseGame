using Godot;
using System;
using System.Linq;

// Giuseppe is an NPC for testing dialog and interactions. His brain serves as a testbed for those features.
public partial class GiuseppeBrain : Brain
{
    private Player trackedPlayer = null;
    
    public override void _Ready()
    {
        base._Ready();

        // Subscribe to PlayerSensed events on the next frame in case it's not fully set up yet.
        Callable.From(() =>
        {
            OwnerNpc.NearbyBodySensor.PlayerSensed += OnPlayerSensed;
        }).CallDeferred();
    }
    
    public override void Think(double deltaTime)
    {
        // Nothing yet.
    }

    protected override float GetLookAtAngle()
    {
        // Look at the nearby player if present.
        if (IsInstanceValid(trackedPlayer))
        {
            return (trackedPlayer.GlobalPosition - OwnerNpc.GlobalPosition).Angle();
        }
        // Look "down" toward the bottom of the screen by default.
        return Vector2.Down.Angle();
    }

    private void OnPlayerSensed(Player player, bool wasSensed)
    {
        if (wasSensed)
        {
            if (trackedPlayer == null)
            {
                trackedPlayer = player;
            }
            // ignore otherwise.
        }
        else
        {
            if (trackedPlayer == player)
            {
                // remove
                trackedPlayer = null;
                // replace (if possible)
                if (OwnerNpc.NearbyBodySensor.Players.Count > 0)
                {
                    trackedPlayer = OwnerNpc.NearbyBodySensor.Players.OrderBy(
                            p => p.GlobalPosition.DistanceSquaredTo(OwnerNpc.GlobalPosition))
                            .First();
                }
            }
        }
    }

    protected void OnPlayerInteracted(InteractionArea area, Character character)
    {
        GD.Print("CONFUSED YELLING");
    }
    
}
