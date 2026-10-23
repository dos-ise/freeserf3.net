/*
 * MapGeometry.cs - Hex grid geometry for the Settlers 3 map
 *
 * Ported from settlers-remake (MIT): EDirection.java and ShortPoint2D.java.
 *
 * The Settlers 3 map uses a pointy-top hex grid. Each cell has six
 * neighbours. The grid deltas are:
 *
 *   NorthEast (0,-1)  East (1,0)  SouthEast (1,1)
 *   SouthWest (0,1)   West (-1,0) NorthWest (-1,-1)
 *
 * Screen space (used by the renderer) maps grid coordinates to pixels:
 *   screenX = x * TileWidth  - y * TileWidth / 2
 *   screenY = y * TileHeight - height * HeightScale
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Game;

/// <summary>The six movement directions on the hex grid.</summary>
public enum Direction
{
    NorthEast = 0,
    East = 1,
    SouthEast = 2,
    SouthWest = 3,
    West = 4,
    NorthWest = 5
}

/// <summary>An immutable position on the map grid.</summary>
public readonly record struct Position(int X, int Y)
{
    public Position GetNeighbor(Direction direction) => MapGeometry.GetNeighbor(this, direction);

    public int GetDistanceTo(Position other) => MapGeometry.GetDistance(this, other);

    /// <summary>Direction to a direct neighbour, or null if not adjacent.</summary>
    public Direction? GetDirectionTo(Position other) => MapGeometry.GetDirection(this, other);

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>Static helpers for the hex grid: neighbours, distances, directions.</summary>
public static class MapGeometry
{
    /// <summary>Number of directions on the hex grid.</summary>
    public const int DirectionCount = 6;

    /// <summary>Grid deltas for each direction (index = (int)Direction).</summary>
    public static readonly (int Dx, int Dy)[] DirectionDeltas =
    {
        (0, -1), // NorthEast
        (1, 0),  // East
        (1, 1),  // SouthEast
        (0, 1),  // SouthWest
        (-1, 0), // West
        (-1, -1) // NorthWest
    };

    // Screen-space constants (from the terrain renderer / settlers-remake).
    public const float TileWidth = 16f;
    public const float TileHeight = 16f;
    public const float HeightScale = 2f;

    /// <summary>Returns the neighbour of <paramref name="pos"/> in the given direction.</summary>
    public static Position GetNeighbor(Position pos, Direction direction)
    {
        var (dx, dy) = DirectionDeltas[(int)direction];
        return new Position(pos.X + dx, pos.Y + dy);
    }

    /// <summary>Returns the neighbour of (x, y) in the given direction.</summary>
    public static Position GetNeighbor(int x, int y, Direction direction)
    {
        var (dx, dy) = DirectionDeltas[(int)direction];
        return new Position(x + dx, y + dy);
    }

    /// <summary>
    /// Number of grid steps a movable must at least walk to get from
    /// <paramref name="a"/> to <paramref name="b"/>.
    /// </summary>
    public static int GetDistance(Position a, Position b)
    {
        return GetDistance(a.X - b.X, a.Y - b.Y);
    }

    /// <summary>Grid distance from (0,0) to (dx, dy).</summary>
    public static int GetDistance(int dx, int dy)
    {
        int absDx = Math.Abs(dx);
        int absDy = Math.Abs(dy);

        // dx and dy go in the same direction: the longer component dominates.
        if (dx * dy > 0)
            return Math.Max(absDx, absDy);

        return absDx + absDy;
    }

    /// <summary>
    /// Direction from <paramref name="from"/> to <paramref name="to"/> if they
    /// are direct neighbours, otherwise null.
    /// </summary>
    public static Direction? GetDirection(Position from, Position to)
    {
        return GetDirection(to.X - from.X, to.Y - from.Y);
    }

    /// <summary>Direction for the given grid delta, or null if not a neighbour step.</summary>
    public static Direction? GetDirection(int dx, int dy)
    {
        for (int i = 0; i < DirectionCount; i++)
        {
            var (ddx, ddy) = DirectionDeltas[i];
            if (ddx == dx && ddy == dy)
                return (Direction)i;
        }

        return null;
    }

    /// <summary>
    /// Returns the direction that best approximates the way from
    /// <paramref name="from"/> to <paramref name="to"/> (not necessarily a
    /// direct neighbour). Undefined but non-null for equal positions.
    /// </summary>
    public static Direction GetApproxDirection(Position from, Position to)
    {
        return GetApproxDirection(from.X, from.Y, to.X, to.Y);
    }

    /// <summary>Approximate direction between two arbitrary positions.</summary>
    public static Direction GetApproxDirection(int fx, int fy, int sx, int sy)
    {
        int dx = sx - fx;
        int dy = sy - fy;

        const double tan22_5 = 0.41421356237309503; // Math.Tan(22.5 deg)
        const double tan67_5 = 2.414213562373095;   // Math.Tan(67.5 deg)

        if (dx == 0)
            return dy < 0 ? Direction.NorthEast : Direction.SouthWest;

        double incline = (double)dy / dx;

        if (dx > 0)
        {
            if (incline < -1) return Direction.NorthEast;
            if (incline < tan22_5) return Direction.East;
            if (incline < tan67_5) return Direction.SouthEast;
            return Direction.SouthWest;
        }
        else
        {
            if (incline < -1) return Direction.SouthWest;
            if (incline < tan22_5) return Direction.West;
            if (incline < tan67_5) return Direction.NorthWest;
            return Direction.NorthEast;
        }
    }

    /// <summary>Returns the opposite direction.</summary>
    public static Direction GetInverse(Direction direction)
    {
        return (Direction)(((int)direction + DirectionCount / 2) % DirectionCount);
    }

    /// <summary>Rotates the direction clockwise by the given number of steps (negative = counter-clockwise).</summary>
    public static Direction Rotate(Direction direction, int steps)
    {
        int result = ((int)direction + steps) % DirectionCount;
        if (result < 0)
            result += DirectionCount;
        return (Direction)result;
    }

    /// <summary>All six directions in clockwise order.</summary>
    public static IEnumerable<Direction> AllDirections()
    {
        for (int i = 0; i < DirectionCount; i++)
            yield return (Direction)i;
    }

    /// <summary>Converts a grid position to screen pixel coordinates.</summary>
    public static (float X, float Y) GridToScreen(Position pos, float height = 0f)
    {
        float x = pos.X * TileWidth - pos.Y * TileWidth / 2f;
        float y = pos.Y * TileHeight - height * HeightScale;
        return (x, y);
    }

    /// <summary>
    /// Converts screen pixel coordinates back to a grid position (ignoring
    /// height). The result may be outside the map.
    /// </summary>
    public static Position ScreenToGrid(float screenX, float screenY)
    {
        int y = (int)MathF.Round(screenY / TileHeight);
        int x = (int)MathF.Round((screenX + y * TileWidth / 2f) / TileWidth);
        return new Position(x, y);
    }
}
