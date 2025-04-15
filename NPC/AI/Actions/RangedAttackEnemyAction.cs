using Godot;
using System;

namespace AI
{
    namespace Actions {
        [GlobalClass]
        public partial class RangedAttackEnemyAction : AI.Action
        {
            [ExportCategory("Attack")]
            // The scene to instantiate for each attack.
            [Export]
            public PackedScene AttackInstanceTemplate { get; set; } = null;

            // Local position offset for the attack instance spawned from the template. Offset is relative to the AI Owner.
            [Export]
            public Vector2 AttackInstanceSpawnOffset { get; set; } = Vector2.Zero;

            // How often this attack is allowed to score > 0.
            [Export]
            public float AttackCooldownSeconds = 4.0f;

            // Maximum range that this attack may be used from. This range is tested from the centerpoint of both the attacker and enemy target bodies.
            [Export]
            public float MaxAttackRange { get; protected set; } = 200.0f;

            // How long this attack takes to perform. After this duration has elapsed the cooldown timer starts (i.e. total time between attacks is AttackCooldownSeconds + AttackTimeSeconds).
            [Export]
            public float AttackTimeSeconds = 0.8f;

            [ExportCategory("Scoring")]

            // Determines how strongly the action needs to be aligned with / aimed at the target enemy in order for the aim test to pass.
            // An AimTolerance of -1 means that the target must be *exactly* behind the NPC in order to attack. An AimTolerance of 1 means *exactly* in front of.
            // An AimTolerance of 0 means *exactly* perpendicular to (the left/right of) the NPC. Generally speaking a value of 0.9 means "you must be ~90% aimed at the target". 
            [Export]
            public float AimTolerance = 0.9f;

            // Whether the attack requires a line-of-sight test to pass before scoring > 0.
            [Export]
            public bool IncludeTraceTest = true;

            // CollisionMask applied to Line-of-Sight traces performed by this action when scoring targets.
            [Export(PropertyHint.Layers2DPhysics)]
            public uint TraceCollisionMask { get; set; } = 0;

            // The raycaster node spawned for this attack, used to check line-of-sight for scoring on targets behind walls and other obstructions.
            protected RayCast2D Raycaster { get; private set; }

            private double lastAttackTime = -1;

            public RangedAttackEnemyAction() : base() {
                PausesMotionWhileActive = true;
            }

            public override void Initialize(Brain brain) {
                base.Initialize(brain);

                if(IncludeTraceTest) {
                    // Spawn the raycaster, this is used to check that line-of-sight exists before applying blast damage.
                    Raycaster = new RayCast2D();
                    Raycaster.CollisionMask = TraceCollisionMask;
                    Raycaster.Enabled = false; // We only want to use this on-demand so disable otherwise.
                    Owner.AddChild(Raycaster);
                }
            }

            public override float CalculateScore() {
                if(Brain.EnemyTarget == null) {
                    return 0;
                }
                if(!CanAttackNow(Brain.EnemyTarget)) {
                    return 0;
                }
                if(!IsAimedAtTarget(Brain.EnemyTarget)) {
                    return 0;
                }
                if (IncludeTraceTest && !HasTraceLOS(Brain.EnemyTarget)) {
                    return 0;
                }

                // HACK: Returning > 1.25 so that it will score higher than other attacks e.g. Melee attacks whenever this is ready.
                return 1.5f;
            }

            public bool CanAttackNow(Node2D targetNode) {
                return IsOffCooldown() && IsInAttackRange(targetNode);
            }

            protected bool IsInAttackRange(Node2D targetNode) {
                return targetNode.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) <= (MaxAttackRange * MaxAttackRange);
            }

            protected bool IsOffCooldown() {
                return (lastAttackTime <= 0 || GetTimeSeconds() - AttackCooldownSeconds > lastAttackTime);
            }

            protected bool IsAimedAtTarget(Node2D targetNode) {
                var lookVec = Vector2.FromAngle(Owner.GlobalRotation);
                var dirVec = (targetNode.GlobalPosition - Owner.GlobalPosition).Normalized();
                var dot = lookVec.Dot(dirVec);
                if (dot > AimTolerance) {
                    return true;
                }
                return false;
            }

            protected double GetTimeSeconds() {
                return Time.GetTicksUsec() / 1000000.0;
            }

            protected bool HasTraceLOS(Node2D targetNode) {
                return true;
            }

            protected override void OnActivate() {
                GD.Print($"{Owner?.Name}-> [RANGED] Attacking enemy ({Brain.EnemyTarget.Name})");
                var rangedAttackInstance = AttackInstanceTemplate?.Instantiate<Node2D>();
                Owner.AddChild(rangedAttackInstance);
                rangedAttackInstance.Position = AttackInstanceSpawnOffset;

                var attackTimer = Owner.GetTree().CreateTimer(AttackTimeSeconds, processAlways: false);
                attackTimer.Timeout += () => {
                    Owner.RemoveChild(rangedAttackInstance);
                    lastAttackTime = GetTimeSeconds();
                    Deactivate();
                };
            }
        }

    }
}