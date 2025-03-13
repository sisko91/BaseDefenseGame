using Godot;

namespace AI
{
    // Actions are scored by the AI Brain and activated to issue commands to NonPlayerCharacters.
    public partial class Action : RefCounted
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
        public bool PausesMotionWhileActive { get; protected set; } = false;

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

        // Updates this Action and the AI associated with it.
        public virtual void Update(double deltaTime)
        {

        }
    }
}
