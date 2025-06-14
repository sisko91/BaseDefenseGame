using Godot;
using System;

namespace AI
{
    namespace Actions
    {
        [GlobalClass]
        public partial class MeleeAttackAction : AI.Actions.AttackAction
        {
            // TODO: Placeholder
            public static float MeleeAttackDamage = 30.0f;

            public MeleeAttackAction() : base()
            {
                // This action halts movement for the NPC while they attack.
                PausesMotionWhileActive = true;
            }

            public override void Initialize(ActionOrientedBrain brain)
            {
                base.Initialize(brain);
            }

            public override float CalculateScore()
            {
                var baseScore = base.CalculateScore();
                if(baseScore <= 0) {
                    return baseScore;
                }
                float meleeAttackRange = GetMeleeAttackRange();
                if (Brain.EnemyTarget.CurrentElevationLevel != OwnerNpc.CurrentElevationLevel || Brain.EnemyTarget.GlobalPosition.DistanceSquaredTo(OwnerNpc.GlobalPosition) > (meleeAttackRange * meleeAttackRange)) {
                    // Out of range.
                    // TODO: Move this into AttackAction?
                    return 0;
                }

                // HACK: Returning > 1 so that this will score higher than MoveToTarget for now. Need to add configurable weights.
                return 1.25f;
            }

            protected override void PrepareAttack()
            {
                GD.Print($"{OwnerNpc?.Name}-> [Prepare(Melee)] Attacking enemy ({Brain.EnemyTarget.Name})");
            }

            protected override void ExecuteAttack() {
                // TODO: We should be implementing a melee weapon that they use that does this correctly (for some definition of correct).
                var hr = new HitResult();
                // Impact location is the midpoint between the two characters meleeing. The normal points from attacker -> target.
                hr.ImpactLocation = (OwnerNpc.GlobalPosition + Brain.EnemyTarget.GlobalPosition) / 2;
                hr.ImpactNormal = (Brain.EnemyTarget.GlobalPosition - OwnerNpc.GlobalPosition);
                OwnerNpc.TryRegisterImpact(Brain.EnemyTarget, hr, MeleeAttackDamage);
                GD.Print("Hiyah!");
            }

            private float GetMeleeAttackRange()
            {
                return Brain.EnemyTarget.GetCollisionBodyRadius() + OwnerNpc.GetCollisionBodyRadius() + 10;
            }
        }
    }
}

