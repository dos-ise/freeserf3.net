/*
 * BuildingType.cs - Building and worker types
 *
 * The building list follows settlers-remake (MIT) EBuildingType.java.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The building types of the Settlers 3 economy.</summary>
public enum BuildingType
{
    Stonecutter,
    Forester,
    Lumberjack,
    Sawmill,

    Coalmine,
    Ironmine,
    Goldmine,
    Goldmelt,
    Ironmelt,
    Toolsmith,
    Weaponsmith,

    Farm,
    PigFarm,
    Mill,
    Waterworks,
    Slaughterhouse,
    Baker,
    Fisher,
    Winegrower,
    CharcoalBurner,
    DonkeyFarm,

    SmallLivinghouse,
    MediumLivinghouse,
    BigLivinghouse,

    LookoutTower,
    Tower,
    BigTower,
    Castle,
    Hospital,
    Barrack,

    Dockyard,
    Harbor,
    Stock,

    Temple,
    BigTemple,

    MarketPlace,

    Sulfurmine,
    Gemsmine,
    Brewery,
    RiceFarm,

    Beekeeping,
    Distillery,
    Laboratory,
    MeadBrewery
}

/// <summary>The worker professions of the Settlers 3 economy.</summary>
public enum WorkerType
{
    None,
    Stonecutter,
    Forester,
    Lumberjack,
    Sawmiller,
    Miner,
    Melter,
    Smith,
    Farmer,
    PigFarmer,
    Miller,
    Waterworker,
    Slaughterer,
    Baker,
    Fisherman,
    Winegrower,
    CharcoalBurner,
    DonkeyFarmer,
    Healer,
    Dockworker
}
