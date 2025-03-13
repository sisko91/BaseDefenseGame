using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI
{
    namespace Actions
    {
        public partial class MoveToTargetAction : AI.Action
        {
            Dictionary<Tuple<BuildingRegion, BuildingRegion>, List<Stairs>> StairsPathCache;

            public override void Initialize(Brain brain) {
                base.Initialize(brain);
                StairsPathCache = new Dictionary<Tuple<BuildingRegion, BuildingRegion>, List<Stairs>>();
            }
            public override float CalculateScore()
            {
                if(Brain.EnemyTarget == null)
                {
                    return 0;
                }

                if(Owner.NavAgent.TargetPosition.IsEqualApprox(Brain.EnemyTarget.GlobalPosition))
                {
                    if(Owner.NavAgent.IsNavigationFinished())
                    {
                        return 0;
                    }
                }

                return 1;
            }

            protected override void OnActivate()
            {
                GD.Print($"{Owner?.Name}->Navigating to enemy ({Brain.EnemyTarget.Name})");
                Owner.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
                // The NavAgent will never get closer than the combined radii of the NPC and its target enemy.
                Owner.NavAgent.TargetDesiredDistance = (Owner.NavAgent.Radius + Brain.EnemyTarget.GetCollisionBodyRadius());
            }

            protected override void OnDeactivate()
            {
                //GD.Print($"{Owner?.Name}->{GetType().Name} deactivated :c");
                Brain.ClearNavigationTarget();
            }

            public override void Update(double deltaTime)
            {
                if(Brain.EnemyTarget == null)
                {
                    //GD.Print($"{Owner?.Name}->{GetType().Name} no enemy target");
                    Deactivate();
                    return;
                }

                if(Owner.NavAgent.IsNavigationFinished())
                {
                    //GD.Print($"{Owner?.Name}->{GetType().Name} nav finished");
                    Deactivate();
                    return;
                }

                if (Brain.EnemyTarget is Player player)
                {
                    bool isPlayerOutside = player.CurrentRegion == null;
                    bool isOwnerOutside = Owner.CurrentRegion == null;
                    bool isOwnerOnGroundFloor = !isOwnerOutside && Owner.CurrentRegion.HasExit;
                    bool isPlayerInDifferentBuilding = !isPlayerOutside && !isOwnerOutside && player.CurrentRegion.OwningBuilding != Owner.CurrentRegion.OwningBuilding;

                    //If in the same region, path directly to the player
                    if (Owner.CurrentRegion == player.CurrentRegion)
                    {
                        Owner.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
                        return;
                    }
                    
                    //If the player is outside and there's a door, path outside the door
                    if (isPlayerOutside && isOwnerOnGroundFloor) {
                        var door = Owner.CurrentRegion.OwningBuilding.Exits[0];
                        var doorOffset = new Vector2(0, 30).Rotated(door.Rotation);
                        if (InInteractRange(Owner.GlobalPosition, door.GlobalPosition - doorOffset) && !door.Open && Brain.CanOpenDoors) {
                            Owner.InteractWithNearestObject();
                        }
                        else {
                            Owner.NavAgent.TargetPosition = door.GlobalPosition + doorOffset;
                        }
                        return;
                    }
                    //If I'm outside, path inside the door to the player's building
                    if (isOwnerOutside) {
                        var door = player.CurrentRegion.OwningBuilding.Exits[0];
                        var doorOffset = new Vector2(0, 30).Rotated(door.Rotation);
                        if (InInteractRange(Owner.GlobalPosition, door.GlobalPosition + doorOffset) && !door.Open && Brain.CanOpenDoors) {
                            Owner.InteractWithNearestObject();
                        }
                        else {
                            Owner.NavAgent.TargetPosition = door.GlobalPosition - doorOffset;
                        }
                        return;
                    }

                    //Player is outside or in another building, try to path to an exit
                    Stairs stairsToPathTo = null;
                    if (isPlayerOutside || isPlayerInDifferentBuilding && !isOwnerOnGroundFloor)
                    {
                        List<Stairs> path = GetStairsPath(Owner.CurrentRegion);
                        foreach (Stairs stairs in path) {
                            if (stairs.TargetStairs.OwningRegion == Owner.CurrentRegion) {
                                stairsToPathTo = stairs;
                                break;
                            }
                        }
                    }
                    //Owner and player are in the same building, walk the stairs
                    else
                    {
                        List<Stairs> path = GetStairsPath(player.CurrentRegion, Owner.CurrentRegion);
                        foreach (Stairs stairs in path) {
                            if (stairs.OwningRegion == Owner.CurrentRegion) {
                                stairsToPathTo = stairs;
                                break;
                            }
                        }
                    }

                    if (stairsToPathTo != null) {
                        //If already on top of the stairs, take them
                        if (InInteractRange(Owner.GlobalPosition, stairsToPathTo.GlobalPosition)) { //TODO: get character radius
                            Owner.InteractWithNearestObject();
                        }
                        else {
                            Owner.NavAgent.TargetPosition = stairsToPathTo.GlobalPosition;
                        }
                    }
                    else {
                        //No path
                        Deactivate();
                    }
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
        }
    }
}

