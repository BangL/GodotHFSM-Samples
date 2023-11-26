namespace GodotHFSM.Samples.GuardAI;

using System;
using System.Collections;
using Godot;
using HCoroutines;

public partial class GuardAI : CharacterBody2D {
    /// <summary>
    /// Declare the finite state machine
    /// </summary>
    private readonly StateMachine _fsm = new();

    /// <summary>
    /// Internal fields
    /// </summary>
    private int _patrolDirection = 1;
    private Node2D _player;
    private Vector2 _lastSeenPlayerPosition;
    private Vector2 _nextPosition;

    [Export] public float AttackRange { get; set; } = 60;
    [Export] public float AttackSpeed { get; set; } = 150;
    [Export] public float ChaseSpeed { get; set; } = 200;
    [Export] public Vector2[] PatrolPoints { get; set; } = Array.Empty<Vector2>();
    [Export] public float PatrolSpeed { get; set; } = 150;
    [Export] public float SearchRange { get; set; } = 150;
    [Export] public float SearchSpotRange { get; set; } = 350;
    /// <summary>
    /// in seconds
    /// </summary>
    [Export] public float SearchTime { get; set; } = 20;
    /// <summary>
    /// Parameters (can be changed in the inspector)
    /// [Export] public Animator animator;
    /// </summary>
    [Export] public Label StateDisplayText { get; set; }

    /// <summary>
    /// Helper methods (depend on how your scene has been set up)
    /// </summary>
    private Vector2? PlayerPosition => _player?.Position;
    private float? DistanceToPlayer => PlayerPosition?.DistanceTo(Position);

    public override void _Ready() {
        _nextPosition = Position;

        // Fight FSM
        HybridStateMachine fightFsm = new(
          beforeOnLogic: (_, delta) => {
              if (PlayerPosition.HasValue) {
                  MoveTowards(PlayerPosition.Value, AttackSpeed, delta, 40);
              }
          },
          needsExitTime: true
        );

        fightFsm.AddState("Wait" /*, onEnter: state => animator.Play("GuardIdle")*/);
        fightFsm.AddState("Telegraph" /*, onEnter: state => animator.Play("GuardTelegraph")*/);
        fightFsm.AddState("Hit",
          _ => {
              //animator.Play("GuardHit");
              // TODO: Cause damage to player if in range.
          }
        );

        // Because the exit transition should have the highest precedence,
        // it is added before the other transitions.
        fightFsm.AddExitTransition("Wait");

        fightFsm.AddTransition(new TransitionAfter("Wait", "Telegraph", 0.5f));
        fightFsm.AddTransition(new TransitionAfter("Telegraph", "Hit", 0.42f));
        fightFsm.AddTransition(new TransitionAfter("Hit", "Wait", 0.5f));

        // Root FSM
        _fsm.AddState("Patrol", new CoState(Patrol, loop: false));
        _fsm.AddState("Chase",
          onLogic: (_, delta) => {
              if (PlayerPosition.HasValue) {
                  MoveTowards(PlayerPosition.Value, ChaseSpeed, delta);
              }
          }
        );
        _fsm.AddState("Fight", fightFsm);
        _fsm.AddState("Search", new CoState(Search, loop: false));

        _fsm.SetStartState("Patrol");

        _fsm.AddTriggerTransition("PlayerSpotted", "Patrol", "Chase");
        _fsm.AddTwoWayTransition("Chase", "Fight", _ => DistanceToPlayer <= AttackRange);
        _fsm.AddTransition("Chase", "Search",
          _ => DistanceToPlayer > SearchSpotRange,
          _ => _lastSeenPlayerPosition = PlayerPosition!.Value);
        _fsm.AddTransition("Search", "Chase", _ => DistanceToPlayer <= SearchSpotRange);
        _fsm.AddTransition(new TransitionAfter("Search", "Patrol", SearchTime));

        _fsm.Init();
    }

    public override void _Process(double delta) {
        StateDisplayText!.Text = _fsm.GetActiveHierarchyPath();
        QueueRedraw();
    }

    public override void _Draw() {
        DrawLine(Position, _nextPosition, Colors.Red, 2);
    }

    public override void _PhysicsProcess(double delta) {
        _fsm.OnLogic(delta);
        Position = _nextPosition;
    }

    /// <summary>
    /// Triggers the `PlayerSpotted` event.
    /// </summary>
    /// <param name="body"></param>
    private void OnDetectionAreaBodyEntered(Node2D body) {
        if (body.IsInGroup("Player")) {
            _player = body;
            _fsm.Trigger("PlayerSpotted");
        }
    }

    private void MoveTowards(Vector2 target, float speed, double delta, float minDistance = 0) {
        _nextPosition = Position.MoveToward(
        target,
        Mathf.Max(0, Mathf.Min(speed * (float)delta, Position.DistanceTo(target) - minDistance))
      );
    }

    private IEnumerator MoveToPosition(Vector2 target, float speed, double delta, float tolerance = 0.05f) {
        while (Position.DistanceTo(target) > tolerance) {
            MoveTowards(target, speed, delta);
            // Wait one frame.
            yield return null;
        }
    }

    private IEnumerator Patrol() {
        int currentPointIndex = FindClosestPatrolPoint();

        while (true) {
            yield return MoveToPosition(PatrolPoints[currentPointIndex], PatrolSpeed, Co.DeltaTime);

            // Wait at each patrol point.
            yield return Co.Wait(3);

            currentPointIndex += _patrolDirection;

            // Once the bot reaches the end or the beginning of the patrol path,
            // it reverses the direction.
            if (currentPointIndex >= PatrolPoints.Length || currentPointIndex < 0) {
                currentPointIndex = Mathf.Clamp(currentPointIndex, 0, PatrolPoints.Length - 1);
                _patrolDirection *= -1;
            }
        }
    }

    private int FindClosestPatrolPoint() {
        float minDistance = Position.DistanceTo(PatrolPoints[0]);
        int minIndex = 0;

        for (int i = 1; i < PatrolPoints.Length; i++) {
            float distance = Position.DistanceTo(PatrolPoints[i]);
            if (distance < minDistance) {
                minDistance = distance;
                minIndex = i;
            }
        }

        return minIndex;
    }

    private IEnumerator Search() {
        yield return MoveToPosition(_lastSeenPlayerPosition, ChaseSpeed, Co.DeltaTime);

        while (true) {
            yield return Co.Wait(2);

            yield return MoveToPosition(
              Position + RandomInsideCircle(SearchRange),
              PatrolSpeed,
              Co.DeltaTime
            );
        }
    }

    private static Vector2 RandomInsideCircle(float radius) {
        double r = Mathf.Sqrt(Random.Shared.NextSingle()) * radius;
        double theta = Random.Shared.NextSingle() * 2 * Mathf.Pi;
        return new Vector2((float)(r * Mathf.Cos(theta)), (float)(r * Mathf.Sin(theta)));
    }
}
