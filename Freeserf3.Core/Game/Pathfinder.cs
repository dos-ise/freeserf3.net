/*
 * Pathfinder.cs - A* path finding on the hex grid
 *
 * Finds a path between two map positions. The walkability of a cell is
 * controlled by a predicate, so the same search works for settlers,
 * roads and other movement rules.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// A* path finding on the hex grid. Returns the list of positions from
/// start (exclusive) to goal (inclusive), or null if no path exists.
/// </summary>
public static class Pathfinder
{
    /// <summary>
    /// Finds a path from <paramref name="start"/> to <paramref name="goal"/>.
    /// </summary>
    /// <param name="map">The map to search on.</param>
    /// <param name="start">Start position (included in the search, not in the result).</param>
    /// <param name="goal">Goal position (included in the result).</param>
    /// <param name="isWalkable">Predicate deciding whether a cell can be entered.</param>
    /// <returns>The path (start exclusive, goal inclusive), or null if unreachable.</returns>
    public static List<Position>? FindPath(Map map, Position start, Position goal, Func<Map, Position, bool>? isWalkable = null)
    {
        if (!map.IsInBounds(start) || !map.IsInBounds(goal))
            return null;

        if (start == goal)
            return new List<Position>();

        isWalkable ??= DefaultWalkable;

        if (!isWalkable(map, goal))
            return null;

        var open = new PriorityQueue<SearchNode>();
        var gScore = new Dictionary<Position, int>();
        var cameFrom = new Dictionary<Position, Position>();

        var startNode = new SearchNode(start, 0, MapGeometry.GetDistance(start, goal));
        open.Push(startNode);
        gScore[start] = 0;

        while (open.Count > 0)
        {
            var current = open.Pop();

            if (current.Position == goal)
                return ReconstructPath(cameFrom, current.Position);

            foreach (var direction in MapGeometry.AllDirections())
            {
                var neighbor = current.Position.GetNeighbor(direction);
                if (!map.IsInBounds(neighbor))
                    continue;

                if (neighbor != goal && !isWalkable(map, neighbor))
                    continue;

                int tentativeG = current.G + 1;
                if (tentativeG >= gScore.GetValueOrDefault(neighbor, int.MaxValue))
                    continue;

                cameFrom[neighbor] = current.Position;
                gScore[neighbor] = tentativeG;
                open.Push(new SearchNode(neighbor, tentativeG, tentativeG + MapGeometry.GetDistance(neighbor, goal)));
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest reachable position satisfying <paramref name="isTarget"/>
    /// within <paramref name="maxDistance"/> steps, using a breadth-first search.
    /// </summary>
    public static Position? FindNearest(Map map, Position start, Func<Map, Position, bool> isTarget, int maxDistance = int.MaxValue, Func<Map, Position, bool>? isWalkable = null)
    {
        if (!map.IsInBounds(start))
            return null;

        isWalkable ??= DefaultWalkable;

        if (isTarget(map, start))
            return start;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Pos, int Distance)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var (pos, distance) = queue.Dequeue();
            if (distance >= maxDistance)
                continue;

            foreach (var direction in MapGeometry.AllDirections())
            {
                var neighbor = pos.GetNeighbor(direction);
                if (!map.IsInBounds(neighbor) || !visited.Add(neighbor))
                    continue;
                if (!isWalkable(map, neighbor))
                    continue;

                if (isTarget(map, neighbor))
                    return neighbor;

                queue.Enqueue((neighbor, distance + 1));
            }
        }

        return null;
    }

    /// <summary>Default walkability: any in-bounds cell is walkable.</summary>
    public static bool DefaultWalkable(Map map, Position pos)
    {
        return map.IsInBounds(pos);
    }

    static List<Position> ReconstructPath(Dictionary<Position, Position> cameFrom, Position goal)
    {
        var path = new List<Position>();
        var current = goal;

        while (cameFrom.TryGetValue(current, out var previous))
        {
            path.Add(current);
            current = previous;
        }

        path.Reverse();
        return path;
    }

    class SearchNode
    {
        public SearchNode(Position position, int g, int f)
        {
            Position = position;
            G = g;
            F = f;
        }

        public Position Position { get; }
        public int G { get; }
        public int F { get; }
    }

    /// <summary>Min-heap priority queue ordered by F score.</summary>
    sealed class PriorityQueue<T> where T : SearchNode
    {
        readonly List<T> heap = new();

        public int Count => heap.Count;

        public void Push(T item)
        {
            heap.Add(item);
            int index = heap.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (heap[parent].F <= heap[index].F)
                    break;
                (heap[parent], heap[index]) = (heap[index], heap[parent]);
                index = parent;
            }
        }

        public T Pop()
        {
            var top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                int smallest = index;

                if (left < heap.Count && heap[left].F < heap[smallest].F)
                    smallest = left;
                if (right < heap.Count && heap[right].F < heap[smallest].F)
                    smallest = right;

                if (smallest == index)
                    break;

                (heap[index], heap[smallest]) = (heap[smallest], heap[index]);
                index = smallest;
            }

            return top;
        }
    }
}
