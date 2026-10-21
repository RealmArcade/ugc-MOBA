namespace Realm.Maps;

using Realm.MapAPI;

public sealed class TowerAI
{
    private readonly TowerDefenseConfig _config;
    private readonly UnitId _tower;
    private float _cooldown;

    public TowerAI(UnitId tower, TowerDefenseConfig config)
    {
        _tower = tower;
        _config = config;
        if (_tower.Range <= 0) _tower.Range = config.Range;
        if (_tower.Damage <= 0) _tower.Damage = config.Damage;
    }

    public bool IsAlive => !_tower.IsDead;

    public void Update(IGameAPI api, float delta) =>
        MapScriptHelpers.RunTowerDefenseTick(api, _tower, _config, ref _cooldown, delta);
}
