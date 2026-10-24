using Freeserf3.Data;
using Freeserf3.Simulation;

var map = new Map(12);
map.Players.Add(new MapPlayerInfo { Name = "P0" });
map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
for (int y = 0; y < 12; y++)
    for (int x = 0; x < 12; x++)
    {
        var c = map.GetCell(x, y);
        c.Landscape = 1;
        map.SetCell(x, y, c);
    }
var orePos = new Position(6, 6);
var cell = map.GetCell(orePos);
cell.ResourceType1 = 2;
cell.ResourceType2 = 5;
map.SetCell(orePos, cell);

var game = new Game(map, 42);
game.StartGame();
var mine = game.PlaceBuilding(BuildingType.Ironmine, new Position(4, 4), 0);
for (int i = 0; i < 5000; i++)
{
    game.TickOnce();
    if (i % 500 == 0)
        Console.WriteLine($"tick={i} state={mine.State} worker={mine.Worker} inputBread={mine.Input.Get(Material.Bread)} inputMeat={mine.Input.Get(Material.Meat)} inputFish={mine.Input.Get(Material.Fish)} output={mine.Output.Get(Material.IronOre)} freeSettlers={game.GetPlayer(0)!.FreeSettlers} pick={game.GetPlayer(0)!.Inventory.Get(Material.Pick)}");
}
