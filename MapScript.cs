namespace Realm.Maps;

using System.Numerics;
using Realm.MapAPI;

internal static class MapScriptUnits
{
    public static ReadOnlySpan<UnitId> All(IGameAPI api) => api.GetAllUnits();
}

public class MapScript : IWasmModule
{
    private static readonly TowerDefenseConfig MobaTowerConfig = new(12f, 20f, 1.25f, "lightning");
    private static readonly HeroProgressionConfig MobaHeroProgression = new(
        xpPerLevel: 100f,
        killGold: 25f,
        killXp: 40f,
        playerIndex: 0,
        minStartingGold: 300f,
        leaderboardId: "MOBA");

    private readonly List<TowerAI> _towers = [];
    private readonly MinionSpawner _minionSpawner = new();
    private UnitId _hero = UnitId.Invalid;
    private HeroProgression? _progression;
    private bool _gameEnded;

    public void Initialize(IGameAPI api)
    {
        api.SetPlayerTeam(0, 0);
        api.SetPlayerTeam(1, 1);
        api.SetPlayersAllied(0, 1, false);
        api.SetPlayerMaxPopulation(0, 200);
        api.SetPlayerMaxPopulation(1, 200);

        SpawnPlayerHero(api);
        _minionSpawner.SpawnInitialWave(api);
        ConfigureTowers(api);
        api.SendMessageToPlayer(0, "First minion wave deployed.");
    }

    private void SpawnPlayerHero(IGameAPI api)
    {
        Vector3? Lookup(string name) => NamedPoints.Center(api, name);

        if (!CoordinateResolver.TryGetCenters(Lookup, ["Spawn_Team1"], out var spawnCenters, out _) &&
            !CoordinateResolver.TryGetCenters(Lookup, ["Base_Team1"], out spawnCenters, out _))
        {
            api.BroadcastMessage("Hero spawn failed: Spawn_Team1/Base_Team1 missing");
            return;
        }

        var spawn = spawnCenters.Values.First();

        _hero = api.GetPlayer(0).SpawnUnit(UnitIds.FantasyWarriorUnit1, spawn, false, true);

        if (!_hero.IsValid)
        {
            api.BroadcastMessage($"Hero spawn failed for {UnitIds.FantasyWarriorUnit1}");
            return;
        }

        api.SetUnitOwner(_hero, 0);
        _hero.Scale = 1.35f;
        _hero.HoldPosition();
        api.SelectPlayerUnit(0, _hero);
        api.PanPlayerCameraTo(0, _hero.Position, 0.35f);
        _progression = new HeroProgression(_hero, MobaHeroProgression);
        _progression.Attach(api);
        api.SendMessageToPlayer(0, "Kevin ready. Waves every 30s.");
    }

    public void Update(IGameAPI api, float delta)
    {
        CheckBaseWinLose(api);
        if (_gameEnded)
            return;

        foreach (var tower in _towers)
            tower.Update(api, delta);

        _minionSpawner.Update(api, delta);
        _progression?.Update(api, delta);
    }

    private void CheckBaseWinLose(IGameAPI api)
    {
        if (_gameEnded)
            return;

        var team1Base = UnitId.Invalid;
        var team2Base = UnitId.Invalid;

        foreach (var unit in MapScriptUnits.All(api))
        {
            if (unit.TemplateID != BuildingIds.IceCastle1 || !unit.IsBuilding || unit.IsDead)
                continue;

            if (api.IsPositionInCoordinate(unit.Position, "Base_Team1"))
                team1Base = unit;
            else if (api.IsPositionInCoordinate(unit.Position, "Base_Team2"))
                team2Base = unit;
        }

        if (team1Base.IsValid && team1Base.IsDead)
        {
            _gameEnded = true;
            api.TriggerDefeat();
            return;
        }

        if (team2Base.IsValid && team2Base.IsDead)
        {
            _gameEnded = true;
            api.TriggerVictory();
        }
    }

    private void ConfigureTowers(IGameAPI api)
    {
        foreach (var unit in MapScriptUnits.All(api))
        {
            if (unit.TemplateID != BuildingIds.IceCastle1 || !unit.IsBuilding)
                continue;

            var owner = GetTowerOwner(unit, api);
            if (unit.Player != owner)
                api.SetUnitOwner(unit, owner);

            _towers.Add(new TowerAI(unit, MobaTowerConfig));
        }
    }

    private static int GetTowerOwner(UnitId unit, IGameAPI api)
    {
        var position = unit.Position;
        if (api.IsPositionInCoordinate(position, "Base_Team1") ||
            api.IsPositionInCoordinate(position, "Mid_Team1_Tower") ||
            api.IsPositionInCoordinate(position, "Side_Team1_Tower") ||
            api.IsPositionInCoordinate(position, "Bot_Team1_Tower"))
            return 0;

        if (api.IsPositionInCoordinate(position, "Base_Team2") ||
            api.IsPositionInCoordinate(position, "Mid_Team2_Tower") ||
            api.IsPositionInCoordinate(position, "Side_Team2_Tower") ||
            api.IsPositionInCoordinate(position, "Top_Team2_Tower"))
            return 1;

        return position.Z > 0 || position.Z == 0 && position.X < 0 ? 0 : 1;
    }
}
