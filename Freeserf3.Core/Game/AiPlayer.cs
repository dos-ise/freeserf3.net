/*
 * AiPlayer.cs - Simple computer opponent
 *
 * The AI builds living houses, production and military buildings near its
 * base, lets the normal game logic staff and run them, and periodically
 * sends attack waves of soldiers against the human player.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// Drives one computer-controlled player: builds buildings to grow the
/// economy and sends soldiers to attack the human player.
/// </summary>
public sealed class AiPlayer
{
    readonly Game game;
    readonly Player player;
    readonly Random random;

    /// <summary>Ticks between two AI build decisions.</summary>
    const int BuildInterval = 400;

    /// <summary>Ticks between two AI attack waves.</summary>
    const int AttackInterval = 3000;

    /// <summary>Soldiers sent per attack wave (grows with the AI's army).</summary>
    int AttackWaveSize
    {
        get
        {
            int soldiers = game.Settlers.Count(s => s.Player == player.Index && s.Type == SerfType.Soldier);
            return Math.Clamp(2 + soldiers / 4, 2, 8);
        }
    }

    public AiPlayer(Game game, Player player, Random random)
    {
        this.game = game;
        this.player = player;
        this.random = random;
    }

    /// <summary>Advances the AI by one game tick.</summary>
    public void Tick()
    {
        if (game.Tick % BuildInterval == 0)
            TryBuild();

        if (game.Tick % AttackInterval == 0)
            TryAttack();
    }

    /// <summary>
    /// Builds the next useful building: the first missing production or
    /// military building, then more of each to grow. The building is only
    /// placed when the player has the construction materials and a valid
    /// position near the base.
    /// </summary>
    void TryBuild()
    {
        var toBuild = ChooseBuilding();
        if (toBuild == null)
            return;

        if (!HasMaterials(toBuild.Value))
            return;

        var pos = FindBuildPosition(toBuild.Value);
        if (pos == null)
            return;

        game.PlaceBuilding(toBuild.Value, pos.Value, player.Index);
    }

    /// <summary>Chooses the building type the AI needs most right now.</summary>
    BuildingType? ChooseBuilding()
    {
        int lumberjacks = Count(BuildingType.Lumberjack);
        int sawmills = Count(BuildingType.Sawmill);
        int farms = Count(BuildingType.Farm);
        int mills = Count(BuildingType.Mill);
        int bakers = Count(BuildingType.Baker);
        int fishers = Count(BuildingType.Fisher);
        int towers = Count(BuildingType.Tower);
        int stonecutters = Count(BuildingType.Stonecutter);
        int livingHouses = CountLivingHouses();
        int ironmines = Count(BuildingType.Ironmine);
        int coalmines = Count(BuildingType.Coalmine);
        int ironmelts = Count(BuildingType.Ironmelt);
        int weaponsmiths = Count(BuildingType.Weaponsmith);
        int toolsmiths = Count(BuildingType.Toolsmith);

        if (lumberjacks == 0) return BuildingType.Lumberjack;
        if (sawmills == 0) return BuildingType.Sawmill;
        if (farms == 0) return BuildingType.Farm;
        if (towers == 0) return BuildingType.Tower;
        if (livingHouses == 0) return BuildingType.SmallLivinghouse;
        if (lumberjacks < 2) return BuildingType.Lumberjack;
        if (sawmills < 2) return BuildingType.Sawmill;
        if (farms < 2) return BuildingType.Farm;
        if (towers < 2) return BuildingType.Tower;
        if (stonecutters == 0) return BuildingType.Stonecutter;
        if (livingHouses < 8) return BuildingType.SmallLivinghouse;

        // Food production chain: flour and bread from the farm's crop.
        if (mills == 0) return BuildingType.Mill;
        if (bakers == 0) return BuildingType.Baker;
        if (fishers == 0) return BuildingType.Fisher;
        if (mills < 2) return BuildingType.Mill;
        if (bakers < 2) return BuildingType.Baker;

        // Military production chain: iron and coal for weapons.
        if (ironmines == 0) return BuildingType.Ironmine;
        if (coalmines == 0) return BuildingType.Coalmine;
        if (ironmelts == 0) return BuildingType.Ironmelt;
        if (weaponsmiths == 0) return BuildingType.Weaponsmith;
        if (toolsmiths == 0) return BuildingType.Toolsmith;
        if (ironmines < 2) return BuildingType.Ironmine;
        if (coalmines < 2) return BuildingType.Coalmine;
        if (weaponsmiths < 2) return BuildingType.Weaponsmith;

        return BuildingType.Tower;
    }

    /// <summary>Number of the player's buildings of the given type.</summary>
    int Count(BuildingType type)
    {
        return game.Buildings.Count(b => b.Player == player.Index && b.Type == type && b.State != BuildingState.Destroyed);
    }

    /// <summary>Number of the player's living houses.</summary>
    int CountLivingHouses()
    {
        return game.Buildings.Count(b => b.Player == player.Index && b.Definition.IsLivinghouse && b.State != BuildingState.Destroyed);
    }

    /// <summary>Whether the player has all construction materials.</summary>
    bool HasMaterials(BuildingType type)
    {
        var definition = BuildingDefinition.Get(type);
        foreach (var (material, count) in definition.ConstructionCosts)
        {
            if (player.Inventory.Get(material) < count)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Finds a valid position for the building near the player's base by
    /// sampling random points in expanding rings around it. Samples are
    /// biased toward the human player so the AI expands in its direction.
    /// </summary>
    Position? FindBuildPosition(BuildingType type)
    {
        var center = GetBasePosition();
        var humanBase = GetHumanBasePosition();
        int hdx = Math.Sign(humanBase.X - center.X);
        int hdy = Math.Sign(humanBase.Y - center.Y);

        for (int radius = 3; radius <= 24; radius += 2)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int dx, dy;
                if (random.Next(3) == 0)
                {
                    // Bias toward the human player.
                    dx = hdx * random.Next(1, radius + 1);
                    dy = hdy * random.Next(1, radius + 1);
                }
                else
                {
                    dx = random.Next(-radius, radius + 1);
                    dy = random.Next(-radius, radius + 1);
                }

                var pos = new Position(center.X + dx, center.Y + dy);
                if (!game.Map.IsInBounds(pos))
                    continue;
                if (game.IsBuildingAt(pos))
                    continue;
                if (!game.CanPlaceBuilding(type, pos, player.Index))
                    continue;
                return pos;
            }
        }
        return null;
    }

    /// <summary>The human player's base (their first building or start).</summary>
    Position GetHumanBasePosition()
    {
        var human = game.GetPlayer(0);
        if (human == null)
            return player.StartPosition;

        var building = game.Buildings.FirstOrDefault(b => b.Player == 0 && b.State != BuildingState.Destroyed);
        return building?.Position ?? human.StartPosition;
    }

    /// <summary>The player's base: their first building, or the start position.</summary>
    Position GetBasePosition()
    {
        var building = game.Buildings.FirstOrDefault(b => b.Player == player.Index && b.State != BuildingState.Destroyed);
        return building?.Position ?? player.StartPosition;
    }

    /// <summary>
    /// Sends a wave of soldiers from the military buildings against the
    /// nearest human building. The soldiers march there and fight any
    /// human settler they meet.
    /// </summary>
    void TryAttack()
    {
        var targets = game.Buildings.Where(b => b.Player == 0 && b.State == BuildingState.Active).ToList();
        if (targets.Count == 0)
            return;

        var basePos = GetBasePosition();
        var target = targets.OrderBy(t => MapGeometry.GetDistance(basePos, t.Position)).First();
        var attackPos = FindWalkableNear(target);

        int sent = 0;
        foreach (var building in game.Buildings)
        {
            if (building.Player != player.Index || !building.Definition.IsMilitary)
                continue;
            if (building.State != BuildingState.Active)
                continue;

            foreach (var soldier in building.Soldiers.ToList())
            {
                if (soldier.MarchTarget != null || soldier.SoldierState == SoldierState.Attacking)
                    continue;

                // The soldier leaves the building for the attack wave, so
                // the building can recruit a replacement.
                building.Soldiers.Remove(soldier);
                soldier.IsDeployed = true;
                soldier.MarchTarget = attackPos;
                soldier.SoldierState = SoldierState.Attacking;
                sent++;
                if (sent >= AttackWaveSize)
                    return;
            }
        }
    }

    /// <summary>
    /// Finds a walkable position next to the building (its flag or a
    /// walkable neighbor) so the soldiers can reach it.
    /// </summary>
    Position FindWalkableNear(Building building)
    {
        if (building.Flag != null && game.IsWalkable(game.Map, building.Flag.Position))
            return building.Flag.Position;

        foreach (var direction in MapGeometry.AllDirections())
        {
            var pos = MapGeometry.GetNeighbor(building.Position, direction);
            if (game.Map.IsInBounds(pos) && game.IsWalkable(game.Map, pos))
                return pos;
        }
        return building.Position;
    }
}
