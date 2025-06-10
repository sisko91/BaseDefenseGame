using ExtensionMethods;
using Godot;
using System;

namespace AI
{
    namespace Actions {
        [GlobalClass]
        public partial class RangedAttackAction : AI.Actions.AttackAction
        {
            [ExportCategory("Attack")]
            // The scene to instantiate for each attack.
            [Export]
            public PackedScene AttackInstanceTemplate { get; set; } = null;

            // Local position offset for the attack instance spawned from the template. Offset is relative to the AI Owner.
            [Export]
            public Vector2 AttackInstanceSpawnOffset { get; set; } = Vector2.Zero;

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

            public RangedAttackAction() : base() {
                PausesMotionWhileActive = true;
            }

            public override void Initialize(ActionOrientedBrain brain) {
                base.Initialize(brain);

                if(IncludeTraceTest) {
                    // Spawn the raycaster, this is used to check that line-of-sight exists before applying blast damage.
                    Raycaster = new RayCast2D();
                    Raycaster.CollisionMask = TraceCollisionMask;
                    Raycaster.Enabled = false; // We only want to use this on-demand so disable otherwise.
                    OwnerNpc.AddChild(Raycaster);
                }
            }

            public override float CalculateScore() {
                float baseScore = base.CalculateScore();
                // The base AttackAction will score 0 if we can't use the attack right now (on cooldown, out of range, etc.)
                if(baseScore <= 0) {
                    return baseScore;
                }
                if (!IsAimedAtTarget(Brain.EnemyTarget)) {
                    return 0;
                }
                if (IncludeTraceTest && !HasTraceLOS(Brain.EnemyTarget)) {
                    return 0;
                }

                // HACK: Returning > 1.25 so that it will score higher than other attacks e.g. Melee attacks whenever this is ready.
                return 1.5f;
            }

            protected bool IsAimedAtTarget(Node2D targetNode) {
                var lookVec = Vector2.FromAngle(OwnerNpc.GlobalRotation);
                var dirVec = (targetNode.GlobalPosition - OwnerNpc.GlobalPosition).Normalized();
                var dot = lookVec.Dot(dirVec);
                if (dot > AimTolerance) {
                    return true;
                }
                return false;
            }
            protected bool HasTraceLOS(Node2D targetNode) {
                // TODO: Do this for real.
                return true;
            }

            protected override void PrepareAttack() {
                // Do nothing during prepare by default. Children can override this.
                GD.Print($"{OwnerNpc?.Name}-> [Prepare(Ranged)] Attacking enemy ({Brain.EnemyTarget.Name})");
            }

            // HACK:
            private Node2D rangedAttackInstance = null;
            protected override void ExecuteAttack() {
                rangedAttackInstance = AttackInstanceTemplate?.Instantiate<Node2D>();
                if (rangedAttackInstance is IInstigated instigated) {
                    instigated.Instigator = OwnerNpc;
                }
    
                if (rangedAttackInstance is CollisionObject2D c) {
                    c.CollisionLayer = c.CollisionLayer << OwnerNpc.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
                    c.CollisionMask = c.CollisionMask << OwnerNpc.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR;
                }

                var parent = OwnerNpc.CurrentRegion?.Foreground ?? OwnerNpc.GetGameWorld().Foreground;
                parent.AddChild(rangedAttackInstance);

                rangedAttackInstance.GlobalPosition = OwnerNpc.GlobalPosition + AttackInstanceSpawnOffset.Rotated(OwnerNpc.GlobalRotation);
                rangedAttackInstance.GlobalRotation = OwnerNpc.GlobalRotation;
            }

            protected override void OnDeactivate() {
                rangedAttackInstance.QueueFree();
            }
        }

    }
}