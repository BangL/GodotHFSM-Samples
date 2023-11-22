using System;
using System.Collections;
using Godot;
using GodotHFSM;  // Import GodotHFSM
using HCoroutines;

namespace GodotHFSM.Samples.GuardAI
{

    public partial class GuardAI : CharacterBody2D
    {
        // Declare the finite state machine
        private StateMachine fsm;

        // Parameters (can be changed in the inspector)
        //[Export] public Animator animator;
        [Export] public Label stateDisplayText;
        [Export] public float searchSpotRange = 350;
        [Export] public float attackRange = 60;
        [Export] public float searchRange = 150;
        [Export] public float searchTime = 20;  // in seconds
        [Export] public float patrolSpeed = 150;
        [Export] public float chaseSpeed = 200;
        [Export] public float attackSpeed = 150;
        [Export] public Vector2[] patrolPoints;

        // Internal fields
        private int patrolDirection = 1;
        private Vector2 lastSeenPlayerPosition;
        private Node2D player;
        private Vector2 nextPosition;

        // Helper methods (depend on how your scene has been set up)
        private Vector2 playerPosition => player.Position;
        private float distanceToPlayer => playerPosition.DistanceTo(Position);

        public override void _Ready()
        {
            nextPosition = Position;

            fsm = new StateMachine();

            // Fight FSM
            var fightFsm = new HybridStateMachine(
                beforeOnLogic: (state, delta) => MoveTowards(playerPosition, attackSpeed, delta, minDistance: 40),
                needsExitTime: true
            );

            fightFsm.AddState("Wait"/*, onEnter: state => animator.Play("GuardIdle")*/);
            fightFsm.AddState("Telegraph"/*, onEnter: state => animator.Play("GuardTelegraph")*/);
            fightFsm.AddState("Hit",
                onEnter: state =>
                {
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
            fsm.AddState("Patrol", new CoState(Patrol, loop: false));
            fsm.AddState("Chase",
                onLogic: (state, delta) => MoveTowards(playerPosition, chaseSpeed, delta)
            );
            fsm.AddState("Fight", fightFsm);
            fsm.AddState("Search", new CoState(Search, loop: false));

            fsm.SetStartState("Patrol");

            fsm.AddTriggerTransition("PlayerSpotted", "Patrol", "Chase");
            fsm.AddTwoWayTransition("Chase", "Fight", t => distanceToPlayer <= attackRange);
            fsm.AddTransition("Chase", "Search",
                t => distanceToPlayer > searchSpotRange,
                onTransition: t => lastSeenPlayerPosition = playerPosition);
            fsm.AddTransition("Search", "Chase", t => distanceToPlayer <= searchSpotRange);
            fsm.AddTransition(new TransitionAfter("Search", "Patrol", searchTime));

            fsm.Init();
        }

        public override void _Process(double delta)
        {
            stateDisplayText.Text = fsm.GetActiveHierarchyPath();
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawLine(Position, nextPosition, Colors.Red, 2, false);
        }

        public override void _PhysicsProcess(double delta)
        {
            fsm.OnLogic(delta);
            Position = nextPosition;
        }

        // Triggers the `PlayerSpotted` event.
        private void OnDetectionAreaBodyEntered(Node2D body)
        {
            if (body.IsInGroup("Player"))
            {
                player = body;
                fsm.Trigger("PlayerSpotted");
            }
        }

        private void MoveTowards(Vector2 target, float speed, double delta, float minDistance = 0)
        {
            nextPosition = Position.MoveToward(
                target,
                Mathf.Max(0, Mathf.Min(speed * (float)delta, Position.DistanceTo(target) - minDistance))
            );
        }

        private IEnumerator MoveToPosition(Vector2 target, float speed, double delta, float tolerance = 0.05f)
        {
            while (Position.DistanceTo(target) > tolerance)
            {
                MoveTowards(target, speed, delta);
                // Wait one frame.
                yield return null;
            }
        }

        private IEnumerator Patrol()
        {
            int currentPointIndex = FindClosestPatrolPoint();

            while (true)
            {
                yield return MoveToPosition(patrolPoints[currentPointIndex], patrolSpeed, Co.DeltaTime);

                // Wait at each patrol point.
                yield return Co.Wait(3);

                currentPointIndex += patrolDirection;

                // Once the bot reaches the end or the beginning of the patrol path,
                // it reverses the direction.
                if (currentPointIndex >= patrolPoints.Length || currentPointIndex < 0)
                {
                    currentPointIndex = Mathf.Clamp(currentPointIndex, 0, patrolPoints.Length - 1);
                    patrolDirection *= -1;
                }
            }
        }

        private int FindClosestPatrolPoint()
        {
            float minDistance = Position.DistanceTo(patrolPoints[0]);
            int minIndex = 0;

            for (int i = 1; i < patrolPoints.Length; i++)
            {
                float distance = Position.DistanceTo(patrolPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = i;
                }
            }

            return minIndex;
        }

        private IEnumerator Search()
        {
            yield return MoveToPosition(lastSeenPlayerPosition, chaseSpeed, Co.DeltaTime);

            while (true)
            {
                yield return Co.Wait(2);

                yield return MoveToPosition(
                    Position + RandomInsideCircle(searchRange),
                    patrolSpeed,
                    Co.DeltaTime
                );
            }
        }

        private static Vector2 RandomInsideCircle(float radius)
        {
            double r = Mathf.Sqrt(Random.Shared.NextSingle()) * radius;
            double theta = Random.Shared.NextSingle() * 2 * Mathf.Pi;
            return new Vector2((float)(r * Mathf.Cos(theta)), (float)(r * Mathf.Sin(theta)));
        }
    }

}