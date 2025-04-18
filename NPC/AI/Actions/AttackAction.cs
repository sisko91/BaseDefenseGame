using Godot;
using System;

namespace AI
{
    namespace Actions {
        public partial class AttackAction : AI.Action
        {
            [ExportCategory("Attack")]
            // Maximum range that this attack may be used from. This range is tested from the centerpoint of both the attacker and enemy target bodies.
            [Export]
            public float MaxAttackRange { get; protected set; } = 200.0f;

            // How long in seconds the attack is prepared before executing. This duration timer starts the moment this action is activated.
            [Export]
            public float AttackPrepareDuration = 0.5f;

            // How long in seconds the attack takes to execute. This duration timer starts the moment the PrepareAttack() timer completes.
            [Export]
            public float AttackExecuteDuration = 0.8f;

            // How often in seconds the attack action must wait before it is allowed to score > 0. The reset timer for cooldowns starts
            // the moment ExecuteAttack() completes.
            [Export]
            public float AttackCooldownDuration = 4.0f;

            private double lastAttackTime = -1;

            public override float CalculateScore() {
                if (Brain.EnemyTarget == null) {
                    // No target.
                    return 0;
                }
                if(!IsOffCooldown()) {
                    // Premature.
                    return 0;
                }
                if (!IsInRangeOf(Brain.EnemyTarget)) {
                    // Out of range.
                    return 0;
                }
                // Fine.
                return 1.0f;
            }

            public bool IsInRangeOf(Character targetNode) {
                return Owner.CurrentElevationLevel == targetNode.CurrentElevationLevel && targetNode.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) <= (MaxAttackRange * MaxAttackRange);
            }

            public bool IsOffCooldown() {
                return (lastAttackTime <= 0 || GetTimeSeconds() - AttackCooldownDuration > lastAttackTime);
            }
            protected double GetTimeSeconds() {
                return Time.GetTicksUsec() / 1000000.0;
            }
            protected override void OnActivate() {
                //GD.Print($"{Owner?.Name}-> [AttackAction] Activated (Target: {Brain.EnemyTarget.Name})");
                PrepareAttack();
                var prepareTimer = Owner.GetTree().CreateTimer(AttackPrepareDuration, processAlways: false);
                prepareTimer.Timeout += () => {
                    // The owner may have died while the timer for this was scheduled.
                    if (IsInstanceValid(Owner)) {
                        ExecuteAttack();
                        // Schedule the ExecuteAttack() timer.
                        var executeTimer = Owner.GetTree().CreateTimer(AttackExecuteDuration, processAlways: false);
                        executeTimer.Timeout += () => {
                            if(IsInstanceValid(Owner)) {
                                lastAttackTime = GetTimeSeconds();
                                Deactivate();
                            }
                        };
                    }
                };
            }

            // PrepareAttack runs the moment an attack is possible and the AttackAction activates. What happens during PrepareAttack() is
            // up to the implementation, but conventionally this is where wind-ups occur or when telegraph visuals are spawned.
            protected virtual void PrepareAttack() {
                // TODO: Support a signal here so that we can wire up simple attacks in the editor without C#
            }

            // ExecuteAttack runs immediately after PrepareAttack() returns. Like PrepareAttack(), what this does is left up to the
            // subclass implementation to decide. Conventionally this is where projectiles would be spawned, clubs would be swung, etc.
            protected virtual void ExecuteAttack() {
                // TODO: Support a signal here so that we can wire up simple attacks in the editor without C#
            }
        }
    }
}
