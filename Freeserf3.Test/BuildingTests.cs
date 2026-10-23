/*
 * BuildingTests.cs - Tests for building definitions and the state machine
 */

using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class BuildingTests
{
    [TestMethod]
    public void AllBuildingTypesHaveDefinitions()
    {
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            var def = BuildingDefinition.Get(type);
            Assert.IsNotNull(def);
            Assert.AreEqual(type, def.Type);
        }
    }

    [TestMethod]
    public void ConstructionCostsMatchOriginalData()
    {
        // From the settlers-remake building XMLs (roman).
        Assert.AreEqual(2, BuildingDefinition.Get(BuildingType.Lumberjack).GetConstructionCost(Material.Stone));
        Assert.AreEqual(2, BuildingDefinition.Get(BuildingType.Lumberjack).GetConstructionCost(Material.Plank));
        Assert.AreEqual(4, BuildingDefinition.Get(BuildingType.Lumberjack).TotalConstructionCost);

        Assert.AreEqual(3, BuildingDefinition.Get(BuildingType.Sawmill).GetConstructionCost(Material.Plank));
        Assert.AreEqual(4, BuildingDefinition.Get(BuildingType.Sawmill).GetConstructionCost(Material.Stone));
        Assert.AreEqual(7, BuildingDefinition.Get(BuildingType.Sawmill).TotalConstructionCost);

        Assert.AreEqual(4, BuildingDefinition.Get(BuildingType.Ironmelt).GetConstructionCost(Material.Plank));
        Assert.AreEqual(6, BuildingDefinition.Get(BuildingType.Ironmelt).GetConstructionCost(Material.Stone));
    }

    [TestMethod]
    public void ProductionRecipes()
    {
        var sawmill = BuildingDefinition.Get(BuildingType.Sawmill);
        Assert.AreEqual(Material.Trunk, sawmill.Inputs.Single());
        Assert.AreEqual(Material.Plank, sawmill.Output);
        Assert.IsTrue(sawmill.IsProducer);
        Assert.IsTrue(sawmill.Consumes(Material.Trunk));

        var ironmelt = BuildingDefinition.Get(BuildingType.Ironmelt);
        Assert.IsTrue(ironmelt.Consumes(Material.Coal));
        Assert.IsTrue(ironmelt.Consumes(Material.IronOre));
        Assert.AreEqual(Material.Iron, ironmelt.Output);

        var lumberjack = BuildingDefinition.Get(BuildingType.Lumberjack);
        Assert.AreEqual(Material.Trunk, lumberjack.Output);
        Assert.IsFalse(lumberjack.Consumes(Material.Trunk));
    }

    [TestMethod]
    public void MinesAndMilitaryFlags()
    {
        Assert.IsTrue(BuildingDefinition.Get(BuildingType.Coalmine).IsMine);
        Assert.IsTrue(BuildingDefinition.Get(BuildingType.Ironmine).IsMine);
        Assert.IsTrue(BuildingDefinition.Get(BuildingType.Goldmine).IsMine);
        Assert.IsFalse(BuildingDefinition.Get(BuildingType.Sawmill).IsMine);

        Assert.IsTrue(BuildingDefinition.Get(BuildingType.Tower).IsMilitary);
        Assert.IsTrue(BuildingDefinition.Get(BuildingType.Castle).IsMilitary);
        Assert.IsFalse(BuildingDefinition.Get(BuildingType.Farm).IsMilitary);

        Assert.IsTrue(BuildingDefinition.Get(BuildingType.SmallLivinghouse).IsLivinghouse);
        Assert.IsFalse(BuildingDefinition.Get(BuildingType.Farm).IsLivinghouse);
    }

    [TestMethod]
    public void WorkerTypes()
    {
        Assert.AreEqual(WorkerType.Lumberjack, BuildingDefinition.Get(BuildingType.Lumberjack).Worker);
        Assert.AreEqual(WorkerType.Sawmiller, BuildingDefinition.Get(BuildingType.Sawmill).Worker);
        Assert.AreEqual(WorkerType.Miner, BuildingDefinition.Get(BuildingType.Coalmine).Worker);
        Assert.AreEqual(WorkerType.Melter, BuildingDefinition.Get(BuildingType.Ironmelt).Worker);
        Assert.AreEqual(WorkerType.Smith, BuildingDefinition.Get(BuildingType.Toolsmith).Worker);
        Assert.AreEqual(WorkerType.None, BuildingDefinition.Get(BuildingType.Castle).Worker);
    }

    [TestMethod]
    public void BuildingStartsAsConstructionSite()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);

        Assert.AreEqual(BuildingState.ConstructionSite, building.State);
        Assert.AreEqual(0, building.ConstructionProgress);
        Assert.IsFalse(building.HasWorker);
    }

    [TestMethod]
    public void ConstructionCompletesToActive()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);

        building.AddConstructionProgress(40);
        Assert.AreEqual(BuildingState.ConstructionSite, building.State);
        Assert.AreEqual(40, building.ConstructionProgress);

        building.AddConstructionProgress(60);
        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.AreEqual(100, building.ConstructionProgress);
    }

    [TestMethod]
    public void ConstructionProgressIsCapped()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);

        building.AddConstructionProgress(150);
        Assert.AreEqual(100, building.ConstructionProgress);
        Assert.AreEqual(BuildingState.Active, building.State);
    }

    [TestMethod]
    public void ProductionRequiresWorkerAndInputs()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);
        building.AddConstructionProgress(100);

        // No worker -> cannot produce.
        Assert.IsFalse(building.CanProduce);

        building.AssignWorker(WorkerType.Sawmiller);
        // No input -> cannot produce.
        Assert.IsFalse(building.CanProduce);

        building.Input.Add(Material.Trunk);
        Assert.IsTrue(building.CanProduce);
    }

    [TestMethod]
    public void ProductionCycleConsumesInputAndProducesOutput()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);
        building.AddConstructionProgress(100);
        building.AssignWorker(WorkerType.Sawmiller);
        building.Input.Add(Material.Trunk, 3);

        int productionTime = building.Definition.ProductionTime;

        // Run almost to completion.
        for (int i = 0; i < productionTime - 1; i++)
            building.Tick();

        Assert.AreEqual(0, building.Output.Get(Material.Plank));
        Assert.AreEqual(3, building.Input.Get(Material.Trunk));

        // The final tick completes the cycle.
        building.Tick();
        Assert.AreEqual(1, building.Output.Get(Material.Plank));
        Assert.AreEqual(2, building.Input.Get(Material.Trunk));
    }

    [TestMethod]
    public void ProductionStopsWithoutInput()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);
        building.AddConstructionProgress(100);
        building.AssignWorker(WorkerType.Sawmiller);
        building.Input.Add(Material.Trunk, 1);

        int productionTime = building.Definition.ProductionTime;
        for (int i = 0; i < productionTime; i++)
            building.Tick();

        Assert.AreEqual(1, building.Output.Get(Material.Plank));
        Assert.AreEqual(0, building.Input.Get(Material.Trunk));

        // No more input -> production stops.
        for (int i = 0; i < productionTime; i++)
            building.Tick();

        Assert.AreEqual(1, building.Output.Get(Material.Plank));
        Assert.AreEqual(0, building.ProductionProgress);
    }

    [TestMethod]
    public void ProductionStopsWithoutWorker()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);
        building.AddConstructionProgress(100);
        building.AssignWorker(WorkerType.Sawmiller);
        building.Input.Add(Material.Trunk, 1);

        building.RemoveWorker();
        for (int i = 0; i < building.Definition.ProductionTime; i++)
            building.Tick();

        Assert.AreEqual(0, building.Output.Get(Material.Plank));
        Assert.AreEqual(1, building.Input.Get(Material.Trunk));
    }

    [TestMethod]
    public void DestroyStopsProduction()
    {
        var building = new Building(BuildingType.Sawmill, new Position(5, 5), 0);
        building.AddConstructionProgress(100);
        building.AssignWorker(WorkerType.Sawmiller);
        building.Input.Add(Material.Trunk, 1);

        building.Destroy();
        Assert.AreEqual(BuildingState.Destroyed, building.State);

        for (int i = 0; i < building.Definition.ProductionTime; i++)
            building.Tick();

        Assert.AreEqual(0, building.Output.Get(Material.Plank));
    }

    [TestMethod]
    public void ProductionChainLumberjackToSawmill()
    {
        // Lumberjack produces trunks, sawmill turns them into planks.
        var lumberjack = new Building(BuildingType.Lumberjack, new Position(2, 2), 0);
        lumberjack.AddConstructionProgress(100);
        lumberjack.AssignWorker(WorkerType.Lumberjack);

        var sawmill = new Building(BuildingType.Sawmill, new Position(5, 5), 0);
        sawmill.AddConstructionProgress(100);
        sawmill.AssignWorker(WorkerType.Sawmiller);

        // Run the lumberjack until it produced a trunk, then feed the sawmill.
        int ticks = 0;
        while (lumberjack.Output.Get(Material.Trunk) == 0 && ticks < 10000)
        {
            lumberjack.Tick();
            ticks++;
        }

        Assert.AreEqual(1, lumberjack.Output.Get(Material.Trunk));
        Assert.IsTrue(ticks <= lumberjack.Definition.ProductionTime);

        sawmill.Input.Add(Material.Trunk, 1);
        for (int i = 0; i < sawmill.Definition.ProductionTime; i++)
            sawmill.Tick();

        Assert.AreEqual(1, sawmill.Output.Get(Material.Plank));
    }
}
