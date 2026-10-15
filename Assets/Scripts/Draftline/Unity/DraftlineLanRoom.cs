using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Draftline.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Draftline.Unity
{
    public sealed class DraftlineLanRoom : MonoBehaviour
    {
        private const string RequestChannel = "driftline.request.v2", StateChannel = "driftline.state.v2", AckChannel = "driftline.ack.v2", DiceChannel = "driftline.dice.v2";
        private const string Contract = "board19-health5-pieces8-portals-5";
        public NetworkManager Network;
        public UnityTransport Transport;
        public bool FillWithComputers = true;
        private DraftlineGameController controller;
        private readonly Dictionary<PlayerColor, Member> members = new Dictionary<PlayerColor, Member>();
        private readonly Dictionary<ulong, Hello> approved = new Dictionary<ulong, Hello>();
        private readonly HashSet<string> accepted = new HashSet<string>();
        private string token = Guid.NewGuid().ToString("N"), sessionId, matchId, pendingId;
        private string lastAddress = "127.0.0.1";
        public LanAddressOption[] HostAddresses {get;private set;}=Array.Empty<LanAddressOption>();
        private int hostAddressIndex;
        public string HostEndpoint => HostAddresses.Length>0?HostAddresses[hostAddressIndex].Address+":"+lastPort:"未找到局域网地址";
        public void RefreshLocalAddresses(){HostAddresses=LanAddressUtility.Find();hostAddressIndex=0;}
        public void SelectHostAddress(int index){if(index>=0&&index<HostAddresses.Length)hostAddressIndex=index;}
        private ushort lastPort = 7777;
        private int sequence, receivedSequence, lastSentRevision = -1, lastReceivedRevision = -1, seatsRequested = 2;
        private float pendingSince;
        private bool matchStarted, leaving, lanSession;
        public bool IsNetworked => Network != null && Network.IsListening;
        public bool IsHost => IsNetworked && Network.IsHost;
        public bool IsClientOnly => IsNetworked && Network.IsClient && !Network.IsHost;
        public bool IsLanSession => lanSession;
        public bool MatchStarted => matchStarted;
        public int SeatsRequested => seatsRequested;
        public int PlayerCount => members.Values.Count(m => m.Connected);
        public string Status { get; private set; } = "尚未连接局域网";
        public bool HasPendingCommand => !string.IsNullOrEmpty(pendingId);
        public PlayerColor? LocalColor => Network == null ? null : members.Values.Where(m => m.Connected && m.ClientId == Network.LocalClientId).Select(m => (PlayerColor?)m.Color).FirstOrDefault();
        public bool LocalReady => LocalColor.HasValue && members[LocalColor.Value].Ready;
        public bool CanStart => IsHost && !matchStarted && members.Values.All(m => m.Connected && m.Ready) &&
            (FillWithComputers || PlayerCount == seatsRequested) && PlayerCount > 0;
        public bool WaitingForDisconnectedPlayer => IsHost && matchStarted && controller.Rules != null &&
            members.TryGetValue(controller.Rules.State.CurrentPlayer, out var m) && !m.Connected;
        public bool HasDisconnectedPlayers => members.Values.Any(m => !m.Connected);
        public string RoomDescription
        {
            get
            {
                if(!IsNetworked)return "";
                var text = IsHost ? "房主" : "房主：" + lastAddress + " : " + lastPort;
                var theme=DraftlineThemeApplier.Load(DraftlineThemeApplier.CurrentId);
                if(theme!=null)text+="\n棋盘主题："+theme.ThemeName+" · "+theme.EnvironmentNames[DraftlineThemeApplier.CurrentVariant];
                foreach (var m in members.Values.OrderBy(m => m.Color))
                    text += "\n" + MatchLabels.Player(m.Color) + " · " + (!m.Connected ? "掉线，可重连或由电脑接管" : m.Ready ? "已准备" : "未准备");
                if (FillWithComputers) text += "\n空席位由电脑补齐";
                return text;
            }
        }

        private sealed class Member
        {
            public ulong ClientId;
            public PlayerColor Color;
            public string Token;
            public bool Connected, Ready;
            public float DisconnectedAt;
        }
        [Serializable] private sealed class Hello { public int protocol = 2; public string contract = Contract, token; public int color = -1; }
        [Serializable] private sealed class Request { public string id, session, match, kind; public MatchCommand command; public bool ready; public int color; }
        [Serializable] private sealed class Ack { public string id, error; public bool accepted; }

        private void Awake()
        {
            controller = GetComponent<DraftlineGameController>();
            if (Network == null || Transport == null) { Debug.LogError("LAN components must be saved in the scene."); return; }
            Network.OnClientConnectedCallback += OnConnected;
            Network.OnClientDisconnectCallback += OnDisconnected;
            Network.ConnectionApprovalCallback = Approve;
        }
        private void OnDestroy()
        {
            if (Network == null) return;
            leaving = true;
            Unregister();
            Network.OnClientConnectedCallback -= OnConnected;
            Network.OnClientDisconnectCallback -= OnDisconnected;
            if (Network.IsListening) Network.Shutdown();
        }
        private bool Configure()
        {
            if (Network == null || Transport == null) { Status = "场景缺少 NetworkManager / UnityTransport"; return false; }
            Network.NetworkConfig.NetworkTransport = Transport;
            Network.NetworkConfig.EnableSceneManagement = false;
            Network.NetworkConfig.ForceSamePrefabs = false;
            Network.NetworkConfig.ConnectionApproval = true;
            Network.NetworkConfig.PlayerPrefab = null;
            return true;
        }
        public bool Host(int players, ushort port = 7777)
        {
            if (IsNetworked || players < 2 || players > 4 || !Configure()) return false;
            ResetConnection();
            lanSession = true; seatsRequested = players; lastPort = port;
            sessionId = Guid.NewGuid().ToString("N"); matchId = "";
            controller.Session.Mode = GameMode.Lan;
            RefreshLocalAddresses();
            Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello { token = token }));
            Transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            var ok = Network.StartHost();
            if (ok) { Register(); BroadcastState(Array.Empty<MatchEvent>(), true); }
            Status = ok ? "房间已创建，等待准备" : "创建失败，请检查端口是否被占用";
            return ok;
        }
        public bool Join(string address, ushort port = 7777)
        {
            if (IsNetworked || string.IsNullOrWhiteSpace(address) || !Configure()) return false;
            ResetConnection();
            lanSession = true; lastAddress = address.Trim(); lastPort = port;
            controller.Session.Mode = GameMode.Lan;
            Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello { token = token }));
            Transport.SetConnectionData(lastAddress, port);
            var ok = Network.StartClient();
            if (ok) Register();
            Status = ok ? "连接中…" : "无法开始连接";
            return ok;
        }
        public void Reconnect() => Join(lastAddress, lastPort);
        private void ResetConnection()
        {
            members.Clear(); approved.Clear(); accepted.Clear();
            receivedSequence = sequence = 0; lastSentRevision = lastReceivedRevision = -1;
            pendingId = null; matchStarted = false; leaving = false;
        }
        public void Leave()
        {
            leaving = true;
            Unregister();
            if (Network != null && Network.IsListening) Network.Shutdown();
            ResetConnection();
            leaving = true; lanSession = false; token = Guid.NewGuid().ToString("N");
            Status = "已离开房间";
        }
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Hello hello;
            try { hello = request.Payload == null || request.Payload.Length == 0 ? null : JsonUtility.FromJson<Hello>(Encoding.UTF8.GetString(request.Payload)); }
            catch { hello = null; }
            var valid = hello != null && hello.protocol == 2 && hello.contract == Contract && !string.IsNullOrEmpty(hello.token);
            var reserved = valid ? members.Values.FirstOrDefault(m => m.Token == hello.token) : null;
            var reconnect = reserved != null && !reserved.Connected && Time.unscaledTime - reserved.DisconnectedAt <= 60f;
            response.Approved = valid && (reconnect || (!matchStarted && members.Count + approved.Count < seatsRequested));
            response.CreatePlayerObject = false; response.Pending = false;
            if (response.Approved) approved[request.ClientNetworkId] = hello;
            else response.Reason = !valid ? "协议或棋盘版本不一致" : "房间已满、已开局或重连窗口已结束";
        }
        private void Register()
        {
            var messaging = Network.CustomMessagingManager;
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestChannel, ReceiveRequest);
            messaging.RegisterNamedMessageHandler(StateChannel, ReceiveState);
            messaging.RegisterNamedMessageHandler(AckChannel, ReceiveAck);
            messaging.RegisterNamedMessageHandler(DiceChannel, ReceiveDice);
        }
        private void Unregister()
        {
            var messaging = Network?.CustomMessagingManager;
            if (messaging == null) return;
            foreach (var channel in new[] { RequestChannel, StateChannel, AckChannel, DiceChannel }) messaging.UnregisterNamedMessageHandler(channel);
        }
        private void OnConnected(ulong id)
        {
            Register();
            if (Network.IsHost)
            {
                var hello = id == Network.LocalClientId ? new Hello { token = token } : approved.TryGetValue(id, out var value) ? value : new Hello { token = Guid.NewGuid().ToString("N") };
                approved.Remove(id);
                var member = members.Values.FirstOrDefault(m => m.Token == hello.token);
                if (member == null)
                {
                    var color = Enumerable.Range(0,seatsRequested).Select(i => (PlayerColor)i).First(c => !members.ContainsKey(c));
                    member = new Member { Color = color, Token = hello.token };
                    members[color] = member;
                }
                member.ClientId = id; member.Connected = true; member.Ready = id == Network.LocalClientId || matchStarted;
                Status = matchStarted ? "玩家已恢复连接" : "等待所有玩家准备";
                BroadcastState(Array.Empty<MatchEvent>(), true);
            }
            else Status = "已连接，等待房间状态";
        }
        private void OnDisconnected(ulong id)
        {
            if (leaving) return;
            pendingId = null;
            if (Network.IsHost)
            {
                var member = members.Values.FirstOrDefault(m => m.ClientId == id);
                if (member != null)
                {
                    if (matchStarted) { member.Connected = false; member.Ready = false; member.DisconnectedAt = Time.unscaledTime; }
                    else members.Remove(member.Color);
                }
                Status = matchStarted ? "有玩家掉线：60 秒内可重连，或房主选择电脑接管" : "有玩家离开房间";
                BroadcastState(Array.Empty<MatchEvent>(), true);
            }
            else Status = string.IsNullOrEmpty(Network.DisconnectReason) ? "连接断开，可重新连接；房主退出时请返回菜单" : "连接已结束：" + Network.DisconnectReason;
        }
        public void ToggleReady()
        {
            if (!IsNetworked || matchStarted || !LocalColor.HasValue) return;
            if (IsHost) { members[LocalColor.Value].Ready = !LocalReady; BroadcastState(Array.Empty<MatchEvent>(), true); }
            else SendRequest(new Request { kind = "ready", ready = !LocalReady });
        }
        public void RequestColor(int color)
        {
            if (!IsNetworked || matchStarted) return;
            if (IsHost) { if (!ChangeColor(Network.LocalClientId, color)) controller.ShowError("这个席位不可用，请选择房间内的空闲颜色。"); }
            else SendRequest(new Request { kind = "color", color = color });
        }
        private bool ChangeColor(ulong id, int color)
        {
            var member = members.Values.FirstOrDefault(m => m.ClientId == id);
            if (member == null || color < 0 || color >= seatsRequested) return false;
            if (member.Color == (PlayerColor)color) return true;
            if (members.ContainsKey((PlayerColor)color)) return false;
            members.Remove(member.Color); member.Color = (PlayerColor)color; member.Ready = false;
            members[member.Color] = member;
            BroadcastState(Array.Empty<MatchEvent>(), true);
            return true;
        }
        public bool StartHostedMatch()
        {
            if (!CanStart) { Status = "等待所有人准备，或开启电脑补位"; return false; }
            matchStarted = true; matchId = Guid.NewGuid().ToString("N");
            accepted.Clear(); lastSentRevision = -1;
            ApplySeatControls(null);
            controller.BeginNetworkMatch((1 << seatsRequested) - 1);
            Status = "对局进行中";
            BroadcastState(Array.Empty<MatchEvent>(), true);
            return true;
        }
        public void RestartHostedMatch()
        {
            if (!IsHost) return;
            matchStarted = false; matchId = ""; lastSentRevision = -1;
            foreach (var m in members.Values) m.Ready = m.ClientId == Network.LocalClientId;
            controller.ClearNetworkMatch();
            Status = "返回房间，请重新准备";
            BroadcastState(Array.Empty<MatchEvent>(), true);
        }
        public void TakeOverDisconnected()
        {
            if (!IsHost || !matchStarted) return;
            foreach (var m in members.Values.Where(m => !m.Connected).ToArray())
            { controller.Session.Seat(m.Color).Control = SeatControl.Computer; members.Remove(m.Color); }
            Status = "电脑已接管掉线席位";
            BroadcastState(Array.Empty<MatchEvent>(), true);
        }
        public void SendRoll() => SendRequest(new Request { kind = "roll", command = new MatchCommand { ExpectedRevision = controller.Rules.State.Revision } });
        public void SendCommand(MatchCommand command) => SendRequest(new Request { kind = "command", command = command });
        private void SendRequest(Request request)
        {
            if (!IsClientOnly || !Network.IsConnectedClient || HasPendingCommand) return;
            request.id = Guid.NewGuid().ToString("N"); request.session = sessionId; request.match = matchId;
            if (request.kind == "roll" || request.kind == "command") { pendingId = request.id; pendingSince = Time.unscaledTime; }
            SendJson(RequestChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(request));
        }
        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out string json);
            Request request;
            try { request = JsonUtility.FromJson<Request>(json); } catch { return; }
            var member = members.Values.FirstOrDefault(m => m.ClientId == sender && m.Connected);
            if (request == null || member == null || request.session != sessionId) return;
            if (request.kind == "ready" && !matchStarted) { member.Ready = request.ready; BroadcastState(Array.Empty<MatchEvent>(), true); return; }
            if (request.kind == "color" && !matchStarted)
            {
                if (!ChangeColor(sender, request.color)) SendJson(AckChannel, sender, JsonUtility.ToJson(new Ack { id = request.id, accepted = false, error = "这个席位不可用，请选择房间内的空闲颜色。" }));
                return;
            }
            var ok = false;
            var error = "棋局已更新或现在不能行动。";
            if (matchStarted && request.match == matchId && request.command != null && !accepted.Contains(request.id) &&
                request.command.ExpectedRevision == controller.Rules.State.Revision &&
                (request.kind == "roll" || (request.kind == "command" && request.command.Kind != MatchCommandKind.ApplyDice)))
            {
                accepted.Add(request.id);
                ok = controller.AcceptRemoteCommand(member.Color, request.command, request.kind == "roll");
                error = controller.SetupError ?? error;
            }
            SendJson(AckChannel, sender, JsonUtility.ToJson(new Ack { id = request.id, accepted = ok, error = ok ? "" : error }));
        }
        private void ReceiveAck(ulong sender, FastBufferReader reader)
        {
            if (!IsClientOnly || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            var ack = JsonUtility.FromJson<Ack>(json);
            if (ack == null || ack.id != pendingId) return;
            if (!ack.accepted) { pendingId = null; controller.ShowError(ack.error); }
        }
        public void BroadcastDiceStart()
        {
            if (!IsHost) return;
            using (var writer = new FastBufferWriter(1, Allocator.Temp)) Network.CustomMessagingManager.SendNamedMessageToAll(DiceChannel, writer);
        }
        private void ReceiveDice(ulong sender, FastBufferReader reader)
        { if (IsClientOnly && sender == NetworkManager.ServerClientId) controller.PlayNetworkDice(); }
        public void BroadcastState(IReadOnlyList<MatchEvent> events, bool force = false)
        {
            if (!IsHost) return;
            var revision = controller.Rules?.State.Revision ?? -1;
            if (!force && revision == lastSentRevision) return;
            lastSentRevision = revision;
            var packet = new RoomStatePacket
            {
                sequence = ++sequence, sessionId = sessionId, matchId = matchId,
                themeId = DraftlineThemeApplier.CurrentId, themeVersion=DraftlineThemeApplier.Load(DraftlineThemeApplier.CurrentId)?.Version??1,
                environmentVariant=DraftlineThemeApplier.CurrentVariant,environmentSeed=DraftlineThemeApplier.CurrentSeed,
                seatsRequested = seatsRequested, started = matchStarted, fillComputers = FillWithComputers,
                members = members.Values.Select(m => new RoomMemberPacket { clientId = m.ClientId, color = (int)m.Color, connected = m.Connected, ready = m.Ready }).ToArray(),
                controls = controller.Session.Seats.Select(s => (int)s.Control).ToArray(),
                snapshot = controller.Rules == null ? "" : JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)),
                events = events.Select(MatchEventPacket.Capture).ToArray()
            };
            using (var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(JsonUtility.ToJson(packet)), Allocator.Temp))
            {
                writer.WriteValueSafe(JsonUtility.ToJson(packet));
                Network.CustomMessagingManager.SendNamedMessageToAll(StateChannel, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (!IsClientOnly || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            var packet = JsonUtility.FromJson<RoomStatePacket>(json);
            if (packet == null || packet.sequence <= receivedSequence) return;
            var theme=DraftlineThemeApplier.Load(packet.themeId);
            if(theme==null || !theme.Implemented || theme.Version!=packet.themeVersion) {
                Leave();controller.ShowError("房主的棋盘主题与本机版本不一致，请使用相同游戏包。");return;
            }
            if(DraftlineThemeApplier.CurrentId!=theme.ThemeId)DraftlineThemeApplier.Apply(theme.ThemeId);
            if(DraftlineThemeApplier.CurrentVariant!=packet.environmentVariant || DraftlineThemeApplier.CurrentSeed!=packet.environmentSeed)
                DraftlineThemeApplier.ApplyEnvironment(packet.environmentVariant,packet.environmentSeed);
            var newMatch = packet.matchId != matchId;
            receivedSequence = packet.sequence; sessionId = packet.sessionId; matchId = packet.matchId;
            matchStarted = packet.started; seatsRequested = packet.seatsRequested; FillWithComputers = packet.fillComputers;
            members.Clear();
            foreach (var p in packet.members ?? Array.Empty<RoomMemberPacket>())
                members[(PlayerColor)p.color] = new Member { Color = (PlayerColor)p.color, ClientId = p.clientId, Connected = p.connected, Ready = p.ready };
            ApplySeatControls(packet.controls);
            if (newMatch) { controller.ClearNetworkMatch(); lastReceivedRevision = -1; }
            if (!matchStarted) { if (controller.Rules != null) controller.ClearNetworkMatch(); Status = "房间等待准备"; return; }
            Status = HasDisconnectedPlayers ? "有玩家掉线，等待重连或房主处理" : "对局进行中";
            if (string.IsNullOrEmpty(packet.snapshot)) return;
            var snapshot = JsonUtility.FromJson<MatchSnapshot>(packet.snapshot).Restore();
            if (snapshot.Revision <= lastReceivedRevision) return;
            lastReceivedRevision = snapshot.Revision; pendingId = null;
            controller.ReceiveNetworkUpdate(snapshot, (packet.events ?? Array.Empty<MatchEventPacket>()).Select(e => e.Restore()).ToArray());
        }
        private void ApplySeatControls(int[] controls)
        {
            controller.Session.Mode = GameMode.Lan;
            for (var i = 0; i < 4; i++)
            {
                var color = (PlayerColor)i;
                if (i >= seatsRequested) controller.Session.Seat(color).Control = SeatControl.Closed;
                else if (members.TryGetValue(color, out var m))
                    controller.Session.Seat(color).Control = m.ClientId == Network.LocalClientId ? SeatControl.LocalHuman : SeatControl.RemoteHuman;
                else controller.Session.Seat(color).Control = controls != null ? (SeatControl)controls[i] : SeatControl.Computer;
            }
        }
        private void SendJson(string channel, ulong target, string json)
        {
            using (var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(json), Allocator.Temp))
            { writer.WriteValueSafe(json); Network.CustomMessagingManager.SendNamedMessage(channel, target, writer, NetworkDelivery.ReliableSequenced); }
        }
        private void Update()
        {
            if (HasPendingCommand && Time.unscaledTime - pendingSince > 45f)
            { pendingId = null; controller.ShowError("请求等待超时，请检查连接并重试。"); }
        }
        private static string LocalAddresses()
        {
            try { return string.Join(" / ", Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Select(a => a.ToString())); }
            catch { return "请查看 Windows 网络设置"; }
        }
    }
}
