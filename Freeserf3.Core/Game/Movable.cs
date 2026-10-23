/*
 * Movable.cs - Base class for moving units on the map
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The states of a movable unit.</summary>
public enum MovableState
{
    /// <summary>Standing still, waiting for a task.</summary>
    Idle,

    /// <summary>Walking along a path.</summary>
    Walking,

    /// <summary>Performing work (building, producing, ...).</summary>
    Working
}

/// <summary>
/// Base class of all moving units: position, target, movement path and
/// state. Subclasses implement the per-tick behaviour.
/// </summary>
public abstract class Movable
{
    protected Movable(int player, Position position)
    {
        Player = player;
        Position = position;
    }

    /// <summary>Owning player.</summary>
    public int Player { get; }

    /// <summary>Current position on the grid.</summary>
    public Position Position { get; set; }

    /// <summary>Final destination, or null if none.</summary>
    public Position? Target { get; set; }

    /// <summary>Current state.</summary>
    public MovableState State { get; set; } = MovableState.Idle;

    /// <summary>The path currently being walked (start exclusive).</summary>
    public List<Position>? Path { get; private set; }

    /// <summary>Index of the next path position to walk to.</summary>
    public int PathIndex { get; private set; }

    /// <summary>Whether the unit is currently walking.</summary>
    public bool IsWalking => State == MovableState.Walking;

    /// <summary>
    /// Sets a new path to walk. The path must not include the current
    /// position (start-exclusive, as returned by the pathfinder).
    /// </summary>
    public void SetPath(List<Position> path)
    {
        Path = path;
        PathIndex = 0;
        State = path.Count > 0 ? MovableState.Walking : MovableState.Idle;
    }

    /// <summary>Stops the unit at its current position.</summary>
    public void Stop()
    {
        Path = null;
        PathIndex = 0;
        State = MovableState.Idle;
    }

    /// <summary>Advances one step along the current path.</summary>
    protected void WalkAlongPath()
    {
        if (Path == null || PathIndex >= Path.Count)
        {
            Stop();
            return;
        }

        Position = Path[PathIndex];
        PathIndex++;

        if (PathIndex >= Path.Count)
        {
            Target = Position;
            Stop();
        }
    }

    /// <summary>Advances the unit by one tick.</summary>
    public abstract void Tick();
}
