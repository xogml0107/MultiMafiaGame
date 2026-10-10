using System;
using System.Collections.Generic;
using System.Linq;

namespace ZZabmongus.Core
{
    /// <summary>Trusted map metadata supplied by the server, never by a device RPC.</summary>
    public sealed class HostedDevice
    {
        public int Id { get; }
        public DeviceKind Kind { get; }
        public Position Position { get; }
        public HostedDevice(int id, DeviceKind kind, Position position)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (!Enum.IsDefined(typeof(DeviceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Id = id; Kind = kind; Position = position;
        }
    }

    public sealed partial class HostedMatch
    {
        private readonly Dictionary<int, HostedDevice> devices = new();
        private readonly Dictionary<string, Position> serverPositions = new(StringComparer.Ordinal);

        public void RegisterDevices(IEnumerable<HostedDevice> mapDevices)
        {
            if (!Active) throw new InvalidOperationException("Start a match before registering its map devices.");
            var snapshot = mapDevices?.ToArray() ?? throw new ArgumentNullException(nameof(mapDevices));
            if (snapshot.Any(d => d == null) || snapshot.Select(d => d.Id).Distinct().Count() != snapshot.Length)
                throw new ArgumentException("Map device IDs must be unique.", nameof(mapDevices));
            devices.Clear();
            foreach (var device in snapshot) devices.Add(device.Id, device);
        }

        // This is a local server API. The transport must read authoritative avatar transforms.
        public bool SetPlayerPosition(string authenticatedId, Position position)
        {
            if (!Active || authenticatedId == null || !participants.Contains(authenticatedId)) return false;
            serverPositions[authenticatedId] = position; match.SetPosition(authenticatedId, position); return true;
        }

        public bool TryInspect(string authenticatedId, int expectedRound, int deviceId, out string message)
        {
            if (!Active || expectedRound != Round) return Fail("현재 판의 입장 정보가 아닙니다.", out message);
            if (authenticatedId == null || !participants.Contains(authenticatedId)) return Fail("참가자만 장치를 사용할 수 있습니다.", out message);
            if (match.Phase != MatchPhase.Playing) return Fail("정보 수집 시간이 끝났습니다.", out message);
            if (!devices.TryGetValue(deviceId, out var device)) return Fail("사용할 수 없는 장치입니다.", out message);
            if (!serverPositions.TryGetValue(authenticatedId, out var position)) return Fail("플레이어 위치를 확인하지 못했습니다.", out message);
            var radiusSquared = match.Config.InteractionDistance * match.Config.InteractionDistance;
            if (position.DistanceSquared(device.Position) > radiusSquared) return Fail("장치에 더 가까이 가세요.", out message);
            var partner = serverPositions.Where(pair => pair.Key != authenticatedId && participants.Contains(pair.Key) &&
                    pair.Value.DistanceSquared(position) <= radiusSquared && pair.Value.DistanceSquared(device.Position) <= radiusSquared)
                .OrderBy(pair => pair.Value.DistanceSquared(position)).ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key).FirstOrDefault();
            if (!match.TryInspect(authenticatedId, device.Kind, partner, out var reason))
            {
                message = reason switch
                {
                    "Device cooldown is active." => "장치 재사용 대기시간이 남아 있습니다.",
                    "A second player must be nearby." => "동행할 다른 플레이어가 장치 근처에 있어야 합니다.",
                    "Not enough team points." => "팀 포인트가 부족합니다.",
                    "This device is blocked by a rule." => "현재 규칙으로 사용할 수 없는 장치입니다.",
                    _ => "지금은 장치를 사용할 수 없습니다."
                };
                return false;
            }
            message = "검사 결과를 단서 노트에 기록했습니다.";
            return true;
        }

        public double CooldownRemaining(string authenticatedId) =>
            TryGetOwnerView(authenticatedId, out var view) ? Math.Max(0, view.DeviceReadyAt - match.ElapsedSeconds) : 0;

        private void ClearDevices() { devices.Clear(); serverPositions.Clear(); }
    }
}
