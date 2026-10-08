using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using ZZabmongus.Core;

namespace ZZabmongus.Runtime
{
    /// <summary>Single-computer development harness, not a network host or production match controller.</summary>
    public sealed class LocalSandbox : MonoBehaviour
    {
        [SerializeField] private SandboxSettings settings;
        private readonly List<GameObject> avatars = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<string, HashSet<int>> notes = new Dictionary<string, HashSet<int>>();
        private CharacterController[] controllers;
        private DeviceStation[] stations;
        private int selected;
        private int guess = 1;
        private int suspectIndex = -1;
        private bool holdingRecruit;
        private float speed = 1;
        public MatchSession Session { get; private set; }
        public string SelectedId => Session.PlayerIds[selected];
        public string Feedback { get; private set; } = "Explore the stations. First mission starts at 00:45.";
        public float SimulationSpeed => speed;
        public int Guess => guess;
        public string Suspect => suspectIndex < 0 ? null : Session.PlayerIds[suspectIndex];

        public void Configure(SandboxSettings value) => settings = value;

        private void Start()
        {
            if (settings == null) { Debug.LogError("Assign SandboxSettings before starting.", this); enabled = false; return; }
            stations = FindObjectsByType<DeviceStation>(FindObjectsSortMode.None);
            Restart();
            gameObject.AddComponent<SandboxHud>().Initialize(this);
        }

        public void Restart()
        {
            foreach (var avatar in avatars) Destroy(avatar);
            foreach (var material in materials) Destroy(material);
            avatars.Clear();
            materials.Clear();
            notes.Clear();
            selected = 0;
            guess = 1;
            suspectIndex = -1;
            holdingRecruit = false;
            var count = Mathf.Clamp(settings.playerCount, 4, 12);
            Session = new MatchSession(Enumerable.Range(1, count).Select(i => "P" + i), settings.seed, settings.CreateConfig());
            controllers = new CharacterController[count];
            for (var i = 0; i < count; i++)
            {
                var root = new GameObject(Session.PlayerIds[i]);
                root.transform.SetParent(transform, false);
                var angle = i * Mathf.PI * 2 / count;
                root.transform.position = new Vector3(Mathf.Sin(angle) * 4, 0.05f, Mathf.Cos(angle) * 3);
                var controller = root.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0, 0.9f, 0);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0, 0.9f, 0);
                body.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
                Destroy(body.GetComponent<Collider>());
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = Color.HSVToRGB((float)i / count, 0.55f, 0.95f);
                body.GetComponent<Renderer>().sharedMaterial = material;
                materials.Add(material);
                var label = new GameObject("PlayerLabel").AddComponent<TextMeshPro>();
                label.transform.SetParent(root.transform, false);
                label.transform.localPosition = new Vector3(0, 2.5f, 0);
                label.transform.rotation = Camera.main.transform.rotation;
                label.text = Session.PlayerIds[i];
                label.fontSize = 4;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(2, 1);
                controllers[i] = controller;
                avatars.Add(root);
                notes.Add(Session.PlayerIds[i], new HashSet<int>());
            }
            SyncPositions();
            Feedback = "LOCAL SANDBOX: switch between simulated players with Tab.";
        }

        private void Update()
        {
            if (Session == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame) NextPlayer();
                if (Session.Phase == MatchPhase.Playing || Session.Phase == MatchPhase.FinalDiscussion)
                {
                    var move = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0,
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                    controllers[selected].Move((move.normalized * settings.movementSpeed + Vector3.down * 3) * Time.deltaTime);
                }
                SyncPositions();
                if (keyboard.eKey.wasPressedThisFrame) Interact();
                if (keyboard.fKey.wasPressedThisFrame) BeginRecruit();
                if (holdingRecruit && !keyboard.fKey.isPressed)
                { Session.CancelRecruitment(SelectedId); holdingRecruit = false; }
            }
            else if (holdingRecruit) { Session.CancelRecruitment(SelectedId); holdingRecruit = false; }
            Session.Tick(Time.unscaledDeltaTime * speed);
        }

        private void SyncPositions()
        {
            for (var i = 0; i < avatars.Count; i++)
            {
                var pos = avatars[i].transform.position;
                Session.SetPosition(Session.PlayerIds[i], new Position(pos.x, pos.z));
            }
        }

        public void NextPlayer()
        {
            Session.CancelRecruitment(SelectedId);
            holdingRecruit = false;
            selected = (selected + 1) % avatars.Count;
            suspectIndex = -1;
            Feedback = "Controlling " + SelectedId + ". Its private view is shown on the right.";
        }

        public void ToggleSpeed() => speed = speed == 1 ? 10 : 1;
        public void AdvanceMinute()
        {
            Session.CancelRecruitment(SelectedId);
            holdingRecruit = false;
            Session.Tick(60);
            Feedback = "Developer control: advanced match clock by 60 seconds.";
        }

        public void CycleGuess() => guess = guess % Session.PlayerIds.Count + 1;
        public void CycleSuspect()
        {
            do { suspectIndex++; } while (suspectIndex < Session.PlayerIds.Count && suspectIndex == selected);
            if (suspectIndex >= Session.PlayerIds.Count) suspectIndex = -1;
        }
        public void ToggleCandidate(int number)
        { if (!notes[SelectedId].Add(number)) notes[SelectedId].Remove(number); }
        public bool IsCandidate(int number) => notes[SelectedId].Contains(number);
        public void Submit() { Session.TrySubmit(SelectedId, guess, Suspect, out var message); Feedback = message; }

        public string NearbyHint()
        {
            var station = NearestStation();
            return station == null ? "WASD: move | Tab: next player | Hold F near a player: recruit" :
                "E: " + station.DisplayName + (station.missionRelay ? " contribution" : " check (" + MatchSession.DeviceCost(station.device) + " points)");
        }

        private DeviceStation NearestStation() => stations.Where(s =>
            FlatDistance(s.transform.position, avatars[selected].transform.position) <= Session.Config.InteractionDistance)
            .OrderBy(s => FlatDistance(s.transform.position, avatars[selected].transform.position)).FirstOrDefault();

        private string NearestPartner()
        {
            var nearest = -1;
            var distance = Session.Config.InteractionDistance;
            for (var i = 0; i < avatars.Count; i++)
            {
                if (i == selected) continue;
                var current = FlatDistance(avatars[i].transform.position, avatars[selected].transform.position);
                if (current > distance) continue;
                nearest = i;
                distance = current;
            }
            return nearest < 0 ? null : Session.PlayerIds[nearest];
        }

        private void Interact()
        {
            var station = NearestStation();
            if (station == null) { Feedback = "Move closer to a station."; return; }
            string message;
            if (station.missionRelay) Session.TryContribute(SelectedId, out message);
            else Session.TryInspect(SelectedId, station.device, NearestPartner(), out message);
            Feedback = message;
        }

        private void BeginRecruit()
        {
            var partner = NearestPartner();
            if (partner == null) { Feedback = "Move closer to another player."; return; }
            holdingRecruit = Session.TryBeginRecruitment(SelectedId, partner, out var message);
            Feedback = message;
        }
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private void OnDestroy()
        { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
