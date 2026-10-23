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
    /// Advances the building by one tick. Construction sites gain progress;
    /// active producers consume inputs and produce their output.
    /// </summary>
    public void Tick()
    {
        if (State == BuildingState.ConstructionSite)
        {
            // Construction progress is driven by delivered materials and
            // bricklayers (M2.4); here we only advance when materials are
            // present.
            return;
        }

        if (State != BuildingState.Active)
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
}
