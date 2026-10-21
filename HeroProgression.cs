namespace Realm.Maps;

using Realm.MapAPI;

public sealed class HeroProgression
{
    private readonly HeroProgressionConfig _config;
    private readonly UnitId _hero;
    private readonly HashSet<int> _rewardedDeaths = [];
    private IGameAPI? _api;
    private float _trackedXp;
    private float _trackedGold;
    private bool _attached;

    public HeroProgression(UnitId hero, HeroProgressionConfig config)
    {
        _hero = hero;
        _config = config;
    }

    public void Attach(IGameAPI api)
    {
        if (_attached)
            return;

        _attached = true;
        _api = api;
        _trackedGold = MathF.Max(api.GetPlayer(_config.PlayerIndex).Gold, _config.MinStartingGold);
        api.SetPlayerGold(_config.PlayerIndex, _trackedGold);
        try
        {
            api.SetLeaderboardVisible(_config.LeaderboardId, true);
            api.ClearLeaderboard();
            api.AddLeaderboardRow("Gold", $"{(int)_trackedGold}", null);
            api.AddLeaderboardRow("Level", "1", null);
        }
        catch (Exception ex)
        {
            api.BroadcastMessage($"HeroProgression leaderboard setup failed: {ex.Message}");
        }

        api.SendMessageToPlayer(_config.PlayerIndex, $"Starting gold: {(int)_trackedGold}");
        api.OnUnitDied += HandleUnitDied;
    }

    public void Update(IGameAPI api, float delta)
    {
        _trackedGold = api.GetPlayer(_config.PlayerIndex).Gold;
        api.SetLeaderboardValue("Gold", $"{(int)_trackedGold}");
    }

    private void HandleUnitDied(UnitId dead, UnitId killer)
    {
        if (_api == null || !_hero.IsValid || dead.Player == _hero.Player || _hero.IsDead)
            return;
        if (!_rewardedDeaths.Add(dead.UniqueId))
            return;

        _ = killer.UniqueId;
        var reward = HeroKillReward.AfterKill(_config, _trackedGold, _trackedXp);
        _trackedGold = reward.Gold;
        _trackedXp = reward.Xp;
        _api.SetPlayerGold(_config.PlayerIndex, _trackedGold);
        _api.SetUnitLevel(_hero, reward.Level);
        _api.SetLeaderboardValue("Gold", $"{(int)_trackedGold}");
        _api.SetLeaderboardValue("Level", $"{reward.Level}");
        _api.SendMessageToPlayer(
            _config.PlayerIndex,
            $"+{(int)_config.KillGold} gold (total {(int)_trackedGold}) | Level {reward.Level}");
    }
}
