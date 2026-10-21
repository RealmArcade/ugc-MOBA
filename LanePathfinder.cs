namespace Realm.Maps;

using System.Numerics;
using Realm.MapAPI;

public sealed class LanePathfinder
{
    private readonly WaypointMarcher _marcher;

    public LanePathfinder(UnitId unit, IReadOnlyList<Vector3> waypoints, WaypointMarchConfig config) =>
        _marcher = new WaypointMarcher(unit, waypoints, config);

    public bool IsAlive => _marcher.IsAlive;

    public void Update(IGameAPI api, float delta) => _marcher.Update(api, delta);
}
