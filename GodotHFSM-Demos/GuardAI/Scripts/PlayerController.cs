using Godot;

namespace GodotHFSM.Samples.GuardAI
{
    public partial class PlayerController : CharacterBody2D
    {
        [Export] public float Speed { get; set; } = 400;

        public override void _PhysicsProcess(double delta)
        {
            Velocity = GetInputDirection() * Speed;
            MoveAndSlide();
        }

        private static Vector2 GetInputDirection()
        {
            return Input.GetVector("left", "right", "up", "down");
        }
    }
}
