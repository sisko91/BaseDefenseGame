using Godot;
using System;

namespace AI
{
    namespace Actions
    {
        public partial class AttackEnemyTargetAction : AI.Action
        {
            // TODO: Placeholder
            public static float MeleeAttackDamage = 30.0f;

            // TODO: Placeholder
            public static float MeleeAttackRange = 70.0f;

            // TODO: Placeholder
            public static float MeleeAttackCooldown = 1.0f;

            private double lastAttackTime = -1;

            public AttackEnemyTargetAction() : base()
            {
                // This action halts movement for the NPC while they attack.
                PausesMotionWhileActive = true;
            }

            public override void Initialize(Brain brain)
            {
                base.Initialize(brain);
            }

            public override float CalculateScore()
            {
                if(Brain.EnemyTarget == null)
                {
                    return 0;
                }

                if(Brain.EnemyTarget.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) > (MeleeAttackRange*MeleeAttackRange))
                {
                    // Out of range.
                    return 0;
                }

                if (lastAttackTime > 0 && GetTimeSeconds() - MeleeAttackCooldown < lastAttackTime)
                {
                    // Premature
                    return 0;
                }

                // HACK: Returning > 1 so that this will score higher than MoveToTarget for now. Need to add configurable weights.
                return 1.25f;
            }

            protected override void OnActivate()
            {
                GD.Print($"{Owner?.Name}->Attacking enemy ({Brain.EnemyTarget.Name})");
            }

            public override void Update(double deltaTime)
            {
                if(Brain.EnemyTarget == null)
                {
                    Deactivate();
                    return;
                }

                if (Brain.EnemyTarget.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) > (MeleeAttackRange * MeleeAttackRange))
                {
                    // Out of range.
                    Deactivate();
                    return;
                }

                if (lastAttackTime > 0 && GetTimeSeconds() - MeleeAttackCooldown < lastAttackTime)
                {
                    // Not time to attack again yet.
                    return;
                }

                // TODO: We should be implementing a melee weapon that they use that does this correctly (for some definition of correct).
                KinematicCollision2D impact = null;
                Brain.EnemyTarget.ReceiveHit(impact, MeleeAttackDamage, Owner);
                GD.Print("Hiyah!");
                lastAttackTime = GetTimeSeconds();
            }

            private double GetTimeSeconds()
            {
                return Time.GetTicksUsec() / 1000000.0;
            }
        }
    }
}

