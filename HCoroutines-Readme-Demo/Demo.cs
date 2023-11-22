using Godot;
using System;
using System.Collections;
using System.Threading.Tasks;

// Import the library.
using HCoroutines;

public partial class Demo : Node2D
{
	public override void _Ready()
	{
		// Spawn a new coroutine that is managed by
		// the default CoroutineManager.
		Co.Run(PlayAnimation());
	}

	private IEnumerator PlayAnimation()
	{
		GD.Print("Starting animation");

		// Wait one frame.
		yield return null;

		// Wait for the GoTo task to finish before continuing.
		yield return GoTo(new Vector2(100, 100), 2);

		// Wait for two seconds.
		yield return Co.Wait(2);

		// Wait for the parallel coroutine to finish.
		// The parallel coroutine waits until all of its
		// sub-coroutines have finished.
		yield return Co.Parallel(
			Co.Coroutine(GoTo(new Vector2(0, 0), 2)),
			Co.Coroutine(Turn(1))
		);

		// Await an async task that waits for 100ms
		yield return Co.Await(Task.Delay(100));

		// Await and use the result of an async task
		var fetch = Co.Await(FetchNumber());
		yield return fetch;
		int number = fetch.Task.Result;

		// Wait for a tween to animate some properties.
		yield return Co.Tween(
			tween =>
			{
				// Change the modulate color over two seconds.
				tween.TweenProperty(this, "modulate", new Color(1, 0, 0), 2);
			}
		);

		// Waits for a signal to be emitted before continuing.
		yield return Co.WaitForSignal(this, "some_signal");
	}

	private IEnumerator GoTo(Vector2 target, float duration)
	{
		float speed = Position.DistanceTo(target) / duration;

		while (Position.DistanceTo(target) > 0.01f)
		{
			// delta time can be accessed via Co.DeltaTime.
			Position = Position.MoveToward(target, speed * Co.DeltaTime);
			yield return null;
		}
	}

	private IEnumerator Turn(float duration)
	{
		float fullRotation = 2 * Mathf.Pi;
		float angularSpeed = fullRotation / duration;
		float angle = 0;

		while (angle < fullRotation)
		{
			angle += angularSpeed * Co.DeltaTime;
			Rotation = angle;
			yield return null;
		}
	}

	private static async Task<int> FetchNumber()
	{
		await Task.Delay(100);
		return 0;
	}
}