/*
 * TransportTests.cs - Tests for the flag/road transport network
 */

using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class TransportTests
{
    [TestMethod]
    public void FlagQueuesGoods()
    {
        var flag = new Flag(new Position(2, 2), 0);

        Assert.AreEqual(0, flag.Count);
        flag.AddGood(Material.Plank);
        flag.AddGood(Material.Stone);

        Assert.AreEqual(2, flag.Count);
        Assert.AreEqual(Material.Plank, flag.TakeGood());
        Assert.AreEqual(Material.Stone, flag.TakeGood());
        Assert.IsNull(flag.TakeGood());
    }

    [TestMethod]
    public void RoadTransportsGoodsToEndFlag()
    {
        var start = new Flag(new Position(0, 0), 0);
        var end = new Flag(new Position(4, 0), 0);
        var path = new List<Position>
        {
            new(1, 0),
            new(2, 0),
            new(3, 0),
            new(4, 0)
        };
        var road = new Road(start, end, path);

        road.SendGood(Material.Plank);
        Assert.AreEqual(1, road.TransitCount);
        Assert.AreEqual(0, end.Count);

        // 4 steps to reach the end flag.
        for (int i = 0; i < 4; i++)
            road.Tick();

        Assert.AreEqual(0, road.TransitCount);
        Assert.AreEqual(1, end.Count);
        Assert.AreEqual(Material.Plank, end.TakeGood());
    }

    [TestMethod]
    public void RoadTransportsMultipleGoods()
    {
        var start = new Flag(new Position(0, 0), 0);
        var end = new Flag(new Position(2, 0), 0);
        var road = new Road(start, end, new List<Position> { new(1, 0), new(2, 0) });

        road.SendGood(Material.Plank);

        // First good arrives after 2 ticks.
        road.Tick();
        road.Tick();
        Assert.AreEqual(1, end.Count);
        Assert.AreEqual(Material.Plank, end.TakeGood());

        // A second good sent later arrives after 2 more ticks.
        road.SendGood(Material.Stone);
        road.Tick();
        road.Tick();
        Assert.AreEqual(1, end.Count);
        Assert.AreEqual(Material.Stone, end.TakeGood());
    }

    [TestMethod]
    public void NetworkConnectsFlagsWithRoad()
    {
        var map = new Map(16);
        var network = new TransportNetwork(map);

        var flagA = network.AddFlag(new Position(1, 1), 0);
        var flagB = network.AddFlag(new Position(5, 1), 0);

        var road = network.Connect(flagA, flagB);
        Assert.IsNotNull(road);
        Assert.AreEqual(flagA, road!.Start);
        Assert.AreEqual(flagB, road.End);
        Assert.AreEqual(4, road.Length);
    }

    [TestMethod]
    public void NetworkReturnsNullWhenNoPath()
    {
        var map = new Map(8);
        var network = new TransportNetwork(map);

        var flagA = network.AddFlag(new Position(0, 0), 0);
        var flagB = network.AddFlag(new Position(7, 7), 0);

        // Block everything except the start.
        var road = network.Connect(flagA, flagB, (m, pos) => pos == new Position(0, 0));
        Assert.IsNull(road);
    }

    [TestMethod]
    public void NetworkTickMovesGoodsBetweenFlags()
    {
        var map = new Map(16);
        var network = new TransportNetwork(map);

        var flagA = network.AddFlag(new Position(0, 0), 0);
        var flagB = network.AddFlag(new Position(3, 0), 0);
        var road = network.Connect(flagA, flagB);
        Assert.IsNotNull(road);

        flagA.AddGood(Material.Trunk);
        road!.SendGood(flagA.TakeGood()!.Value);

        for (int i = 0; i < 3; i++)
            network.Tick();

        Assert.AreEqual(1, flagB.Count);
        Assert.AreEqual(Material.Trunk, flagB.TakeGood());
    }

    [TestMethod]
    public void GetFlagFindsByPosition()
    {
        var map = new Map(16);
        var network = new TransportNetwork(map);
        var flag = network.AddFlag(new Position(3, 4), 1);

        Assert.AreEqual(flag, network.GetFlag(new Position(3, 4)));
        Assert.IsNull(network.GetFlag(new Position(5, 5)));
    }
}
