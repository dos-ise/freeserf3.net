/*
 * BricklayerPositions.cs - Where builders stand while constructing a building
 *
 * The positions are relative to the building position and come from the
 * building XMLs of settlers-remake (MIT), roman civilisation.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>A bricklayer position relative to the building, with the facing direction.</summary>
public readonly record struct BricklayerPosition(int Dx, int Dy, Direction Facing);

/// <summary>
/// The positions around a building where its builders stand while
/// constructing it (from settlers-remake building XMLs).
/// </summary>
public static class BricklayerPositions
{
    static readonly Dictionary<BuildingType, BricklayerPosition[]> positions = new()
    {
        [BuildingType.Stonecutter] = new[]
        {
            new BricklayerPosition(-2, 2, Direction.NorthEast),
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(3, 4, Direction.NorthWest),
            new BricklayerPosition(3, 3, Direction.NorthWest),
            new BricklayerPosition(4, 2, Direction.NorthWest)
        },
        [BuildingType.Forester] = new[]
        {
            new BricklayerPosition(-2, 1, Direction.NorthEast),
            new BricklayerPosition(0, 3, Direction.NorthEast),
            new BricklayerPosition(1, 3, Direction.NorthEast),
            new BricklayerPosition(3, 3, Direction.NorthWest),
            new BricklayerPosition(4, 2, Direction.NorthWest)
        },
        [BuildingType.Lumberjack] = new[]
        {
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(0, 3, Direction.NorthEast),
            new BricklayerPosition(4, 3, Direction.NorthWest),
            new BricklayerPosition(3, 4, Direction.NorthWest)
        },
        [BuildingType.Sawmill] = new[]
        {
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(5, 4, Direction.NorthEast),
            new BricklayerPosition(4, 5, Direction.NorthWest),
            new BricklayerPosition(5, 5, Direction.NorthWest),
            new BricklayerPosition(6, 4, Direction.NorthWest)
        },
        [BuildingType.Coalmine] = new[]
        {
            new BricklayerPosition(-2, 1, Direction.NorthEast),
            new BricklayerPosition(0, 3, Direction.NorthEast),
            new BricklayerPosition(2, 3, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.Ironmine] = new[]
        {
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(3, 3, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest),
            new BricklayerPosition(3, 4, Direction.NorthWest)
        },
        [BuildingType.Goldmine] = new[]
        {
            new BricklayerPosition(-2, 2, Direction.NorthEast),
            new BricklayerPosition(0, 3, Direction.NorthEast),
            new BricklayerPosition(2, 3, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.Goldmelt] = new[]
        {
            new BricklayerPosition(-2, 2, Direction.NorthEast),
            new BricklayerPosition(0, 4, Direction.NorthEast),
            new BricklayerPosition(2, 3, Direction.NorthWest),
            new BricklayerPosition(2, 1, Direction.NorthWest),
            new BricklayerPosition(3, 0, Direction.NorthWest)
        },
        [BuildingType.Ironmelt] = new[]
        {
            new BricklayerPosition(-3, 2, Direction.NorthEast),
            new BricklayerPosition(-1, 4, Direction.NorthEast),
            new BricklayerPosition(3, 0, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest),
            new BricklayerPosition(2, 3, Direction.NorthWest)
        },
        [BuildingType.Toolsmith] = new[]
        {
            new BricklayerPosition(-4, 0, Direction.NorthEast),
            new BricklayerPosition(-2, 2, Direction.NorthEast),
            new BricklayerPosition(0, 2, Direction.NorthEast),
            new BricklayerPosition(4, 1, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.Weaponsmith] = new[]
        {
            new BricklayerPosition(-2, 2, Direction.NorthEast),
            new BricklayerPosition(0, 3, Direction.NorthEast),
            new BricklayerPosition(4, 1, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.Farm] = new[]
        {
            new BricklayerPosition(-2, 4, Direction.NorthEast),
            new BricklayerPosition(0, 5, Direction.NorthEast),
            new BricklayerPosition(2, 5, Direction.NorthWest),
            new BricklayerPosition(6, 7, Direction.NorthWest),
            new BricklayerPosition(7, 5, Direction.NorthWest),
            new BricklayerPosition(6, 2, Direction.West)
        },
        [BuildingType.PigFarm] = new[]
        {
            new BricklayerPosition(-2, 3, Direction.NorthEast),
            new BricklayerPosition(0, 5, Direction.NorthEast),
            new BricklayerPosition(5, 6, Direction.NorthWest),
            new BricklayerPosition(6, 5, Direction.NorthWest),
            new BricklayerPosition(6, 3, Direction.NorthWest)
        },
        [BuildingType.Mill] = new[]
        {
            new BricklayerPosition(-2, 1, Direction.NorthEast),
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(2, 0, Direction.NorthWest),
            new BricklayerPosition(2, 1, Direction.NorthWest),
            new BricklayerPosition(2, 2, Direction.NorthWest)
        },
        [BuildingType.Waterworks] = new[]
        {
            new BricklayerPosition(-2, 2, Direction.NorthEast),
            new BricklayerPosition(0, 4, Direction.NorthEast),
            new BricklayerPosition(3, 4, Direction.NorthWest),
            new BricklayerPosition(4, 3, Direction.NorthWest)
        },
        [BuildingType.Slaughterhouse] = new[]
        {
            new BricklayerPosition(-3, 1, Direction.NorthEast),
            new BricklayerPosition(0, 4, Direction.NorthEast),
            new BricklayerPosition(3, 4, Direction.NorthWest),
            new BricklayerPosition(4, 3, Direction.NorthWest)
        },
        [BuildingType.Baker] = new[]
        {
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(4, 4, Direction.NorthWest),
            new BricklayerPosition(4, 2, Direction.NorthWest)
        },
        [BuildingType.Fisher] = new[]
        {
            new BricklayerPosition(0, 3, Direction.NorthEast),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(3, 2, Direction.NorthWest),
            new BricklayerPosition(3, 3, Direction.NorthWest)
        },
        [BuildingType.Winegrower] = new[]
        {
            new BricklayerPosition(-3, 1, Direction.NorthEast),
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(2, 3, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.CharcoalBurner] = new[]
        {
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(2, 3, Direction.NorthEast),
            new BricklayerPosition(6, 4, Direction.NorthWest),
            new BricklayerPosition(6, 2, Direction.NorthWest),
            new BricklayerPosition(1, 2, Direction.NorthWest)
        },
        [BuildingType.DonkeyFarm] = new[]
        {
            new BricklayerPosition(-4, 1, Direction.NorthEast),
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(0, 4, Direction.NorthEast),
            new BricklayerPosition(3, 7, Direction.NorthEast),
            new BricklayerPosition(7, 4, Direction.NorthWest)
        },
        [BuildingType.SmallLivinghouse] = new[]
        {
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(2, 5, Direction.NorthEast),
            new BricklayerPosition(4, 2, Direction.NorthWest),
            new BricklayerPosition(4, 4, Direction.NorthWest)
        },
        [BuildingType.MediumLivinghouse] = new[]
        {
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(2, 5, Direction.NorthEast),
            new BricklayerPosition(4, 4, Direction.NorthWest),
            new BricklayerPosition(6, 4, Direction.NorthWest),
            new BricklayerPosition(6, 2, Direction.NorthWest)
        },
        [BuildingType.BigLivinghouse] = new[]
        {
            new BricklayerPosition(-3, 1, Direction.NorthEast),
            new BricklayerPosition(1, 5, Direction.NorthEast),
            new BricklayerPosition(3, 6, Direction.NorthEast),
            new BricklayerPosition(5, 5, Direction.NorthWest),
            new BricklayerPosition(5, 3, Direction.NorthWest),
            new BricklayerPosition(6, 2, Direction.NorthWest)
        },
        [BuildingType.LookoutTower] = new[]
        {
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(1, 3, Direction.NorthEast),
            new BricklayerPosition(3, 3, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.Tower] = new[]
        {
            new BricklayerPosition(-1, 1, Direction.NorthEast),
            new BricklayerPosition(1, 3, Direction.NorthEast),
            new BricklayerPosition(3, 3, Direction.NorthWest),
            new BricklayerPosition(3, 1, Direction.NorthWest),
            new BricklayerPosition(3, 2, Direction.NorthWest)
        },
        [BuildingType.BigTower] = new[]
        {
            new BricklayerPosition(-3, 1, Direction.NorthEast),
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(4, 2, Direction.NorthWest),
            new BricklayerPosition(3, 4, Direction.NorthWest)
        },
        [BuildingType.Castle] = new[]
        {
            new BricklayerPosition(-4, 2, Direction.NorthEast),
            new BricklayerPosition(-2, 3, Direction.NorthEast),
            new BricklayerPosition(1, 6, Direction.NorthEast),
            new BricklayerPosition(4, 7, Direction.NorthWest),
            new BricklayerPosition(4, 4, Direction.NorthWest),
            new BricklayerPosition(5, 3, Direction.NorthWest)
        },
        [BuildingType.Hospital] = new[]
        {
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(4, 4, Direction.NorthWest),
            new BricklayerPosition(4, 2, Direction.NorthWest)
        },
        [BuildingType.Barrack] = new[]
        {
            new BricklayerPosition(-2, 0, Direction.NorthEast),
            new BricklayerPosition(0, 2, Direction.NorthEast),
            new BricklayerPosition(2, 1, Direction.NorthWest),
            new BricklayerPosition(6, 2, Direction.NorthWest),
            new BricklayerPosition(5, 3, Direction.NorthWest)
        },
        [BuildingType.Dockyard] = new[]
        {
            new BricklayerPosition(-1, 3, Direction.NorthEast),
            new BricklayerPosition(3, 1, Direction.NorthWest),
            new BricklayerPosition(3, 3, Direction.NorthWest),
            new BricklayerPosition(1, 3, Direction.NorthWest)
        },
        [BuildingType.Harbor] = new[]
        {
            new BricklayerPosition(-2, 0, Direction.NorthEast),
            new BricklayerPosition(0, 2, Direction.NorthEast),
            new BricklayerPosition(3, 0, Direction.NorthWest),
            new BricklayerPosition(2, 1, Direction.NorthWest)
        },
        [BuildingType.Stock] = new[]
        {
            new BricklayerPosition(-1, -5, Direction.SouthWest),
            new BricklayerPosition(1, 4, Direction.NorthEast),
            new BricklayerPosition(5, 3, Direction.NorthWest),
            new BricklayerPosition(-3, -2, Direction.NorthWest)
        },
        [BuildingType.Temple] = new[]
        {
            new BricklayerPosition(-2, 3, Direction.NorthEast),
            new BricklayerPosition(1, 6, Direction.NorthEast),
            new BricklayerPosition(5, 3, Direction.NorthWest),
            new BricklayerPosition(4, 6, Direction.NorthWest),
            new BricklayerPosition(7, 7, Direction.NorthWest)
        },
        [BuildingType.BigTemple] = new[]
        {
            new BricklayerPosition(-2, 4, Direction.NorthEast),
            new BricklayerPosition(2, 7, Direction.NorthEast),
            new BricklayerPosition(0, 2, Direction.NorthEast),
            new BricklayerPosition(7, 2, Direction.NorthWest),
            new BricklayerPosition(6, 3, Direction.NorthWest),
            new BricklayerPosition(6, 5, Direction.NorthWest)
        },
        [BuildingType.MarketPlace] = new[]
        {
            new BricklayerPosition(-1, 2, Direction.NorthEast),
            new BricklayerPosition(-1, 1, Direction.NorthEast),
            new BricklayerPosition(2, 2, Direction.NorthWest),
            new BricklayerPosition(2, 0, Direction.NorthWest)
        }
    };

    /// <summary>
    /// Gets the bricklayer positions for the given building type, or an
    /// empty array if none are defined.
    /// </summary>
    public static IReadOnlyList<BricklayerPosition> For(BuildingType type)
    {
        return positions.TryGetValue(type, out var list) ? list : Array.Empty<BricklayerPosition>();
    }
}
