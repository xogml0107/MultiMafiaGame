using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
#endif

namespace ZZabmongus.Networking
{
    /// <summary>Opt-in development build integration check. Inert without -zzNetProbe.</summary>
    public sealed class MultiplayerProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerConnection connection;
        private NetworkLobby lobby;
        private int clientNumber, stage, sessions;
        private bool host, previousSession, everConnected, rejoined, completed, sawReplicatedMovement;
        private float started, stageAt, movementAt;
        private string output, screenshot;

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var marker = Array.IndexOf(args, "-zzNetProbe");
            if (marker < 0 || marker + 1 >= args.Length) { enabled = false; yield break; }
            var role = args[marker + 1];
            host = role == "host";
            clientNumber = host ? 0 : int.Parse(role);
            var result = Array.IndexOf(args, "-zzNetResult");
            output = result >= 0 ? args[result + 1] : Path.Combine(Application.persistentDataPath, "network-probe-" + role + ".json");
            var picture = Array.IndexOf(args, "-zzNetScreenshot");
            if (picture >= 0) screenshot = args[picture + 1];
            connection = FindFirstObjectByType<MultiplayerConnection>();
            lobby = FindFirstObjectByType<NetworkLobby>();
            connection.DisplayName = host ? "ProbeHost" : "ProbeClient" + clientNumber;
            var port = Array.IndexOf(args, "-zzNetPort");
            connection.Port = port >= 0 ? ushort.Parse(args[port + 1]) : (ushort)15000;
            started = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(host ? 0.3f : 1.5f);
            if (host) connection.Host(); else connection.Join();
        }

        private void Update()
        {
            if (!connection || completed) return;
            if (Time.unscaledTime - started > 60) { Finish(false, "timeout at stage " + stage + ", sessions " + sessions + ", players " + lobby.Players.Length + ": " + connection.Message); return; }
            if (connection.Connected) everConnected = true;
            if (lobby.InSession && !previousSession) sessions++;
            previousSession = lobby.InSession;
            var local = NetworkPlayer.Local;
            if (local && connection.Connected)
            {
                if (movementAt == 0) movementAt = Time.unscaledTime;
                local.ProbeInput = !host && Time.unscaledTime - movementAt < 1.5f ? Vector2.right : Vector2.zero;
                if (!lobby.InSession && !local.Ready && Time.unscaledTime - movementAt > 1) local.ToggleReady();
            }
            if (host) HostCheck(); else ClientCheck();
        }

        private void HostCheck()
        {
            var players = lobby.Players;
            if (stage == 0 && players.Length == 4 && lobby.CanStart &&
                players.All(p => p.DisplayName.StartsWith("Probe")) && players.Select(p => p.ColorIndex).Distinct().Count() == 4)
            {
                var remotes = players.Where(p => !p.isOwner).ToArray();
                if (remotes.Any(p => p.transform.position.x - (p.Slot % 4 - 1.5f) * 2 < 1.2f)) return;
                lobby.StartSession();
                if (!lobby.InSession) { Finish(false, "host could not start a ready room"); return; }
                MoveStage(1);
                if (!string.IsNullOrEmpty(screenshot)) ScreenCapture.CaptureScreenshot(screenshot);
            }
            else if (stage == 1 && players.Length == 3)
            {
                lobby.ReturnToLobby();
                MoveStage(2);
            }
            else if (stage == 2 && players.Length == 4 && lobby.CanStart)
            {
                lobby.StartSession();
                MoveStage(3);
            }
            else if (stage == 3 && Time.unscaledTime - stageAt > 3)
            {
                connection.Leave();
                MoveStage(4);
            }
            else if (stage == 4 && !connection.Busy && players.Length == 0)
                Finish(sessions == 2, "four peers, unique profiles/colors, authoritative remote movement, ready/start, leave/despawn, reconnect, second start, host closure");
        }

        private void ClientCheck()
        {
            var peers = lobby.Players;
            var local = NetworkPlayer.Local;
            if (local && local.transform.position.x - (local.Slot % 4 - 1.5f) * 2 > 0.6f &&
                peers.Any(p => !p.isOwner && p.DisplayName.StartsWith("ProbeClient") && p.transform.position.x - (p.Slot % 4 - 1.5f) * 2 > 0.6f))
                sawReplicatedMovement = true;
            if (clientNumber == 1 && stage == 0 && sessions == 1 && lobby.InSession && sawReplicatedMovement)
            {
                connection.Leave();
                MoveStage(1);
            }
            else if (clientNumber == 1 && stage == 1 && !connection.Busy && lobby.Players.Length == 0 && Time.unscaledTime - stageAt > 2)
            {
                connection.Join();
                rejoined = true;
                MoveStage(2);
            }
            if (everConnected && sessions >= 2 && !connection.Busy && lobby.Players.Length == 0)
                Finish(sawReplicatedMovement && (clientNumber != 1 || rejoined), "observed both session starts, synchronized movement/roster and host closure" + (rejoined ? ", reconnected" : ""));
        }

        private void MoveStage(int next)
        {
            stage = next; stageAt = Time.unscaledTime;
            Debug.Log("[NetProbe] " + (host ? "host" : clientNumber.ToString()) + " stage " + stage);
        }
        private void Finish(bool passed, string detail)
        {
            completed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(new ProbeResult { passed = passed, detail = detail,
                sessions = sessions, role = host ? "host" : clientNumber.ToString() }, true));
            Debug.Log("[NetProbe] " + (passed ? "PASS " : "FAIL ") + detail);
            Application.Quit(passed ? 0 : 1);
        }
        [Serializable] private sealed class ProbeResult { public bool passed; public string role, detail; public int sessions; }
#endif
    }
}
