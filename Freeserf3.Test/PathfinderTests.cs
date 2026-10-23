/*
 * PathfinderTests.cs - Tests for A* path finding on the hex grid
 */

using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class PathfinderTests
{
    [TestMethod]
    public void FindsStraightPath()
    {
        var map = new Map(16);
        var start = new Position(2, 2);
        var goal = new Position(6, 2);

        var path = Pathfinder.FindPath(map, start, goal);

        Assert.IsNotNull(path);
        Assert.AreEqual(goal, path![^1]);
        Assert.AreEqual(4, path.Count);
        Assert.AreEqual(new Position(3, 2), path[0]);
    }

    [TestMethod]
    public void FindsDiagonalPath()
    {
        var map = new Map(16);
        var start = new Position(0, 0);
        var goal = new Position(5, 5);

        var path = Pathfinder.FindPath(map, start, goal);

        Assert.IsNotNull(path);
        Assert.AreEqual(goal, path![^1]);
        // The path length must equal the grid distance.
        Assert.AreEqual(start.GetDistanceTo(goal), path.Count);
    }

    [TestMethod]
    public void PathIsContinuous()
    {
        var map = new Map(16);
        var start = new Position(1, 1);
        var goal = new Position(9, 7);

        var path = Pathfinder.FindPath(map, start, goal);
        Assert.IsNotNull(path);

        var previous = start;
        foreach (var pos in path!)
        {
            Assert.AreEqual(1, previous.GetDistanceTo(pos), "Path steps must be adjacent.");
            previous = pos;
        }
    }

    [TestMethod]
    public void ReturnsNullForUnreachableGoal()
    {
        var map = new Map(8);
        var start = new Position(0, 0);
        var goal = new Position(7, 7);

        // Block everything except the start cell.
        var path = Pathfinder.FindPath(map, start, goal, (m, pos) => pos == start);

        Assert.IsNull(path);
    }

    [TestMethod]
    public void ReturnsEmptyPathForSamePosition()
    {
        var map = new Map(8);
        var pos = new Position(3, 3);

        var path = Pathfinder.FindPath(map, pos, pos);
        Assert.IsNotNull(path);
        Assert.HasCount(0, path!);
    }

    [TestMethod]
    public void ReturnsNullForOutOfBounds()
    {
        var map = new Map(8);
        Assert.IsNull(Pathfinder.FindPath(map, new Position(-1, 0), new Position(3, 3)));
        Assert.IsNull(Pathfinder.FindPath(map, new Position(0, 0), new Position(8, 8)));
    }

    [TestMethod]
    public void AvoidsBlockedCells()
    {
        var map = new Map(8);
        var start = new Position(0, 0);
        var goal = new Position(4, 0);

        // Block the direct east path.
        var path = Pathfinder.FindPath(map, start, goal, (m, pos) => pos != new Position(1, 0) && pos != new Position(2, 0));

        Assert.IsNotNull(path);
        Assert.AreEqual(goal, path![^1]);
        Assert.IsFalse(path.Contains(new Position(1, 0)));
        Assert.IsFalse(path.Contains(new Position(2, 0)));
    }

    [TestMethod]
    public void FindNearestFindsClosestTarget()
    {
        var map = new Map(8);
        var start = new Position(2, 2);

        var found = Pathfinder.FindNearest(map, start, (m, pos) => pos == new Position(5, 5) || pos == new Position(3, 2));

        Assert.AreEqual(new Position(3, 2), found);
    }

    [TestMethod]
    public void FindNearestReturnsNullWhenNothingMatches()
    {
        var map = new Map(8);
        var start = new Position(2, 2);

        var found = Pathfinder.FindNearest(map, start, (m, pos) => false, maxDistance: 3);
        Assert.IsNull(found);
    }
}
