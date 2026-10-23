/*
 * InventoryTests.cs - Tests for the goods stock
 */

using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class InventoryTests
{
    [TestMethod]
    public void NewInventoryIsEmpty()
    {
        var inventory = new Inventory();

        Assert.IsTrue(inventory.IsEmpty);
        Assert.AreEqual(0, inventory.TotalCount);
        Assert.AreEqual(0, inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void AddAndGet()
    {
        var inventory = new Inventory();

        inventory.Add(Material.Plank, 3);
        inventory.Add(Material.Stone, 2);

        Assert.AreEqual(3, inventory.Get(Material.Plank));
        Assert.AreEqual(2, inventory.Get(Material.Stone));
        Assert.AreEqual(5, inventory.TotalCount);
        Assert.IsFalse(inventory.IsEmpty);
    }

    [TestMethod]
    public void RemoveReducesCount()
    {
        var inventory = new Inventory();
        inventory.Add(Material.Iron, 5);

        Assert.IsTrue(inventory.Remove(Material.Iron, 2));
        Assert.AreEqual(3, inventory.Get(Material.Iron));
    }

    [TestMethod]
    public void RemoveFailsWhenInsufficient()
    {
        var inventory = new Inventory();
        inventory.Add(Material.Iron, 2);

        Assert.IsFalse(inventory.Remove(Material.Iron, 3));
        Assert.AreEqual(2, inventory.Get(Material.Iron), "Stock must be unchanged on failed remove.");
    }

    [TestMethod]
    public void RemoveFailsForMissingMaterial()
    {
        var inventory = new Inventory();
        Assert.IsFalse(inventory.Remove(Material.Coal));
    }

    [TestMethod]
    public void SetOverwritesCount()
    {
        var inventory = new Inventory();
        inventory.Add(Material.Bread, 4);
        inventory.Set(Material.Bread, 9);

        Assert.AreEqual(9, inventory.Get(Material.Bread));
    }

    [TestMethod]
    public void WeaponAndToolCounts()
    {
        var inventory = new Inventory();
        inventory.Add(Material.Sword, 2);
        inventory.Add(Material.Bow, 1);
        inventory.Add(Material.Axe, 3);
        inventory.Add(Material.Plank, 10);

        Assert.AreEqual(3, inventory.WeaponCount);
        Assert.AreEqual(3, inventory.ToolCount);
    }

    [TestMethod]
    public void NonEmptyEnumeratesPositiveCounts()
    {
        var inventory = new Inventory();
        inventory.Add(Material.Fish, 2);
        inventory.Add(Material.Meat, 1);

        var items = inventory.NonEmpty().ToList();
        Assert.HasCount(2, items);
        Assert.IsTrue(items.Contains((Material.Fish, 2)));
        Assert.IsTrue(items.Contains((Material.Meat, 1)));
    }

    [TestMethod]
    public void RejectsNegativeCounts()
    {
        var inventory = new Inventory();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => inventory.Add(Material.Plank, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => inventory.Set(Material.Plank, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => inventory.Remove(Material.Plank, -1));
    }
}
