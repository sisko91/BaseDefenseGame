using Godot;
using System;

//Common functionality for any moveable object that implements CharacterBody2D (like projectiles and characters)
public partial class Moveable : CharacterBody2D, IEntity {

    // The entity's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // entities enter their regions
    public int CurrentElevationLevel = 0;
    public BuildingRegion CurrentRegion { get; set; }

    public Moveable() : base() {
        //Better for 2d top-down
        MotionMode = MotionModeEnum.Floating;
    }
    public virtual void ChangeFloor(int targetFloor) {
        if (targetFloor == CurrentElevationLevel) {
            return;
        }

        int shift = targetFloor - CurrentElevationLevel;
        CurrentElevationLevel = targetFloor;
        if (shift > 0) {
            CollisionLayer = CollisionLayer << shift * CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask << shift * CollisionConfig.LAYERS_PER_FLOOR;
        } else if (shift < 0) {
            CollisionLayer = CollisionLayer >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
            CollisionMask = CollisionMask >> -shift * CollisionConfig.LAYERS_PER_FLOOR;
        }
    }

    public void SetInside(bool inside) {
        if (Material != null)
        {
            Material.Set("shader_parameter/is_inside", inside);
        }
    }
}
