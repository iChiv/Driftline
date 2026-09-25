using System;
using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Small LAN room for portfolio demonstrations, with host-authoritative commands.</summary>
    public sealed class DraftlineLanRoom : MonoBehaviour
    {
        private const string CommandChannel = "draftline.command.v1";
        private const string StateChannel = "draftline.state.v1";
        private const string DiceChannel = "draftline.dice.v1";
        private const ushort DefaultPort = 7777;
        private readonly Dictionary<ulong, PlayerColor> seats = new Dictionary<ulong, PlayerColor>();
        private NetworkManager network;
        private UnityTransport transport;
        private DraftlineGameController controller;
        private int seatsRequested = 2;
        private int lastSequence;
        private bool matchStarted;

        public bool IsNetworked => network != null && network.IsListening;
        public bool IsHost => IsNetworked && network.IsHost;
        public bool IsClientOnly => IsNetworked && network.IsClient && !network.IsHost;
        public PlayerColor? LocalColor => IsNetworked && seats.TryGetValue(network.LocalClientId, out var color)
            ? color : (IsHost ? (PlayerColor?)PlayerColor.Red : null);
        public int PlayerCount => seats.Count;
        public int SeatsRequested => seatsRequested;
        public bool MatchStarted => matchStarted;
        public string Status { get; private set; } = "本地对局";

        private void Awake()
        {
            controller = GetComponent<DraftlineGameController>();
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.OnClientConnectedCallback -= OnConnected;
            network.OnClientDisconnectCallback -= OnDisconnected;
            if (network.IsListening) network.Shutdown();
        }

        private void EnsureNetwork()
        {
            if (network != null) return;
            var root = new GameObject("Draftline LAN Network");
            transport = root.AddComponent<UnityTransport>();
            network = root.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                ConnectionApproval = true,
                PlayerPrefab = null
            };
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientConnectedCallback += OnConnected;
            network.OnClientDisconnectCallback += OnDisconnected;
        }

        public bool Host(int players, ushort port = DefaultPort)
        {
            if (players < 2 || players > 4 || IsNetworked) return false;
            EnsureNetwork();
            seatsRequested = players;
            seats.Clear();
            matchStarted = false;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            var started = network.StartHost();
            Status = started ? $"局域网房间已创建 · 端口 {port}" : "无法创建局域网房间";
            if (started) RegisterHandlers();
            return started;
        }

        public bool Join(string address, ushort port = DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(address) || IsNetworked) return false;
            EnsureNetwork();
            seats.Clear();
            matchStarted = false;
            transport.SetConnectionData(address.Trim(), port);
            var started = network.StartClient();
            Status = started ? "正在连接房主…" : "连接未启动";
            if (started) RegisterHandlers();
            return started;
        }

        public bool StartHostedMatch()
        {
            if (!IsHost || matchStarted || seats.Count != seatsRequested) return false;
            matchStarted = true;
            var mask = seats.Values.Aggregate(0, (current, color) => current | (1 << (int)color));
            controller.BeginNetworkMatch(mask);
            BroadcastState(Array.Empty<MatchEvent>());
            Status = "对局进行中";
            return true;
        }

        public void Leave()
        {
            if (network != null && network.IsListening) network.Shutdown();
            seats.Clear();
            matchStarted = false;
            Status = "已离开局域网房间";
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = !matchStarted && seats.Count < seatsRequested;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (!response.Approved) response.Reason = "Room is full or the match has started.";
        }

        private void OnConnected(ulong id)
        {
            if (network.IsHost)
            {
                var next = Enumerable.Range(0, 4).Select(index => (PlayerColor)index)
                    .FirstOrDefault(color => !seats.ContainsValue(color));
                seats[id] = next;
                BroadcastState(Array.Empty<MatchEvent>());
                Status = $"已连接 {seats.Count}/{seatsRequested} 位玩家";
            }
            else
                Status = "已连接房主，等待开局";
        }

        private void OnDisconnected(ulong id)
        {
            seats.Remove(id);
            if (matchStarted) Status = "有玩家断开，房主可重新开局";
            else Status = IsHost ? $"已连接 {seats.Count}/{seatsRequested} 位玩家" : "连接已断开";
        }

        private void RegisterHandlers()
        {
            if (network.CustomMessagingManager == null) return;
            network.CustomMessagingManager.RegisterNamedMessageHandler(CommandChannel, ReceiveCommand);
            network.CustomMessagingManager.RegisterNamedMessageHandler(StateChannel, ReceiveState);
            network.CustomMessagingManager.RegisterNamedMessageHandler(DiceChannel, ReceiveDice);
        }

        public void SendCommand(string kind, int first = 0, int second = 0)
        {
            if (!IsClientOnly) return;
            using (var writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteValueSafe(kind);
                writer.WriteValueSafe(first);
                writer.WriteValueSafe(second);
                network.CustomMessagingManager.SendNamedMessage(CommandChannel,
                    NetworkManager.ServerClientId, writer);
            }
        }

        private void ReceiveCommand(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || !matchStarted || !seats.TryGetValue(sender, out var color) ||
                controller.Rules == null || controller.Rules.State.CurrentPlayer != color) return;
            reader.ReadValueSafe(out string kind);
            reader.ReadValueSafe(out int first);
            reader.ReadValueSafe(out int second);
            controller.ApplyVerifiedRemoteCommand(kind, first, second);
        }

        public void BroadcastDiceStart()
        {
            if (!IsHost) return;
            using (var writer = new FastBufferWriter(4, Allocator.Temp))
                network.CustomMessagingManager.SendNamedMessageToAll(DiceChannel, writer);
        }

        private void ReceiveDice(ulong sender, FastBufferReader reader)
        {
            if (IsClientOnly && sender == NetworkManager.ServerClientId)
                controller.PlayNetworkDice();
        }

        public void BroadcastState(IReadOnlyList<MatchEvent> events)
        {
            if (!IsHost) return;
            var slide = events.FirstOrDefault(item => item.Type == MatchEventType.PieceSlid);
            var packet = new RoomStatePacket
            {
                sequence = ++lastSequence,
                seatsRequested = seatsRequested,
                started = matchStarted,
                seats = string.Join(",", seats.Select(pair => pair.Key + ":" + (int)pair.Value)),
                snapshot = controller.Rules == null ? "" :
                    JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)),
                slide = slide?.Slide == null ? "" : JsonUtility.ToJson(SlidePacket.Capture(slide))
            };
            var json = JsonUtility.ToJson(packet);
            var capacity = FastBufferWriter.GetWriteSize(json);
            using (var writer = new FastBufferWriter(capacity, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                network.CustomMessagingManager.SendNamedMessageToAll(
                    StateChannel, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (!IsClientOnly || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            var packet = JsonUtility.FromJson<RoomStatePacket>(json);
            if (packet.sequence <= lastSequence) return;
            lastSequence = packet.sequence;
            seatsRequested = packet.seatsRequested;
            matchStarted = packet.started;
            seats.Clear();
            foreach (var item in packet.seats.Split(','))
            {
                var pair = item.Split(':');
                if (pair.Length == 2 && ulong.TryParse(pair[0], out var id) &&
                    int.TryParse(pair[1], out var value) && value >= 0 && value < 4)
                    seats[id] = (PlayerColor)value;
            }
            SlidePacket slide = null;
            if (!string.IsNullOrEmpty(packet.slide))
                slide = JsonUtility.FromJson<SlidePacket>(packet.slide);
            var origin = slide != null ? controller.PieceViewPosition(slide.pieceId) : null;
            if (!string.IsNullOrEmpty(packet.snapshot))
                controller.ApplyNetworkSnapshot(JsonUtility.FromJson<MatchSnapshot>(packet.snapshot).Restore());
            if (slide != null)
                controller.ApplyNetworkSlide(slide.Restore(), slide.pieceId, origin);
            Status = matchStarted ? "对局进行中" : $"等待玩家 {seats.Count}/{seatsRequested}";
        }
    }
}
