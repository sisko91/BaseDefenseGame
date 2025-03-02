using Godot;
using System;

public partial class FragGrenade : CharacterBody2D
{
    public int Speed = 250;
    public float LifetimeSeconds = 3f;

    private bool exploding = false;
    private GradientTexture2D texture;
    public void Start(Vector2 position, float direction)
    {
        Rotation = direction;
        Position = position;
        Velocity = new Vector2(Speed, 0).Rotated(Rotation);
    }

    public void Explode()
    {
        if (exploding)
        {
            return;
        }

        exploding = true;
        var timer = GetTree().CreateTimer(LifetimeSeconds);
        timer.Timeout += QueueFree;
        Velocity = Vector2.Zero;
    }
        
    public override void _Ready()
    {
        var sprite = GetNode<Sprite2D>("Sprite2D");
        texture = (GradientTexture2D) sprite.Texture.Duplicate(true);
        sprite.Texture = texture;
    }

    public override void _Process(double delta)
    {
        //Grow in size while exploding
        if (exploding)
        {
            //Only scale the sprite since collision shapes cant scale
            var sprite = GetNode<Sprite2D>("Sprite2D");
            if (sprite.Scale.X > 10) {
                QueueFree();
            }
            sprite.Scale *= Vector2.One * 1.05f;
            texture.Gradient.SetColor(1, new Color(255, 0, 0));
        }   
    }

    public override void _PhysicsProcess(double delta)
    {
        var collision = MoveAndCollide(Velocity * (float)delta);
        if (collision != null)
        {
            //Make grenades bounce off walls
            Velocity = Velocity.Bounce(collision.GetNormal());

            //TODO: Handle collisions
            if (collision.GetCollider() is Player || collision.GetCollider() is NonPlayerCharacter npc)
            {
                Explode();
            }
        }
    }
}