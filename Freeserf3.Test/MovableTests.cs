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
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier);
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
        var serf = new Serf(0, new Position(0, 0), SerfType.Carrier);
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
