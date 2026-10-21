namespace Realm.Maps;

using System.Numerics;
using Realm.MapAPI;

public sealed class MinionSpawner
{
    private const float WaveInterval = 30f;
    private const float SpawnOffset = 1.5f;
    private static readonly string[] LaneCorners = ["Top_Corner", "Middle", "Bot_Corner"];
    private static readonly WaypointMarchConfig MarchConfig = WaypointMarchConfig.Default;
    private readonly List<LanePathfinder> _minions = [];
    private float _elapsed;
    private bool _reportedSpawnFailure;

    public void SpawnInitialWave(IGameAPI api)
    {
        _elapsed = 0f;
        SpawnWave(api);
    }

    public void Update(IGameAPI api, float delta)
    {
        _elapsed += delta;
        if (_elapsed >= WaveInterval)
        {
            _elapsed -= WaveInterval;
            SpawnWave(api);
        }

        foreach (var minion in _minions.ToArray())
            minion.Update(api, delta);

        _minions.RemoveAll(minion => !minion.IsAlive);
    }

    private void SpawnWave(IGameAPI api)
    {
        if (NamedPoints.Center(api, "Base_Team1") is not { } baseTeam1 ||
            NamedPoints.Center(api, "Base_Team2") is not { } baseTeam2)
        {
            api.BroadcastMessage("MinionSpawner: Base_Team1/Base_Team2 missing; skipping wave");
            return;
        }

        var spawned = 0;
        spawned += SpawnTeam(api, 0, baseTeam1, baseTeam2);
        spawned += SpawnTeam(api, 1, baseTeam2, baseTeam1);
        api.BroadcastMessage($"Minion wave deployed: {spawned} units across 3 lanes per team.");
    }

    private int SpawnTeam(IGameAPI api, int player, Vector3 start, Vector3 destination)
    {
        var spawnPad = player == 0 ? "Spawn_Team1" : "Spawn_Team2";
        var spawnStart = NamedPoints.Center(api, spawnPad)
            ?? start + (player == 0 ? new Vector3(5f, 0f, -5f) : new Vector3(-5f, 0f, 5f));

        var spawned = 0;
        foreach (var cornerName in LaneCorners)
        {
            if (!CoordinateResolver.TryBuildThreePointPath(
                    name => NamedPoints.Center(api, name),
                    spawnStart,
                    cornerName,
                    destination,
                    out var waypoints))
            {
                api.BroadcastMessage($"MinionSpawner: skipping lane {cornerName}");
                continue;
            }

            for (var member = 0; member < 3; member++)
            {
                var offset = new Vector3((member - 1) * SpawnOffset, 0, 0);
                var unit = api.GetPlayer(player).SpawnUnit(
                    player == 0 ? UnitIds.MobaMinionTeam1 : UnitIds.MobaMinionTeam2,
                    spawnStart + offset,
                    player == 1,
                    true);
                if (!unit.IsValid)
                {
                    if (!_reportedSpawnFailure)
                    {
                        api.BroadcastMessage("MinionSpawner: SpawnUnit returned null; some minions were skipped");
                        _reportedSpawnFailure = true;
                    }
                    continue;
                }

                api.SetUnitOwner(unit, player);
                _minions.Add(new LanePathfinder(unit, waypoints, MarchConfig));
                spawned++;
            }
        }

        return spawned;
    }
}
