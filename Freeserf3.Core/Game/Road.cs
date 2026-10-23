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
        Carrier = new Serf(player, start.Position, SerfType.Carrier);
        CarrierState = CarrierState.IdleAtStart;
        CarrierPathIndex = 0;
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
    /// Advances the carrier by one tick: it picks up a good at a flag,
    /// walks to the other end, drops the good and turns around.
    /// </summary>
    public void Tick()
    {
        switch (CarrierState)
        {
            case CarrierState.IdleAtStart:
                var good = Start.TakeGood();
                if (good.HasValue)
                    Carrier.PickUp(good.Value);
                CarrierState = CarrierState.WalkingToEnd;
                CarrierPathIndex = 0;
                break;

            case CarrierState.WalkingToEnd:
                if (CarrierPathIndex < Path.Count)
                {
                    Carrier.Position = Path[CarrierPathIndex];
                    CarrierPathIndex++;
                }
                if (CarrierPathIndex >= Path.Count)
                {
                    var carried = Carrier.Drop();
                    if (carried.HasValue)
                        End.AddGood(carried.Value);
                    Carrier.Position = End.Position;
                    CarrierState = CarrierState.IdleAtEnd;
                }
                break;

            case CarrierState.IdleAtEnd:
                var good2 = End.TakeGood();
                if (good2.HasValue)
                    Carrier.PickUp(good2.Value);
                CarrierState = CarrierState.WalkingToStart;
                CarrierPathIndex = Path.Count - 1;
                break;

            case CarrierState.WalkingToStart:
                if (CarrierPathIndex >= 0)
                {
                    Carrier.Position = Path[CarrierPathIndex];
                    CarrierPathIndex--;
                }
                if (CarrierPathIndex < 0)
                {
                    var carried2 = Carrier.Drop();
                    if (carried2.HasValue)
                        Start.AddGood(carried2.Value);
                    Carrier.Position = Start.Position;
                    CarrierState = CarrierState.IdleAtStart;
                }
                break;
        }
    }
}
