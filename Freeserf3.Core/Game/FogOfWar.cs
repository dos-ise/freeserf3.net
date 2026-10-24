/*
 * FogOfWar.cs - The fog of war visibility grid
 *
 * Ported from settlers-remake (MIT) FogOfWar.java. Each cell has a
 * visibility state: unexplored (fog), explored (seen before, dimmed) or
 * visible. The human player's buildings and settlers reveal a circle
 * around them; cells that leave the view become explored again.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The visibility states of a map cell.</summary>
public enum FogState : byte
{
    /// <summary>Never seen: covered by fog.</summary>
    Unexplored = 0,

    /// <summary>Seen before but not currently visible: dimmed.</summary>
    Explored = 1,

    /// <summary>Currently visible.</summary>
    Visible = 2
}

/// <summary>
/// Tracks which cells of the map the human player can see. Buildings and
/// settlers reveal a circle around them (like settlers-remake, where the
/// view distance of movables is 8 and military buildings reveal their
/// view distance).
/// </summary>
public sealed class FogOfWar
{
    /// <summary>View distance of a walking settler (settlers-remake EMovableType).</summary>
    public const int SettlerViewDistance = 8;

    /// <summary>View distance of an unoccupied building (settlers-remake UNOCCUPIED_VIEW_DISTANCE).</summary>
    public const int UnoccupiedBuildingViewDistance = 5;

    readonly Map map;
    readonly byte[] sight;

    public FogOfWar(Map map)
    {
        this.map = map;
        sight = new byte[map.CellCount];
    }

    /// <summary>Gets the visibility state of a cell.</summary>
    public FogState GetSight(Position pos)
    {
        if (!map.IsInBounds(pos))
            return FogState.Unexplored;
        return (FogState)sight[pos.Y * map.Size + pos.X];
    }

    /// <summary>Gets the visibility state of a cell.</summary>
    public FogState GetSight(int x, int y)
    {
        if (x < 0 || y < 0 || x >= map.Size || y >= map.Size)
            return FogState.Unexplored;
        return (FogState)sight[y * map.Size + x];
    }

    /// <summary>
    /// Advances the fog of war by one tick: cells that were visible become
    /// explored, then the view circles of the human player's buildings and
    /// settlers are revealed again.
    /// </summary>
    public void Update(Game game)
    {
        // Cells that were visible but are no longer covered become explored.
        for (int i = 0; i < sight.Length; i++)
        {
            if (sight[i] == (byte)FogState.Visible)
                sight[i] = (byte)FogState.Explored;
        }

        // The human player's buildings reveal their view distance.
        foreach (var building in game.Buildings)
        {
            if (building.Player != 0)
                continue;
            if (building.State != BuildingState.Active)
                continue;

            int radius = GetBuildingViewDistance(building);
            if (radius > 0)
                RevealCircle(building.Position, radius);
        }

        // The military buildings from the map file reveal too.
        foreach (var mapBuilding in map.Buildings)
        {
            if (mapBuilding.Party != 0)
                continue;
            var type = Game.BuildingTypeFromMap(mapBuilding.BuildingType);
            if (type == null)
                continue;
            var definition = BuildingDefinition.Get(type.Value);
            if (!definition.IsMilitary || definition.ViewDistance <= 0)
                continue;
            RevealCircle(new Position(mapBuilding.PosX, mapBuilding.PosY), definition.ViewDistance);
        }

        // The human player's settlers reveal a small circle.
        foreach (var settler in game.Settlers)
        {
            if (settler.Player != 0)
                continue;
            RevealCircle(settler.Position, SettlerViewDistance);
        }
    }

    /// <summary>Gets the view distance of a building.</summary>
    public static int GetBuildingViewDistance(Building building)
    {
        if (building.Definition.IsMilitary)
            return building.Definition.ViewDistance;
        return UnoccupiedBuildingViewDistance;
    }

    /// <summary>Marks all cells within the given radius as visible.</summary>
    void RevealCircle(Position center, int radius)
    {
        int size = map.Size;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (MapGeometry.GetDistance(dx, dy) > radius)
                    continue;

                int x = center.X + dx;
                int y = center.Y + dy;
                if (x < 0 || y < 0 || x >= size || y >= size)
                    continue;

                sight[y * size + x] = (byte)FogState.Visible;
            }
        }
    }
}
