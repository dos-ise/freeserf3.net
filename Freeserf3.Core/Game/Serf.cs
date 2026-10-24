/*
 * Serf.cs - The settler unit (carrier, builder, soldier, worker)
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The roles a settler can take.</summary>
public enum SerfType
{
    /// <summary>Transports goods between flags.</summary>
    Carrier,

    /// <summary>Builds buildings.</summary>
    Builder,

    /// <summary>Fights (military).</summary>
    Soldier,

    /// <summary>Works in a production building.</summary>
    Worker,

    /// <summary>Collects resources from map stacks and carries them to the stock.</summary>
    Collector,

    /// <summary>Gathers map resources (cuts trees, mines stones) for a production building.</summary>
    ResourceWorker
}

/// <summary>The states of a resource collector.</summary>
public enum CollectorState
{
    /// <summary>Looking for the nearest resource stack.</summary>
    LookingForStack,

    /// <summary>Walking to the resource stack.</summary>
    WalkingToStack,

    /// <summary>Walking to the player's stock flag with a carried good.</summary>
    WalkingToStock,

    /// <summary>Pausing before looking for the next stack.</summary>
    Pausing
}

/// <summary>The states of a building builder.</summary>
public enum BuilderState
{
    /// <summary>Walking to the construction site.</summary>
    WalkingToSite,

    /// <summary>At the construction site, building.</summary>
    Building,

    /// <summary>Walking away from the finished building.</summary>
    WalkingAway
}

/// <summary>The states of a resource worker (lumberjack, stonecutter).</summary>
public enum ResourceWorkerState
{
    /// <summary>Looking for a tree or stone in the work radius.</summary>
    LookingForResource,

    /// <summary>Walking to the found tree or stone.</summary>
    WalkingToResource,

    /// <summary>Cutting the tree or mining the stone.</summary>
    Working,

    /// <summary>Walking back to the building after gathering.</summary>
    WalkingHome,

    /// <summary>Inside the house after dropping the gathered good (not rendered).</summary>
    InsideHome,

    /// <summary>Pausing before looking for the next resource.</summary>
    Pausing
}

/// <summary>The states of a soldier.</summary>
public enum SoldierState
{
    /// <summary>Standing at the building, waiting.</summary>
    Idle,

    /// <summary>Walking to a patrol point.</summary>
    Patrolling,

    /// <summary>Walking back to the building.</summary>
    Returning,

    /// <summary>Moving toward or fighting an enemy.</summary>
    Attacking
}

/// <summary>
/// A settler on the map. Carries goods, walks along paths and works in
/// buildings.
/// </summary>
public sealed class Serf : Movable
{
    public Serf(int player, Position position, SerfType type)
        : base(player, position)
    {
        Type = type;
    }

    /// <summary>The role of this settler.</summary>
    public SerfType Type { get; set; }

    /// <summary>The profession when working in a building.</summary>
    public WorkerType Profession { get; set; } = WorkerType.None;

    /// <summary>Collector state (only used by collectors).</summary>
    public CollectorState CollectorState { get; set; } = CollectorState.LookingForStack;

    /// <summary>The resource stack the collector is heading to.</summary>
    public ResourceStack? CollectorTarget { get; set; }

    /// <summary>Builder state (only used by builders).</summary>
    public BuilderState BuilderState { get; set; } = BuilderState.WalkingToSite;

    /// <summary>The construction site the builder is working on.</summary>
    public Building? ConstructionSite { get; set; }

    /// <summary>Ticks spent on the current build action.</summary>
    public int BuildTicks { get; set; }

    /// <summary>Resource worker state (only used by resource workers).</summary>
    public ResourceWorkerState ResourceState { get; set; } = ResourceWorkerState.LookingForResource;

    /// <summary>The production building the resource worker works in.</summary>
    public Building? Workplace { get; set; }

    /// <summary>The tree or stone the resource worker is heading to.</summary>
    public Position? ResourceTarget { get; set; }

    /// <summary>Ticks spent on the current cut/mine action.</summary>
    public int WorkTicks { get; set; }

    /// <summary>Soldier state (only used by soldiers).</summary>
    public SoldierState SoldierState { get; set; } = SoldierState.Idle;

    /// <summary>The military building the soldier is stationed in.</summary>
    public Building? Barracks { get; set; }

    /// <summary>
    /// Whether the soldier has been sent on an attack wave and left its
    /// building. Deployed soldiers keep fighting even if their barracks
    /// is destroyed.
    /// </summary>
    public bool IsDeployed { get; set; }

    /// <summary>
    /// Whether the current attack target was ordered by the player. Ordered
    /// targets are not dropped when they are far away.
    /// </summary>
    public bool OrderedAttack { get; set; }

    /// <summary>
    /// The military building the soldier was ordered to garrison in. When
    /// the soldier arrives, it is stationed in the building.
    /// </summary>
    public Building? GarrisonTarget { get; set; }

    /// <summary>The weapon the soldier carries (sword, spear or bow).</summary>
    public Material Weapon { get; set; } = Material.Sword;

    /// <summary>Ticks spent idle at the building.</summary>
    public int SoldierIdleTicks { get; set; }

    /// <summary>The enemy the soldier is currently fighting, or null.</summary>
    public Serf? AttackTarget { get; set; }

    /// <summary>The enemy building the soldier is attacking, or null.</summary>
    public Building? AttackBuilding { get; set; }

    /// <summary>
    /// The position a soldier is ordered to march to (set by the AI for
    /// attack waves). Cleared when the soldier arrives or finds an enemy.
    /// </summary>
    public Position? MarchTarget { get; set; }

    /// <summary>Ticks since the soldier's last attack.</summary>
    public int AttackTicks { get; set; }

    /// <summary>
    /// Ticks spent in the attack animation (drives the ACTION1 frames).
    /// </summary>
    public int AttackAnimationTicks { get; set; }

    /// <summary>The soldier's remaining health.</summary>
    public float Health { get; set; } = 100f;

    /// <summary>Whether the soldier has been killed in combat.</summary>
    public bool IsDead { get; set; }

    /// <summary>The good currently carried, or null.</summary>
    public Material? CarriedGood { get; set; }

    /// <summary>Whether the settler carries a good.</summary>
    public bool IsCarrying => CarriedGood.HasValue;

    /// <summary>
    /// Ticks per map cell walked (1 = one cell per tick). The default
    /// matches the original game's walking speed (~0.4s per cell at
    /// 60 ticks/sec).
    /// </summary>
    public const int DefaultMoveInterval = 25;

    /// <summary>Ticks spent idle (used by the game to pause between walks).</summary>
    public int IdleTicks { get; set; }

    /// <summary>Picks up a good.</summary>
    public void PickUp(Material material)
    {
        CarriedGood = material;
    }

    /// <summary>Drops the carried good and returns it.</summary>
    public Material? Drop()
    {
        var good = CarriedGood;
        CarriedGood = null;
        return good;
    }

    /// <summary>Advances the settler by one tick.</summary>
    public override void Tick()
    {
        if (AdvanceMove())
            WalkAlongPath();
    }
}
