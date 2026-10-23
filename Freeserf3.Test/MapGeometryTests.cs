/*
 * MapGeometryTests.cs - Tests for the hex grid geometry
 */

using Freeserf3.Game;

namespace Freeserf3.Test;

[TestClass]
public class MapGeometryTests
{
    [TestMethod]
    public void NeighborDeltasMatchSettlers3Grid()
    {
        var origin = new Position(0, 0);

        Assert.AreEqual(new Position(0, -1), origin.GetNeighbor(Direction.NorthEast));
        Assert.AreEqual(new Position(1, 0), origin.GetNeighbor(Direction.East));
        Assert.AreEqual(new Position(1, 1), origin.GetNeighbor(Direction.SouthEast));
        Assert.AreEqual(new Position(0, 1), origin.GetNeighbor(Direction.SouthWest));
        Assert.AreEqual(new Position(-1, 0), origin.GetNeighbor(Direction.West));
        Assert.AreEqual(new Position(-1, -1), origin.GetNeighbor(Direction.NorthWest));
    }

    [TestMethod]
    public void NeighborIsSymmetric()
    {
        var a = new Position(3, 5);
        foreach (var direction in MapGeometry.AllDirections())
        {
            var b = a.GetNeighbor(direction);
            Assert.AreEqual(a, b.GetNeighbor(MapGeometry.GetInverse(direction)));
        }
    }

    [TestMethod]
    public void GetDirectionReturnsNeighborDirection()
    {
        var a = new Position(2, 2);

        Assert.AreEqual(Direction.East, a.GetDirectionTo(new Position(3, 2)));
        Assert.AreEqual(Direction.SouthEast, a.GetDirectionTo(new Position(3, 3)));
        Assert.AreEqual(Direction.NorthWest, a.GetDirectionTo(new Position(1, 1)));
        Assert.IsNull(a.GetDirectionTo(new Position(5, 5)));
        Assert.IsNull(a.GetDirectionTo(a));
    }

    [TestMethod]
    public void GetInverseAndRotate()
    {
        Assert.AreEqual(Direction.SouthWest, MapGeometry.GetInverse(Direction.NorthEast));
        Assert.AreEqual(Direction.West, MapGeometry.GetInverse(Direction.East));
        Assert.AreEqual(Direction.NorthEast, MapGeometry.GetInverse(Direction.SouthWest));

        // Rotating 6 times returns the original direction.
        foreach (var direction in MapGeometry.AllDirections())
        {
            Assert.AreEqual(direction, MapGeometry.Rotate(direction, 6));
            Assert.AreEqual(direction, MapGeometry.Rotate(direction, -6));
        }

        // One clockwise step.
        Assert.AreEqual(Direction.East, MapGeometry.Rotate(Direction.NorthEast, 1));
    }

    [TestMethod]
    public void GridDistanceSameDirection()
    {
        // Same direction: the longer component dominates.
        Assert.AreEqual(3, MapGeometry.GetDistance(new Position(0, 0), new Position(3, 3)));
        Assert.AreEqual(4, MapGeometry.GetDistance(new Position(0, 0), new Position(4, 2)));
        Assert.AreEqual(2, MapGeometry.GetDistance(new Position(0, 0), new Position(1, 2)));
    }

    [TestMethod]
    public void GridDistanceOppositeDirection()
    {
        // Opposite directions: components add up.
        Assert.AreEqual(5, MapGeometry.GetDistance(new Position(0, 0), new Position(3, -2)));
        Assert.AreEqual(4, MapGeometry.GetDistance(new Position(0, 0), new Position(-1, 3)));
    }

    [TestMethod]
    public void GridDistanceIsSymmetric()
    {
        var a = new Position(7, 3);
        var b = new Position(2, 9);
        Assert.AreEqual(MapGeometry.GetDistance(a, b), MapGeometry.GetDistance(b, a));
    }

    [TestMethod]
    public void GridDistanceMatchesStepCount()
    {
        // Walking along the grid must take exactly the grid distance.
        var start = new Position(0, 0);
        var end = new Position(5, 8);
        int distance = start.GetDistanceTo(end);

        var pos = start;
        int steps = 0;
        while (pos != end && steps < 100)
        {
            var dir = MapGeometry.GetApproxDirection(pos, end);
            pos = pos.GetNeighbor(dir);
            steps++;
        }

        Assert.AreEqual(end, pos);
        Assert.AreEqual(distance, steps);
    }

    [TestMethod]
    public void ApproxDirectionPointsTowardsTarget()
    {
        var start = new Position(0, 0);
        var end = new Position(10, 4);
        var dir = MapGeometry.GetApproxDirection(start, end);

        // Moving in the approximate direction must reduce the distance.
        var next = start.GetNeighbor(dir);
        Assert.IsTrue(next.GetDistanceTo(end) < start.GetDistanceTo(end));
    }

    [TestMethod]
    public void GridToScreenAndBack()
    {
        var pos = new Position(4, 7);
        var (x, y) = MapGeometry.GridToScreen(pos);

        // screenX = 16x - 8y, screenY = 16y
        Assert.AreEqual(4 * 16f - 7 * 8f, x, 0.001f);
        Assert.AreEqual(7 * 16f, y, 0.001f);

        var back = MapGeometry.ScreenToGrid(x, y);
        Assert.AreEqual(pos, back);
    }

    [TestMethod]
    public void GridToScreenAccountsForHeight()
    {
        var pos = new Position(2, 3);
        var (x, y) = MapGeometry.GridToScreen(pos, height: 5f);
        Assert.AreEqual(3 * 16f - 5 * 2f, y, 0.001f);
    }
}
