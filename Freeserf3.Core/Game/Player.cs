/*
 * Player.cs - A player in the game
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// One player of the game: their goods, population, buildings and flags.
/// AI control is a flag only; the AI itself is milestone M3.
/// </summary>
public sealed class Player
{
    public Player(int index, string name)
    {
        Index = index;
        Name = name;
    }

    /// <summary>Player index (0-based).</summary>
    public int Index { get; }

    public string Name { get; set; }

    /// <summary>Nation id from the map file.</summary>
    public int Nation { get; set; }

    /// <summary>Start position on the map.</summary>
    public Position StartPosition { get; set; }

    /// <summary>Whether this player is controlled by the AI (M3).</summary>
    public bool IsAI { get; set; }

    /// <summary>Whether this player is still in the game.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The player's central goods stock.</summary>
    public Inventory Inventory { get; } = new();

    /// <summary>Number of settlers (population).</summary>
    public int Population { get; set; }

    /// <summary>Number of free (jobless) settlers.</summary>
    public int FreeSettlers { get; set; }

    /// <summary>Building counts per building type (index = building type id).</summary>
    int[] buildingCounts = Array.Empty<int>();

    public int[] BuildingCounts
    {
        get => buildingCounts;
        set => buildingCounts = value ?? Array.Empty<int>();
    }

    /// <summary>Gets the number of buildings of the given type.</summary>
    public int GetBuildingCount(int buildingType)
    {
        if (buildingType < 0 || buildingType >= BuildingCounts.Length)
            return 0;
        return BuildingCounts[buildingType];
    }

    /// <summary>Increments the count of a building type.</summary>
    public void AddBuilding(int buildingType)
    {
        if (buildingType < 0)
            return;
        if (buildingType >= buildingCounts.Length)
            Array.Resize(ref buildingCounts, buildingType + 1);
        buildingCounts[buildingType]++;
    }

    /// <summary>Decrements the count of a building type (never below zero).</summary>
    public void RemoveBuilding(int buildingType)
    {
        if (buildingType < 0 || buildingType >= buildingCounts.Length)
            return;
        if (buildingCounts[buildingType] > 0)
            buildingCounts[buildingType]--;
    }

    /// <summary>Total number of buildings.</summary>
    public int TotalBuildings
    {
        get
        {
            int total = 0;
            for (int i = 0; i < BuildingCounts.Length; i++)
                total += BuildingCounts[i];
            return total;
        }
    }
}
