using Godot;

namespace AI
{
    // Actions are scored by the AI Brain and activated to issue commands to NonPlayerCharacters.
    [GlobalClass]
    public partial class Action : Resource
    {
        // The Brain controlling the NPC.
        public Brain Brain { get; private set; }

        // The NPC owning this AI action.
        public NonPlayerCharacter Owner
        {
            get
            {
                return Brain?.Owner;
            }
        }

        public bool IsActive { get; private set; }

        // If true, this action pauses the brain's updates to velocity when activated, until deactivated.
        [ExportCategory("Action")]
        [Export]
        public bool PausesMotionWhileActive { get; protected set; } = false;

        public Action() : base() {
            // All actions must be LocalToScene because they are typically assigned via the editor and are instanced 1x per Brain.
            ResourceLocalToScene = true;
        }

        public virtual void Initialize(Brain brain)
        {
            Brain = brain;
        }

        // Scores the action for the current state of the owning Brain and NPC.
        public virtual float CalculateScore()
        {
            return 0;
        }

        public void Activate()
        {
            IsActive = true;
            OnActivate();
        }

        protected virtual void OnActivate()
        {

        }

        public void Deactivate()
        {
            IsActive = false;
            OnDeactivate();
        }

        protected virtual void OnDeactivate()
        {

        }

        // TryInterrupt attempts to interrupt an action that is currently active. The action gets to decide if it is ultimately interruptible or not.
        // Actions are un-interruptible by default. Override protected bool CanInterrupt() to change this.
        public virtual bool TryInterrupt() {
            bool success = CanInterrupt();
            GD.Print($"{GetType()} {(success ? "Interrupted" : "Refused Interrupt")}");
            if(success) {
                Deactivate();
            }
            return success;
        }

        protected virtual bool CanInterrupt() {
            return false;
        }

        // Updates this Action and the AI associated with it.
        public virtual void Update(double deltaTime)
        {

        }
    }
}
