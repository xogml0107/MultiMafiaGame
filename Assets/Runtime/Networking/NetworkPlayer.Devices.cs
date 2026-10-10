using System;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Packing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZabmongus.Networking
{
    // Safe owner projection. No assigned number, seed, faction table or server session.
    public struct DeviceClueNote : IPackedAuto
    {
        public string text;
        public int observedSeconds;
        public int numberEpoch;
        public bool isPublic;
    }

    public sealed partial class NetworkPlayer
    {
        private DeviceClueNote[] deviceNotes = Array.Empty<DeviceClueNote>();
        private IReadOnlyList<DeviceClueNote> notesView = Array.AsReadOnly(Array.Empty<DeviceClueNote>());
        private int notebookEpoch;
        private float cooldownEndsAt, nextDeviceAttemptAt, nextDeviceScanAt;
        private string deviceMessage = "";
        public IReadOnlyList<DeviceClueNote> ClueNotes => HasMatchInfo ? notesView : Array.Empty<DeviceClueNote>();
        public int NotebookEpoch => HasMatchInfo ? notebookEpoch : 0;
        public int DeviceCooldownSeconds => HasMatchInfo ? Mathf.CeilToInt(Mathf.Max(0, cooldownEndsAt - Time.unscaledTime)) : 0;
        public string DeviceMessage => HasMatchInfo ? deviceMessage : "";
        public InformationDeviceStation NearbyDevice { get; private set; }

        private void UpdateDeviceInput()
        {
            if (!HasMatchInfo || !lobby || lobby.Loading || lobby.Phase != Core.MatchPhase.Playing ||
                !SessionSceneFlow.Instance || !SessionSceneFlow.Instance.CanMove)
            { NearbyDevice = null; return; }
            if (Time.unscaledTime >= nextDeviceScanAt)
            {
                nextDeviceScanAt = Time.unscaledTime + 0.1f;
                NearbyDevice = InformationDeviceStation.Nearest(transform.position);
            }
            if (NearbyDevice && !UiConsumesKeyboard() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                RequestInspect(lobby.Round, NearbyDevice.DeviceId);
        }

        [ServerRpc(requireOwnership: true)]
        private void RequestInspect(int expectedRound, int deviceId)
        {
            if (!isServer || !lobby || Time.unscaledTime < nextDeviceAttemptAt) return;
            nextDeviceAttemptAt = Time.unscaledTime + 0.25f;
            lobby.InspectDevice(this, expectedRound, deviceId);
        }

        internal void SendNotebook(int revision, int epoch, float cooldown, DeviceClueNote[] notes)
        {
            if (isServer && owner.HasValue) ReceiveNotebook(owner.Value, revision, epoch, cooldown, notes);
        }

        [TargetRpc]
        private void ReceiveNotebook(PlayerID recipient, int revision, int epoch, float cooldown, DeviceClueNote[] notes)
        {
            if (!isOwner || !receivedMatchInfo || revision != privateRound || epoch < notebookEpoch) return;
            deviceNotes = notes ?? Array.Empty<DeviceClueNote>(); notesView = Array.AsReadOnly(deviceNotes);
            notebookEpoch = epoch; cooldownEndsAt = Time.unscaledTime + Mathf.Max(0, cooldown);
        }

        internal void SendDeviceResponse(int revision, string message)
        {
            if (isServer && owner.HasValue) ReceiveDeviceResponse(owner.Value, revision, message);
        }

        [TargetRpc]
        private void ReceiveDeviceResponse(PlayerID recipient, int revision, string message)
        {
            if (isOwner && receivedMatchInfo && revision == privateRound) deviceMessage = message;
        }

        private void ClearDeviceState()
        {
            deviceNotes = Array.Empty<DeviceClueNote>(); notesView = Array.AsReadOnly(deviceNotes);
            notebookEpoch = 0; cooldownEndsAt = nextDeviceAttemptAt = nextDeviceScanAt = 0;
            deviceMessage = ""; NearbyDevice = null;
        }
    }
}
