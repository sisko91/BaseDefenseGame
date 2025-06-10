using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI
{
    namespace Actions
    {
        [GlobalClass]
        public partial class MoveToTargetAction : AI.Action
        {
            Dictionary<Tuple<BuildingRegion, BuildingRegion>, List<Stairs>> StairsPathCache;

            private Character originalEnemy = null;

            public override void Initialize(ActionOrientedBrain brain) {
                base.Initialize(brain);
                StairsPathCache = new Dictionary<Tuple<BuildingRegion, BuildingRegion>, List<Stairs>>();
            }
            public override float CalculateScore()
            {
                if(Brain.EnemyTarget == null)
                {
                    return 0;
                }

                float targetDistance = GetAcceptableTargetDistance();
                float distSq = OwnerNpc.GlobalPosition.DistanceSquaredTo(Brain.EnemyTarget.GlobalPosition);
                if (OwnerNpc.CurrentElevationLevel == Brain.EnemyTarget.CurrentElevationLevel && distSq < targetDistance*targetDistance)
                {
                    // Too close, nothing to do.
                    return 0;
                }

                return 1;
            }

            private float GetAcceptableTargetDistance()
            {
                // The NavAgent will never get closer than the combined radii of the NPC and its target enemy.
                return (OwnerNpc.GetCollisionBodyRadius() + Brain.EnemyTarget.GetCollisionBodyRadius() + 10);
            }

            protected override void OnActivate()
            {
                GD.Print($"{OwnerNpc?.Name}->Navigating to enemy ({Brain.EnemyTarget.Name})");
                OwnerNpc.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
                OwnerNpc.NavAgent.TargetDesiredDistance = GetAcceptableTargetDistance();
                // TODO: Maybe just subscribe to something like an EnemyTargetChanged event?
                originalEnemy = Brain.EnemyTarget;
            }

            protected override void OnDeactivate()
            {
                //GD.Print($"{Owner?.Name}->{GetType().Name} deactivated :c");
                Brain.ClearNavigationTarget();
                originalEnemy = null;
            }

            public override void Update(double deltaTime)
            {
                if(Brain.EnemyTarget == null)
                {
                    //GD.Print($"{Owner?.Name}->{GetType().Name} no enemy target");
                    Deactivate();
                    return;
                }

                // Abort any time the target changes because that probably warrants a reconsideration.
                if (Brain.EnemyTarget != originalEnemy) {
                    //GD.Print($"{Owner?.Name}->{GetType().Name} target has changed");
                    Deactivate();
                    return;
                }

                if (OwnerNpc.NavAgent.IsNavigationFinished())
                {
                    //GD.Print($"{Owner?.Name}->{GetType().Name} nav finished");
                    Deactivate();
                    return;
                }

                if (Brain.EnemyTarget is Player player)
                {
                    bool isPlayerOutside = player.CurrentRegion == null || !player.CurrentRegion.OverlapsBody(player);
                    bool isOwnerOutside = OwnerNpc.CurrentRegion == null || !OwnerNpc.CurrentRegion.OverlapsBody(OwnerNpc);
                    bool isOwnerOnGroundFloor = !isOwnerOutside && OwnerNpc.CurrentRegion.HasExit;
                    bool isPlayerInDifferentBuilding = !isPlayerOutside && !isOwnerOutside && player.CurrentRegion.OwningBuilding != OwnerNpc.CurrentRegion.OwningBuilding;

                    //If in the same region, path directly to the player
                    if (OwnerNpc.CurrentRegion == player.CurrentRegion || isPlayerOutside && isOwnerOutside)
                    {
                        //GD.Print("Pathing directly to player");
                        OwnerNpc.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
                        OwnerNpc.NavAgent.TargetDesiredDistance = GetAcceptableTargetDistance();
                        return;
                    }
                    
                    //If the player is not in this building and there's a door, path outside the door
                    if (isOwnerOnGroundFloor && (isPlayerOutside || isPlayerInDifferentBuilding)) {
                        //GD.Print("I'm inside pathing to outside the door");
                        var door = OwnerNpc.CurrentRegion.OwningBuilding.Exits[0];
                        var doorOffset = new Vector2(0, 30).Rotated(door.Rotation);


                        if (InInteractRange(OwnerNpc.GlobalPosition, door.GlobalPosition) && !door.Open && Brain.CanOpenDoors) {
                            OwnerNpc.InteractWithNearestObject();
                        }

                        OwnerNpc.NavAgent.TargetPosition = door.GlobalPosition - door.Position + doorOffset;
                        OwnerNpc.NavAgent.TargetDesiredDistance = 0;
                        return;
                    }

                    //If I'm outside, path inside the door to the player's building
                    if (isOwnerOutside) {
                        //GD.Print("I'm outside pathing to inside the door");
                        var door = player.CurrentRegion.OwningBuilding.Exits[0];
                        var doorOffset = new Vector2(0, 30).Rotated(door.Rotation);
                        if (InInteractRange(OwnerNpc.GlobalPosition, door.GlobalPosition) && !door.Open && Brain.CanOpenDoors) {
                            OwnerNpc.InteractWithNearestObject();
                        }

                        OwnerNpc.NavAgent.TargetPosition = door.GlobalPosition - door.Position - doorOffset;
                        OwnerNpc.NavAgent.TargetDesiredDistance = 0;
                        return;
                    }

                    //Player is outside or in another building, try to path to an exit
                    Stairs stairsToPathTo = null;
                    if (isPlayerOutside || isPlayerInDifferentBuilding)
                    {
                        List<Stairs> path = GetStairsPath(OwnerNpc.CurrentRegion);
                        foreach (Stairs stairs in path) {
                            if (stairs.TargetStairs.OwningRegion == OwnerNpc.CurrentRegion) {
                                stairsToPathTo = stairs;
                                break;
                            }
                        }
                    }
                    //Owner and player are in the same building, walk the stairs
                    else
                    {
                        List<Stairs> path = GetStairsPath(player.CurrentRegion, OwnerNpc.CurrentRegion);
                        foreach (Stairs stairs in path) {
                            if (stairs.OwningRegion == OwnerNpc.CurrentRegion) {
                                stairsToPathTo = stairs;
                                break;
                            }
                        }
                    }

                    if (stairsToPathTo != null) {
                        //If already on top of the stairs, take them
                        if (InInteractRange(OwnerNpc.GlobalPosition, stairsToPathTo.GlobalPosition)) { //TODO: get character radius
                            OwnerNpc.InteractWithNearestObject();
                        }

                        //GD.Print("Pathing to stairs");
                        OwnerNpc.NavAgent.TargetPosition = stairsToPathTo.GlobalPosition;
                        OwnerNpc.NavAgent.TargetDesiredDistance = 0;
                    }
                    else {
                        //No path
                        //GD.Print($"{Owner?.Name}->{GetType().Name} no path");
                        Deactivate();
                    }
                } else {
                    //GD.Print("Pathing straight to target position");
                    OwnerNpc.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
                    OwnerNpc.NavAgent.TargetDesiredDistance = GetAcceptableTargetDistance();
                }
            }

            //Returns a set of stairs to walk from the entrance of this building, or a start region, to get to this region
            private List<Stairs> GetStairsPath(BuildingRegion targetRegion, BuildingRegion startRegion = null) {
                var key = Tuple.Create(targetRegion, startRegion);
                if (StairsPathCache.ContainsKey(key)) {
                    return StairsPathCache[key];
                }

                var result = GetStairsPathImpl(targetRegion, startRegion);
                StairsPathCache[key] = result;

                return result;
            }
            private List<Stairs> GetStairsPathImpl(BuildingRegion targetRegion, BuildingRegion startRegion = null) {
                Queue<List<Stairs>> queue = new Queue<List<Stairs>>();
                HashSet<BuildingRegion> visited = new HashSet<BuildingRegion>();

                foreach (Stairs stairs in targetRegion.Stairs) {
                    queue.Enqueue(new List<Stairs> { stairs.TargetStairs });
                }
                visited.Add(targetRegion);

                while (queue.Count > 0) {
                    List<Stairs> path = queue.Dequeue();
                    Stairs last = path[path.Count - 1];
                    if (startRegion != null) {
                        if (startRegion == last.OwningRegion) {
                            return path;
                        }
                    }
                    else if (last.OwningRegion.HasExit) {
                        return path;
                    }

                    visited.Add(last.OwningRegion);
                    foreach (Stairs stairs in last.OwningRegion.Stairs) {
                        if (startRegion != null) {
                            if (startRegion == stairs.TargetStairs.OwningRegion) {
                                path.Add(stairs.TargetStairs);
                                return path;
                            }
                        }
                        else if (stairs.TargetStairs.OwningRegion == null || stairs.TargetStairs.OwningRegion.HasExit) {
                            path.Add(stairs.TargetStairs);
                            return path;
                        }
                        else if (!visited.Contains(stairs.TargetStairs.OwningRegion)) {
                            visited.Add(stairs.TargetStairs.OwningRegion);
                            var newPath = new List<Stairs>(path);
                            newPath.Append(stairs.TargetStairs);
                            queue.Enqueue(newPath);
                        }
                    }
                }

                return new List<Stairs>();
            }

            private bool InInteractRange(Vector2 origin, Vector2 target) {
                return origin.DistanceTo(target) < 10;
            }

            protected override bool CanInterrupt() {
                return true;
            }
        }
    }
}

