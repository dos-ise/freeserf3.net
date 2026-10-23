/*
 * Building.cs - A building on the map with its state machine
 *
 * A building starts as a construction site, becomes active once built and
 * then produces goods when it has a worker and the required inputs.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The states of a building's lifecycle.</summary>
public enum BuildingState
{
    /// <summary>Under construction; materials are still being delivered.</summary>
    ConstructionSite,

    /// <summary>Built and operational (may be waiting for a worker or inputs).</summary>
    Active,

    /// <summary>Actively producing goods.</summary>
    Producing,

    /// <summary>Destroyed or removed.</summary>
    Destroyed
}

/// <summary>
/// One building instance on the map. Holds its position, owner, state,
/// construction progress, assigned worker and production progress.
/// </summary>
public sealed class Building
{
    public Building(BuildingType type, Position position, int player)
    {
        Type = type;
        Position = position;
        Player = player;
    }

    public BuildingType Type { get; }
    public Position Position { get; }
    public int Player { get; }

    public BuildingDefinition Definition => BuildingDefinition.Get(Type);

    public BuildingState State { get; private set; } = BuildingState.ConstructionSite;

    /// <summary>Construction progress in percent (0..100).</summary>
    public int ConstructionProgress { get; private set; }

    /// <summary>The worker assigned to this building (None if unassigned).</summary>
    public WorkerType Worker { get; set; } = WorkerType.None;

    /// <summary>Production progress in ticks (0..Definition.ProductionTime).</summary>
    public int ProductionProgress { get; private set; }

    /// <summary>Goods waiting to be consumed by production.</summary>
    public Inventory Input { get; } = new();

    /// <summary>Goods produced and waiting to be picked up.</summary>
    public Inventory Output { get; } = new();

    /// <summary>Construction materials already delivered to the site.</summary>
    public Inventory DeliveredMaterials { get; } = new();

    /// <summary>Construction materials already used by the builder.</summary>
    public Inventory ConsumedMaterials { get; } = new();

    /// <summary>
    /// The flag in front of the building. Produced goods are queued here
    /// and carried to the player's stock by a carrier.
    /// </summary>
    public Flag? Flag { get; set; }

    /// <summary>Whether all construction materials have been delivered.</summary>
    public bool IsFullySupplied
    {
        get
        {
            foreach (var (material, count) in Definition.ConstructionCosts)
            {
                if (DeliveredMaterials.Get(material) < count)
                    return false;
            }
            return true;
        }
    }

    /// <summary>Whether the building has its required worker.</summary>
    public bool HasWorker => Worker != WorkerType.None;

    /// <summary>Whether the building can start a production cycle.</summary>
    public bool CanProduce
    {
        get
        {
            if (State != BuildingState.Active || !HasWorker)
                return false;

            if (!Definition.IsProducer)
                return false;

            foreach (var input in Definition.Inputs)
            {
                if (Input.Get(input) <= 0)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Advances construction by the given amount of percent.</summary>
    public void AddConstructionProgress(int percent)
    {
        if (State != BuildingState.ConstructionSite)
            return;

        ConstructionProgress = Math.Min(100, ConstructionProgress + percent);
        if (ConstructionProgress >= 100)
            State = BuildingState.Active;
    }

    /// <summary>Assigns a worker to the building.</summary>
    public void AssignWorker(WorkerType worker)
    {
        Worker = worker;
    }

    /// <summary>Removes the worker from the building.</summary>
    public void RemoveWorker()
    {
        Worker = WorkerType.None;
        ProductionProgress = 0;
    }

    /// <summary>Destroys the building.</summary>
    public void Destroy()
    {
        State = BuildingState.Destroyed;
        Worker = WorkerType.None;
        ProductionProgress = 0;
    }

    /// <summary>
    /// Advances the building by one tick. Active producers consume inputs
    /// and produce their output. Construction sites are built by their
    /// builder via <see cref="ConsumeConstructionMaterial"/>. Resource
    /// producers (lumberjack, stonecutter) are driven by their worker via
    /// <see cref="ProduceResource"/>.
    /// </summary>
    public void Tick()
    {
        if (State != BuildingState.Active)
            return;

        if (Definition.IsResourceProducer)
            return;

        if (!Definition.IsProducer || !HasWorker)
        {
            ProductionProgress = 0;
            return;
        }

        if (!CanProduce)
        {
            ProductionProgress = 0;
            return;
        }

        ProductionProgress++;
        if (ProductionProgress >= Definition.ProductionTime)
        {
            CompleteProduction();
        }
    }

    /// <summary>
    /// Consumes one construction material from the delivered materials and
    /// advances the construction progress. The building becomes active once
    /// all materials have been used. Returns false if the site is not under
    /// construction or has no material left.
    /// </summary>
    public bool ConsumeConstructionMaterial()
    {
        if (State != BuildingState.ConstructionSite)
            return false;

        foreach (var (material, count) in Definition.ConstructionCosts)
        {
            if (DeliveredMaterials.Get(material) <= 0)
                continue;
            if (ConsumedMaterials.Get(material) >= count)
                continue;

            DeliveredMaterials.Remove(material);
            ConsumedMaterials.Add(material);
            ConstructionProgress = ConsumedMaterials.TotalCount * 100 / Definition.TotalConstructionCost;
            if (ConsumedMaterials.TotalCount >= Definition.TotalConstructionCost)
            {
                ConstructionProgress = 100;
                State = BuildingState.Active;
            }
            return true;
        }
        return false;
    }

    void CompleteProduction()
    {
        // Consume the inputs.
        foreach (var input in Definition.Inputs)
            Input.Remove(input);

        // Produce the output.
        if (Definition.Output is Material output)
            Output.Add(output);

        ProductionProgress = 0;
    }

    /// <summary>
    /// Produces one unit of the building's output. Used by resource
    /// producers (lumberjack, stonecutter) when their worker has gathered
    /// a tree or stone.
    /// </summary>
    public void ProduceResource()
    {
        if (Definition.Output is Material output)
            Output.Add(output);
    }
}
