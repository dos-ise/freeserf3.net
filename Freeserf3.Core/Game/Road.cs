/*
 * Road.cs - A road connecting two flags, served by a carrier
 *
 * Roads are the edges of the transport network. A carrier (serf) walks
 * back and forth along the road, carrying goods from one flag to the
 * other. The carrier always patrols, so goods flow in both directions.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The states of a road carrier.</summary>
public enum CarrierState
{
    /// <summary>At the start flag, about to pick up a good and walk to the end.</summary>
    IdleAtStart,

    /// <summary>Walking from the start flag to the end flag.</summary>
    WalkingToEnd,

    /// <summary>At the end flag, about to pick up a good and walk back.</summary>
    IdleAtEnd,

    /// <summary>Walking from the end flag back to the start flag.</summary>
    WalkingToStart
}

/// <summary>
/// A road between two flags. A carrier walks the road back and forth,
/// carrying one good at a time from one flag to the other.
/// </summary>
public sealed class Road
{
    public Road(Flag start, Flag end, List<Position> path, int player)
    {
        Start = start;
        End = end;
        Path = path;
        Carrier = new Serf(player, start.Position, SerfType.Carrier)
        {
            MoveInterval = Serf.DefaultMoveInterval
        };
        CarrierState = CarrierState.IdleAtStart;
        CarrierPathIndex = 0;
    }

    /// <summary>Ticks per map cell walked by the carrier (same as walking settlers).</summary>
    public int MoveInterval
    {
        get => Carrier.MoveInterval;
        set => Carrier.MoveInterval = value;
    }

    /// <summary>The flag the road starts at.</summary>
    public Flag Start { get; }

    /// <summary>The flag the road ends at.</summary>
    public Flag End { get; }

    /// <summary>The cells of the road (start flag position exclusive, end flag position inclusive).</summary>
    public List<Position> Path { get; }

    /// <summary>The carrier serving this road.</summary>
    public Serf Carrier { get; }

    /// <summary>Current state of the carrier.</summary>
    public CarrierState CarrierState { get; private set; }

    /// <summary>Index of the next path cell the carrier walks to.</summary>
    public int CarrierPathIndex { get; private set; }

    /// <summary>Number of cells of the road.</summary>
    public int Length => Path.Count;

    /// <summary>Whether the carrier is currently carrying a good.</summary>
    public bool IsCarrying => Carrier.IsCarrying;

    /// <summary>
    /// Optional function that assigns a priority to each material. The
    /// carrier picks up the highest-priority good at a flag (construction
    /// materials before production inputs before other goods); when null,
    /// goods are picked up in FIFO order.
    /// </summary>
    public Func<Material, int>? PriorityFunc { get; set; }

    /// <summary>
    /// Advances the carrier by one tick: it picks up a good at a flag,
    /// walks to the other end, drops the good and turns around. The
    /// carrier only walks when there is a good to transport at either
    /// flag; otherwise it stays put (like the real game, where carriers
    /// do not patrol empty roads).
    /// </summary>
    public void Tick()
    {
        switch (CarrierState)
        {
            case CarrierState.IdleAtStart:
                // Only walk if there is a good to pick up here or at the
                // other end.
                if (Start.Count == 0 && End.Count == 0)
                    return;

                var good = TakeGoodAt(Start);
                if (good.HasValue)
                    Carrier.PickUp(good.Value);
                Carrier.State = MovableState.Walking;
                CarrierState = CarrierState.WalkingToEnd;
                CarrierPathIndex = 0;
                break;

            case CarrierState.WalkingToEnd:
                if (!Carrier.AdvanceMove())
                    break;
                if (CarrierPathIndex < Path.Count)
                {
                    var next = Path[CarrierPathIndex];
                    var direction = Carrier.Position.GetDirectionTo(next);
                    if (direction.HasValue)
                        Carrier.Facing = direction.Value;
                    Carrier.PreviousPosition = Carrier.Position;
                    Carrier.Position = next;
                    CarrierPathIndex++;
                }
                if (CarrierPathIndex >= Path.Count)
                {
                    var carried = Carrier.Drop();
                    if (carried.HasValue)
                        End.AddGood(carried.Value);
                    Carrier.Position = End.Position;
                    Carrier.State = MovableState.Idle;
                    CarrierState = CarrierState.IdleAtEnd;
                }
                break;

            case CarrierState.IdleAtEnd:
                // Only walk back if there is a good to pick up here or at
                // the start.
                if (End.Count == 0 && Start.Count == 0)
                    return;

                var good2 = TakeGoodAt(End);
                if (good2.HasValue)
                    Carrier.PickUp(good2.Value);
                Carrier.State = MovableState.Walking;
                CarrierState = CarrierState.WalkingToStart;
                CarrierPathIndex = Path.Count - 1;
                break;

            case CarrierState.WalkingToStart:
                if (!Carrier.AdvanceMove())
                    break;
                if (CarrierPathIndex >= 0)
                {
                    var next = Path[CarrierPathIndex];
                    var direction = Carrier.Position.GetDirectionTo(next);
                    if (direction.HasValue)
                        Carrier.Facing = direction.Value;
                    Carrier.PreviousPosition = Carrier.Position;
                    Carrier.Position = next;
                    CarrierPathIndex--;
                }
                if (CarrierPathIndex < 0)
                {
                    var carried2 = Carrier.Drop();
                    if (carried2.HasValue)
                        Start.AddGood(carried2.Value);
                    Carrier.Position = Start.Position;
                    Carrier.State = MovableState.Idle;
                    CarrierState = CarrierState.IdleAtStart;
                }
                break;
        }
    }

    /// <summary>
    /// Takes the next good from the flag: the highest-priority good when a
    /// priority function is set, otherwise the first good in the queue.
    /// </summary>
    Material? TakeGoodAt(Flag flag)
    {
        return PriorityFunc != null ? flag.TakeHighestPriority(PriorityFunc) : flag.TakeGood();
    }
}
