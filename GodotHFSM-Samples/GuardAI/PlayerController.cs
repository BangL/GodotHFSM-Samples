namespace GodotHFSM.Samples.GuardAI;

using Godot;

public partial class PlayerController : CharacterBody2D {
    /// <summary>
    /// Declare the finite state machine
    /// </summary>
    private StateMachine _fsm;

    [Export] public float Speed { get; set; } = 200;

    public override void _Ready() {
        _fsm = new();

        _fsm.AddState("Idle", isGhostState: true);
        _fsm.SetStartState("Idle");

        _fsm.AddState("Moving",
            onLogic: (_, delta) => {
                Velocity = GetInputDirection() * Speed * (float)delta * 100f;
                MoveAndSlide();
            },
            canExit: _ => GetInputDirection().IsZeroApprox(),
            needsExitTime: true
        );
        _fsm.AddTransition("Idle", "Moving", _ => !GetInputDirection().IsZeroApprox());
        _fsm.AddTransition("Moving", "Idle");

        _fsm.Init();
    }

    public override void _PhysicsProcess(double delta) {
        _fsm.OnLogic(delta);
    }

    private static Vector2 GetInputDirection() {
        return Input.GetVector("left", "right", "up", "down");
    }
}
