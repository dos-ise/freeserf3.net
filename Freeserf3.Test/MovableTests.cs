/*
 * MovableTests.cs - Tests for movables and settlers
 */

using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class MovableTests
{
    [TestMethod]
    public void SerfStartsIdle()
    {
        var serf = new Serf(0, new Position(2, 2), SerfType.Carrier);

        Assert.AreEqual(MovableState.Idle, serf.State);
        Assert.AreEqual(new Position(2, 2), serf.Position);
        Assert.IsFalse(serf.IsWalking);
        Assert.IsFalse(serf.IsCarrying);
    }

    [TestMethod]
    public void SerfWalksAlongPath()
    {
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier)
        {
            MoveInterval = 1
        };
        var path = new List<Position>
        {
            new(1, 0),
            new(2, 0),
            new(3, 0)
        };

        serf.SetPath(path);
        Assert.IsTrue(serf.IsWalking);

        serf.Tick();
        Assert.AreEqual(new Position(1, 0), serf.Position);

        serf.Tick();
        Assert.AreEqual(new Position(2, 0), serf.Position);

        serf.Tick();
        Assert.AreEqual(new Position(3, 0), serf.Position);
        Assert.AreEqual(MovableState.Idle, serf.State);
        Assert.AreEqual(new Position(3, 0), serf.Target);
    }

    [TestMethod]
    public void SerfTracksMovementProgress()
    {
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier)
        {
            MoveInterval = 10
        };
        serf.SetPath(new List<Position> { new(1, 0) });

        // Before moving, no previous position and no progress.
        Assert.IsNull(serf.PreviousPosition);
        Assert.AreEqual(0f, serf.MoveProgress);

        // Halfway through the cell the progress is 0.5.
        for (int i = 0; i < 5; i++)
            serf.Tick();
        Assert.AreEqual(0.5f, serf.MoveProgress);
        Assert.AreEqual(new Position(0, 0), serf.Position);

        // After MoveInterval ticks the settler moves to the next cell and
        // remembers where it came from.
        for (int i = 0; i < 5; i++)
            serf.Tick();
        Assert.AreEqual(new Position(1, 0), serf.Position);
        Assert.AreEqual(new Position(0, 0), serf.PreviousPosition);
        Assert.AreEqual(0f, serf.MoveProgress);
    }

    [TestMethod]
    public void SerfPicksUpAndDropsGoods()
    {
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier);

        serf.PickUp(Material.Plank);
        Assert.IsTrue(serf.IsCarrying);
        Assert.AreEqual(Material.Plank, serf.CarriedGood);

        var dropped = serf.Drop();
        Assert.AreEqual(Material.Plank, dropped);
        Assert.IsFalse(serf.IsCarrying);
        Assert.IsNull(serf.CarriedGood);
    }

    [TestMethod]
    public void SetPathAcceptsStartExclusivePath()
    {
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier)
        {
            MoveInterval = 1
        };
        var path = new List<Position> { new(1, 0), new(2, 0) };

        serf.SetPath(path);
        Assert.IsTrue(serf.IsWalking);

        serf.Tick();
        Assert.AreEqual(new Position(1, 0), serf.Position);
    }

    [TestMethod]
    public void StopHaltsMovement()
    {
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier);
        serf.SetPath(new List<Position> { new(1, 0), new(2, 0) });

        serf.Stop();
        Assert.AreEqual(MovableState.Idle, serf.State);
        Assert.IsNull(serf.Path);

        serf.Tick();
        Assert.AreEqual(new Position(0, 0), serf.Position);
    }

    [TestMethod]
    public void WorkerSerfHasProfession()
    {
        var serf = new Serf(0, new Position(0, 0), SerfType.Worker)
        {
            Profession = WorkerType.Sawmiller
        };

        Assert.AreEqual(SerfType.Worker, serf.Type);
        Assert.AreEqual(WorkerType.Sawmiller, serf.Profession);
    }
}
