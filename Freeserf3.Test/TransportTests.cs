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
    public void FlagTakesSpecificGood()
    {
        var flag = new Flag(new Position(2, 2), 0);
        flag.AddGood(Material.Plank);
        flag.AddGood(Material.Stone);
        flag.AddGood(Material.Plank);

        Assert.IsTrue(flag.Contains(Material.Stone));
        Assert.AreEqual(Material.Stone, flag.TakeGood(Material.Stone));
        Assert.IsFalse(flag.Contains(Material.Stone));
        Assert.AreEqual(2, flag.Count);
        Assert.IsNull(flag.TakeGood(Material.Trunk));
    }

    [TestMethod]
    public void CarrierTransportsGoodToEndFlag()
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
        var road = new Road(start, end, path, 0);

        start.AddGood(Material.Plank);

        // Tick 1: the carrier picks up the good and starts walking.
        road.Tick();
        Assert.IsTrue(road.IsCarrying);
        Assert.AreEqual(new Position(0, 0), road.Carrier.Position);

        // 4 more ticks to walk the 4 path cells and drop at the end flag.
        for (int i = 0; i < 4; i++)
            road.Tick();

        Assert.IsFalse(road.IsCarrying);
        Assert.AreEqual(1, end.Count);
        Assert.AreEqual(Material.Plank, end.TakeGood());
    }

    [TestMethod]
    public void CarrierWalksBackAndForth()
    {
        var start = new Flag(new Position(0, 0), 0);
        var end = new Flag(new Position(2, 0), 0);
        var road = new Road(start, end, new List<Position> { new(1, 0), new(2, 0) }, 0);

        start.AddGood(Material.Plank);

        // Pick up and walk to the end (2 cells).
        road.Tick();
        road.Tick();
        road.Tick();
        Assert.AreEqual(1, end.Count);
        Assert.AreEqual(Material.Plank, end.TakeGood());

        // The carrier turns around and walks back to the start.
        road.Tick();
        road.Tick();
        road.Tick();
        Assert.AreEqual(new Position(0, 0), road.Carrier.Position);
        Assert.AreEqual(CarrierState.IdleAtStart, road.CarrierState);
    }

    [TestMethod]
    public void NetworkConnectsFlagsWithRoad()
    {
        var map = new Map(16);
        var network = new TransportNetwork(map);

        var flagA = network.AddFlag(new Position(1, 1), 0);
        var flagB = network.AddFlag(new Position(5, 1), 0);

        var road = network.Connect(flagA, flagB, 0);
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
        var road = network.Connect(flagA, flagB, 0, (m, pos) => pos == new Position(0, 0));
        Assert.IsNull(road);
    }

    [TestMethod]
    public void NetworkTickMovesGoodsBetweenFlags()
    {
        var map = new Map(16);
        var network = new TransportNetwork(map);

        var flagA = network.AddFlag(new Position(0, 0), 0);
        var flagB = network.AddFlag(new Position(3, 0), 0);
        var road = network.Connect(flagA, flagB, 0);
        Assert.IsNotNull(road);

        flagA.AddGood(Material.Trunk);

        // Pick up (1) + walk 3 cells = 4 ticks.
        for (int i = 0; i < 4; i++)
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

    [TestMethod]
    public void StockFlagIsSet()
    {
        var map = new Map(16);
        var network = new TransportNetwork(map);
        var stock = network.AddStockFlag(new Position(2, 2), 0);

        Assert.AreEqual(stock, network.StockFlag);
        Assert.AreEqual(0, stock.Player);
    }
}
