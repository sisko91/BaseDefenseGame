using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ClusterGrenade : Grenade
{
    [Export]
    public int ClusterSize = 5;
    protected override async void OnLifetimeExpired() {
        await SpawnExplosions();
    }

    private async Task SpawnExplosions() {
        //Disable all collisions, we dont want other explosions interacting with this grenade anymore
        CollisionLayer = 0;
        SpawnExplosion(); //Initial explosion

        //Delay each cluster explosion by a small amount
        List<SceneTreeTimer> clusterTimers = new List<SceneTreeTimer>();
        for (int i = 0; i < ClusterSize; i++) {
            var timer = GetTree().CreateTimer(new Random().NextDouble() * 0.5, false);
            timer.Timeout += () => SpawnExplosion(true);
            clusterTimers.Add(timer);
        }

        foreach (var timer in clusterTimers) {
            if (timer.TimeLeft > 0) {
                await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            }
        }

        QueueFree();
    }

    private void SpawnExplosion(bool spread=false) {
        var explosion = ExplosionTemplate?.Instantiate<Explosion>();
        Vector2 position = GlobalPosition;
        if (spread) {
            double distance = new Random().NextDouble() * explosion.MaximumRadius;
            double angle = new Random().NextDouble() * 2 * Math.PI;
            position = GlobalPosition + Vector2.Right.Rotated((float)angle) * (float)distance;
        }

        // Communicate the original instigator, so that characters receiving damage know who did it
        explosion.Instigator = Instigator;

        //Ignore collisions until the explosion is placed in the desired position, otherwise it can trigger area entered events twice
        var collisionLayer = explosion.CollisionLayer << CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
        explosion.CollisionLayer = 0;
        explosion.CollisionMask = explosion.CollisionMask << CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;

        explosion.Visible = Visible;
        GetParent().AddChild(explosion);
        explosion.GlobalPosition = position;
        explosion.CollisionLayer = collisionLayer;
    }
}
