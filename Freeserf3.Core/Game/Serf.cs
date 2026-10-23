/*
 * Serf.cs - The settler unit (carrier, builder, soldier, worker)
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The roles a settler can take.</summary>
public enum SerfType
{
    /// <summary>Transports goods between flags.</summary>
    Carrier,

    /// <summary>Builds buildings.</summary>
    Builder,

    /// <summary>Fights (military).</summary>
    Soldier,

    /// <summary>Works in a production building.</summary>
    Worker
}

/// <summary>
/// A settler on the map. Carries goods, walks along paths and works in
/// buildings.
/// </summary>
public sealed class Serf : Movable
{
    public Serf(int player, Position position, SerfType type)
        : base(player, position)
    {
        Type = type;
    }

    /// <summary>The role of this settler.</summary>
    public SerfType Type { get; }

    /// <summary>The profession when working in a building.</summary>
    public WorkerType Profession { get; set; } = WorkerType.None;

    /// <summary>The good currently carried, or null.</summary>
    public Material? CarriedGood { get; set; }

    /// <summary>Whether the settler carries a good.</summary>
    public bool IsCarrying => CarriedGood.HasValue;

    /// <summary>Picks up a good.</summary>
    public void PickUp(Material material)
    {
        CarriedGood = material;
    }

    /// <summary>Drops the carried good and returns it.</summary>
    public Material? Drop()
    {
        var good = CarriedGood;
        CarriedGood = null;
        return good;
    }

    /// <summary>Advances the settler by one tick.</summary>
    public override void Tick()
    {
        if (State == MovableState.Walking)
            WalkAlongPath();
    }
}
