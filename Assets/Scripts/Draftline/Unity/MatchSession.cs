using System;
using System.Linq;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    // Keep the LAN serialized value so existing scene/network configuration stays valid.
    public enum GameMode { Local = 0, Lan = 2 }
    public enum SeatControl { Closed, LocalHuman, Computer, RemoteHuman }
    public enum ComputerDifficulty { Easy, Normal }

    [Serializable]
    public sealed class MatchSeat
    {
        public PlayerColor Color;
        public SeatControl Control;
        public ComputerDifficulty Difficulty = ComputerDifficulty.Normal;
        public string Name;
    }

    [DisallowMultipleComponent]
    public sealed class MatchSession : MonoBehaviour
    {
        public GameMode Mode = GameMode.Local;
        public MatchSeat[] Seats =
        {
            new MatchSeat { Color = PlayerColor.Red, Control = SeatControl.LocalHuman, Name = "红方" },
            new MatchSeat { Color = PlayerColor.Blue, Control = SeatControl.Computer, Name = "蓝方" },
            new MatchSeat { Color = PlayerColor.Green, Control = SeatControl.Closed, Name = "绿方" },
            new MatchSeat { Color = PlayerColor.Yellow, Control = SeatControl.Closed, Name = "黄方" }
        };
        public MatchSeat Seat(PlayerColor color) => Seats[(int)color];
        public PlayerColor[] ActiveColors => Seats.Where(s => s.Control != SeatControl.Closed).Select(s => s.Color).ToArray();

        public void Configure(GameMode mode)
        {
            Mode = mode;
            for (var i = 0; i < 4; i++)
                Seats[i].Control = i == 0 ? SeatControl.LocalHuman : i == 1 ? SeatControl.Computer : SeatControl.Closed;
        }

        private void OnValidate() { if (Mode != GameMode.Lan) Mode = GameMode.Local; }

        public bool Validate(out string error)
        {
            if (ActiveColors.Length < 2) { error = "至少需要两方参加。"; return false; }
            var humans = Seats.Count(s => s.Control == SeatControl.LocalHuman);
            if (Mode == GameMode.Local && humans < 1)
            { error = "至少保留一位本地玩家。"; return false; }
            if (Mode == GameMode.Local && Seats.Any(s => s.Control == SeatControl.RemoteHuman))
            { error = "本地游戏不能配置远程玩家。"; return false; }
            error = null;
            return true;
        }
    }
}
