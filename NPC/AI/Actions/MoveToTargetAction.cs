using Godot;
using System;

namespace AI
{
    namespace Actions
    {
        public partial class MoveToTargetAction : AI.Action
        {
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
                GD.Print($"{Owner?.Name}->{GetType().Name} activated :D");

                Owner.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
            }

            protected override void OnDeactivate()
            {
                GD.Print($"{Owner?.Name}->{GetType().Name} deactivated :c");
            }

            public override void Update(double deltaTime)
            {
                if(Brain.EnemyTarget == null)
                {
                    GD.Print($"{Owner?.Name}->{GetType().Name} no enemy target");
                    Deactivate();
                    return;
                }

                if(Owner.NavAgent.IsNavigationFinished())
                {
                    GD.Print($"{Owner?.Name}->{GetType().Name} nav finished");
                    Deactivate();
                    return;
                }

                if (Brain.EnemyTarget is Player player)
                {
                    //If the player is on a different floor, path to the stairs
                    //For multiple houses/floors, this code needs to be updated detect what building a region is in and path through multiple sets of stairs, but this is proof of concept for the current demo
                    if (Owner.CurrentRegion != player.CurrentRegion && player.CurrentRegion != null)
                    {
                        Owner.NavAgent.TargetPosition = player.CurrentRegion.Stairs[0].TargetStairs.GlobalPosition;
                        return;
                    }
                }
                Owner.NavAgent.TargetPosition = Brain.EnemyTarget.GlobalPosition;
            }
        }
    }
}

