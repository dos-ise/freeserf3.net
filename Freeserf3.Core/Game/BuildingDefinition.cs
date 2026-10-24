/*
 * BuildingDefinition.cs - Static definitions of all buildings
 *
 * Building costs, worker types and production recipes are taken from the
 * settlers-remake (MIT) building XML files (roman civilisation).
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// Static data of one building type: construction costs, required worker,
/// production recipe and flags.
/// </summary>
public sealed class BuildingDefinition
{
    /// <summary>Production duration in ticks (1 tick = 1/60 s).</summary>
    public const int DefaultProductionTime = 1200;

    public BuildingDefinition(
        BuildingType type,
        WorkerType worker,
        (Material Material, int Count)[] constructionCosts,
        Material[]? inputs = null,
        Material? output = null,
        bool isMine = false,
        int workRadius = 0,
        bool isMilitary = false,
        bool isLivinghouse = false,
        bool isResourceProducer = false,
        int productionTime = DefaultProductionTime,
        int soldierCapacity = 0,
        int viewDistance = 0,
        Material? tool = null,
        int livingCapacity = 0)
    {
        Type = type;
        Worker = worker;
        ConstructionCosts = constructionCosts;
        Inputs = inputs ?? Array.Empty<Material>();
        Output = output;
        IsMine = isMine;
        WorkRadius = workRadius;
        IsMilitary = isMilitary;
        IsLivinghouse = isLivinghouse;
        IsResourceProducer = isResourceProducer;
        ProductionTime = productionTime;
        SoldierCapacity = soldierCapacity;
        ViewDistance = viewDistance;
        Tool = tool;
        LivingCapacity = livingCapacity;
    }

    public BuildingType Type { get; }
    public WorkerType Worker { get; }
    public (Material Material, int Count)[] ConstructionCosts { get; }
    public Material[] Inputs { get; }
    public Material? Output { get; }
    public bool IsMine { get; }
    public int WorkRadius { get; }
    public bool IsMilitary { get; }
    public bool IsLivinghouse { get; }

    /// <summary>
    /// The tool the worker needs to do the job, or null if the profession
    /// works without a tool (like settlers-remake EMovableType.getTool).
    /// The tool is consumed from the player's stock when the worker is
    /// assigned.
    /// </summary>
    public Material? Tool { get; }

    /// <summary>
    /// Number of settlers a living house can hold (like settlers-remake
    /// SpawnBuilding PRODUCE_LIMIT). Only used by living houses.
    /// </summary>
    public int LivingCapacity { get; }

    /// <summary>
    /// Whether the building gathers a map resource (trees, stones) instead
    /// of producing from material inputs. Its worker drives the production.
    /// </summary>
    public bool IsResourceProducer { get; }
    public int ProductionTime { get; }

    /// <summary>Number of soldiers a military building can hold.</summary>
    public int SoldierCapacity { get; }

    /// <summary>
    /// View distance for the fog of war (settlers-remake building XML
    /// viewdistance attribute). Only military buildings see far.
    /// </summary>
    public int ViewDistance { get; }

    /// <summary>Total number of construction materials needed.</summary>
    public int TotalConstructionCost
    {
        get
        {
            int total = 0;
            foreach (var (_, count) in ConstructionCosts)
                total += count;
            return total;
        }
    }

    /// <summary>Whether the building produces a good.</summary>
    public bool IsProducer => Output.HasValue;

    /// <summary>Whether the building needs a worker to operate.</summary>
    public bool NeedsWorker => Worker != WorkerType.None;

    /// <summary>Gets the amount of a material required for construction.</summary>
    public int GetConstructionCost(Material material)
    {
        foreach (var (m, count) in ConstructionCosts)
        {
            if (m == material)
                return count;
        }
        return 0;
    }

    /// <summary>Whether the building consumes the given material.</summary>
    public bool Consumes(Material material)
    {
        foreach (var input in Inputs)
        {
            if (input == material)
                return true;
        }
        return false;
    }

    static readonly Dictionary<BuildingType, BuildingDefinition> definitions = CreateDefinitions();

    /// <summary>Gets the definition of a building type.</summary>
    public static BuildingDefinition Get(BuildingType type)
    {
        return definitions[type];
    }

    /// <summary>All building definitions.</summary>
    public static IEnumerable<BuildingDefinition> All => definitions.Values;

    static Dictionary<BuildingType, BuildingDefinition> CreateDefinitions()
    {
        var defs = new Dictionary<BuildingType, BuildingDefinition>();

        void Add(BuildingDefinition def) => defs[def.Type] = def;

        // --- Raw material production ---
        Add(new BuildingDefinition(BuildingType.Stonecutter, WorkerType.Stonecutter,
            new[] { (Material.Plank, 2), (Material.Stone, 3) },
            output: Material.Stone, workRadius: 20, isResourceProducer: true, productionTime: 900,
            tool: Material.Pick));

        Add(new BuildingDefinition(BuildingType.Forester, WorkerType.Forester,
            new[] { (Material.Stone, 1), (Material.Plank, 3) },
            workRadius: 18, isResourceProducer: true, productionTime: 900));

        Add(new BuildingDefinition(BuildingType.Lumberjack, WorkerType.Lumberjack,
            new[] { (Material.Stone, 2), (Material.Plank, 2) },
            output: Material.Trunk, workRadius: 30, isResourceProducer: true, productionTime: 900,
            tool: Material.Axe));

        Add(new BuildingDefinition(BuildingType.Sawmill, WorkerType.Sawmiller,
            new[] { (Material.Plank, 3), (Material.Stone, 4) },
            inputs: new[] { Material.Trunk }, output: Material.Plank, productionTime: 900,
            tool: Material.Saw));

        Add(new BuildingDefinition(BuildingType.CharcoalBurner, WorkerType.CharcoalBurner,
            new[] { (Material.Stone, 4), (Material.Plank, 3) },
            inputs: new[] { Material.Plank }, output: Material.Coal, productionTime: 1200));

        // --- Mines ---
        Add(new BuildingDefinition(BuildingType.Coalmine, WorkerType.Miner,
            new[] { (Material.Plank, 4), (Material.Stone, 1) },
            inputs: new[] { Material.Bread, Material.Meat, Material.Fish },
            output: Material.Coal, isMine: true, workRadius: 3, productionTime: 1200,
            tool: Material.Pick));

        Add(new BuildingDefinition(BuildingType.Ironmine, WorkerType.Miner,
            new[] { (Material.Plank, 4), (Material.Stone, 1) },
            inputs: new[] { Material.Bread, Material.Meat, Material.Fish },
            output: Material.IronOre, isMine: true, workRadius: 3, productionTime: 1200,
            tool: Material.Pick));

        Add(new BuildingDefinition(BuildingType.Goldmine, WorkerType.Miner,
            new[] { (Material.Plank, 5), (Material.Stone, 1) },
            inputs: new[] { Material.Bread, Material.Meat, Material.Fish },
            output: Material.GoldOre, isMine: true, workRadius: 3, productionTime: 1200,
            tool: Material.Pick));

        Add(new BuildingDefinition(BuildingType.Sulfurmine, WorkerType.Miner,
            new[] { (Material.Plank, 3), (Material.Stone, 2) },
            inputs: new[] { Material.Bread, Material.Meat, Material.Fish },
            output: Material.Sulfur, isMine: true, workRadius: 3, productionTime: 1200,
            tool: Material.Pick));

        Add(new BuildingDefinition(BuildingType.Gemsmine, WorkerType.Miner,
            new[] { (Material.Plank, 3), (Material.Stone, 2) },
            inputs: new[] { Material.Bread, Material.Meat, Material.Fish },
            output: Material.Gems, isMine: true, workRadius: 3, productionTime: 1200,
            tool: Material.Pick));

        // --- Melts and smiths ---
        Add(new BuildingDefinition(BuildingType.Ironmelt, WorkerType.Melter,
            new[] { (Material.Plank, 4), (Material.Stone, 6) },
            inputs: new[] { Material.Coal, Material.IronOre }, output: Material.Iron,
            productionTime: 1500));

        Add(new BuildingDefinition(BuildingType.Goldmelt, WorkerType.Melter,
            new[] { (Material.Plank, 4), (Material.Stone, 6) },
            inputs: new[] { Material.Coal, Material.GoldOre }, output: Material.Gold,
            productionTime: 1500));

        // The smiths produce a configurable tool/weapon (defaults below).
        Add(new BuildingDefinition(BuildingType.Toolsmith, WorkerType.Smith,
            new[] { (Material.Plank, 3), (Material.Stone, 5) },
            inputs: new[] { Material.Iron, Material.Coal }, output: Material.Hammer,
            productionTime: 1500, tool: Material.Hammer));

        Add(new BuildingDefinition(BuildingType.Weaponsmith, WorkerType.Smith,
            new[] { (Material.Plank, 5), (Material.Stone, 7) },
            inputs: new[] { Material.Iron, Material.Coal }, output: Material.Sword,
            productionTime: 1500, tool: Material.Hammer));

        // --- Food production ---
        Add(new BuildingDefinition(BuildingType.Farm, WorkerType.Farmer,
            new[] { (Material.Plank, 6), (Material.Stone, 6) },
            output: Material.Crop, workRadius: 6, productionTime: 1200,
            tool: Material.Scythe));

        Add(new BuildingDefinition(BuildingType.PigFarm, WorkerType.PigFarmer,
            new[] { (Material.Plank, 6), (Material.Stone, 6) },
            inputs: new[] { Material.Crop, Material.Water }, output: Material.Pig,
            productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.Mill, WorkerType.Miller,
            new[] { (Material.Plank, 3), (Material.Stone, 3) },
            inputs: new[] { Material.Crop }, output: Material.Flour, productionTime: 900));

        Add(new BuildingDefinition(BuildingType.Waterworks, WorkerType.Waterworker,
            new[] { (Material.Plank, 3), (Material.Stone, 4) },
            output: Material.Water, workRadius: 20, productionTime: 900));

        Add(new BuildingDefinition(BuildingType.Slaughterhouse, WorkerType.Slaughterer,
            new[] { (Material.Plank, 4), (Material.Stone, 4) },
            inputs: new[] { Material.Pig }, output: Material.Meat, productionTime: 900,
            tool: Material.Axe));

        Add(new BuildingDefinition(BuildingType.Baker, WorkerType.Baker,
            new[] { (Material.Plank, 4), (Material.Stone, 5) },
            inputs: new[] { Material.Flour, Material.Water }, output: Material.Bread,
            productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.Fisher, WorkerType.Fisherman,
            new[] { (Material.Stone, 2), (Material.Plank, 3) },
            output: Material.Fish, workRadius: 30, productionTime: 900,
            tool: Material.FishingRod));

        Add(new BuildingDefinition(BuildingType.Winegrower, WorkerType.Winegrower,
            new[] { (Material.Plank, 4), (Material.Stone, 4) },
            output: Material.Wine, workRadius: 4, productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.DonkeyFarm, WorkerType.DonkeyFarmer,
            new[] { (Material.Stone, 6), (Material.Plank, 6) },
            inputs: new[] { Material.Crop, Material.Water }, productionTime: 1200));

        // --- Civilisation-specific production ---
        Add(new BuildingDefinition(BuildingType.Brewery, WorkerType.Brewer,
            new[] { (Material.Stone, 5), (Material.Plank, 2) },
            inputs: new[] { Material.Crop, Material.Water }, output: Material.Keg,
            productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.RiceFarm, WorkerType.RiceFarmer,
            new[] { (Material.Stone, 3), (Material.Plank, 9) },
            output: Material.Rice, workRadius: 6, productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.Distillery, WorkerType.Distiller,
            new[] { (Material.Plank, 4), (Material.Stone, 3) },
            inputs: new[] { Material.Rice, Material.Coal }, output: Material.Liquor,
            productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.Beekeeping, WorkerType.Beekeeper,
            new[] { (Material.Plank, 6), (Material.Stone, 1) },
            output: Material.Honey, productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.MeadBrewery, WorkerType.MeadBrewer,
            new[] { (Material.Stone, 4), (Material.Plank, 6) },
            inputs: new[] { Material.Water, Material.Honey }, output: Material.Mead,
            productionTime: 1200));

        Add(new BuildingDefinition(BuildingType.Laboratory, WorkerType.Alchemist,
            new[] { (Material.Plank, 5), (Material.Stone, 1) },
            inputs: new[] { Material.Sulfur, Material.Gems }, output: Material.Iron,
            productionTime: 1500));

        // --- Living houses ---
        Add(new BuildingDefinition(BuildingType.SmallLivinghouse, WorkerType.None,
            new[] { (Material.Plank, 2), (Material.Stone, 3) }, isLivinghouse: true, livingCapacity: 10));

        Add(new BuildingDefinition(BuildingType.MediumLivinghouse, WorkerType.None,
            new[] { (Material.Plank, 5), (Material.Stone, 8) }, isLivinghouse: true, livingCapacity: 30));

        Add(new BuildingDefinition(BuildingType.BigLivinghouse, WorkerType.None,
            new[] { (Material.Plank, 15), (Material.Stone, 24) }, isLivinghouse: true, livingCapacity: 100));

        // --- Military buildings ---
        Add(new BuildingDefinition(BuildingType.LookoutTower, WorkerType.None,
            new[] { (Material.Stone, 2), (Material.Plank, 2) }, isMilitary: true, soldierCapacity: 1, viewDistance: 50));

        Add(new BuildingDefinition(BuildingType.Tower, WorkerType.None,
            new[] { (Material.Plank, 2), (Material.Stone, 3) }, isMilitary: true, soldierCapacity: 3, viewDistance: 38));

        Add(new BuildingDefinition(BuildingType.BigTower, WorkerType.None,
            new[] { (Material.Plank, 5), (Material.Stone, 5) }, isMilitary: true, soldierCapacity: 6, viewDistance: 40));

        Add(new BuildingDefinition(BuildingType.Castle, WorkerType.None,
            new[] { (Material.Stone, 12), (Material.Plank, 8) }, isMilitary: true, soldierCapacity: 12, viewDistance: 40));

        Add(new BuildingDefinition(BuildingType.Hospital, WorkerType.Healer,
            new[] { (Material.Plank, 3), (Material.Stone, 3) }, workRadius: 20));

        Add(new BuildingDefinition(BuildingType.Barrack, WorkerType.None,
            new[] { (Material.Stone, 4), (Material.Plank, 5) },
            inputs: new[] { Material.Sword, Material.Spear, Material.Bow }, isMilitary: true, soldierCapacity: 8));

        // --- Infrastructure ---
        Add(new BuildingDefinition(BuildingType.Dockyard, WorkerType.Dockworker,
            new[] { (Material.Plank, 3), (Material.Stone, 5) },
            inputs: new[] { Material.Plank, Material.Iron }, tool: Material.Hammer));

        Add(new BuildingDefinition(BuildingType.Harbor, WorkerType.None,
            new[] { (Material.Plank, 2), (Material.Stone, 4) }));

        Add(new BuildingDefinition(BuildingType.Stock, WorkerType.None,
            new[] { (Material.Plank, 2), (Material.Stone, 2) }));

        Add(new BuildingDefinition(BuildingType.MarketPlace, WorkerType.None,
            new[] { (Material.Plank, 1), (Material.Stone, 3) }));

        // --- Temples ---
        Add(new BuildingDefinition(BuildingType.Temple, WorkerType.None,
            new[] { (Material.Plank, 5), (Material.Stone, 8) },
            inputs: new[] { Material.Wine }));

        Add(new BuildingDefinition(BuildingType.BigTemple, WorkerType.None,
            new[] { (Material.Stone, 14), (Material.Plank, 8) }));

        return defs;
    }
}
