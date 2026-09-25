using System;
using System.Collections.Generic;

namespace Draftline.Core
{
    public enum CellKind
    {
        Floor,
        FixedWall,
        PlayerGate,
        Spawn,
        Portal,
        RotatingWallSlot
    }

    public enum PlayerColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3
    }

    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public readonly int Column;
        public readonly int Row;

        public GridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public GridPosition Offset(int columns, int rows) =>
            new GridPosition(Column + columns, Row + rows);

        public bool Equals(GridPosition other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => (Column * 397) ^ Row;
        public override string ToString() => $"({Column},{Row})";
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
    }

    public readonly struct CellDefinition
    {
        public readonly CellKind Kind;
        public readonly PlayerColor Owner;
        public readonly char PortalKey;
        public readonly bool InnerRing;
        public readonly int SlotIndex;

        public CellDefinition(CellKind kind, PlayerColor owner = PlayerColor.Red,
            char portalKey = '\0', bool innerRing = false, int slotIndex = -1)
        {
            Kind = kind;
            Owner = owner;
            PortalKey = portalKey;
            InnerRing = innerRing;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>Discrete rule map shared by the HTML prototype and the Unity scene.</summary>
    public sealed class BoardLayout
    {
        public const int Size = 19;

        // '#' is floor and '.' is fixed wall, matching html-prototype/index.html RAW.
        private static readonly string[] Rows =
        {
            ".....#########.....",
            ".S##..#######..##S.",
            ".####..#####..####.",
            ".##.GG.#####.GG.##.",
            "..#G##.O...C.##G#..",
            "#..G#####.#####G..#",
            "##...#w#####x#...##",
            "####M#####b###M####",
            "####.##a######.####",
            "####..###.###..####",
            "####.######c##.####",
            "####R###d#####R####",
            "##...#z#####y#...##",
            "#..G#####.#####G..#",
            "..#G##.O...C.##G#..",
            ".##.GG.#####.GG.##.",
            ".####..#####..####.",
            ".S##..#######..##S.",
            ".....#########....."
        };

        private readonly CellDefinition[,] cells = new CellDefinition[Size, Size];
        private readonly Dictionary<char, GridPosition[]> portals = new Dictionary<char, GridPosition[]>();
        private readonly GridPosition[] spawns = new GridPosition[4];
        private readonly List<GridPosition> rotatingSlots = new List<GridPosition>(8);

        public IReadOnlyList<GridPosition> RotatingSlots => rotatingSlots;

        public BoardLayout()
        {
            var portalPairs = new Dictionary<char, List<GridPosition>>();
            for (var row = 0; row < Size; row++)
            {
                if (Rows[row].Length != Size)
                    throw new InvalidOperationException($"Board row {row} is not {Size} cells wide.");

                for (var column = 0; column < Size; column++)
                {
                    var position = new GridPosition(column, row);
                    var symbol = Rows[row][column];
                    var owner = CornerOwner(position);
                    CellDefinition cell;

                    switch (symbol)
                    {
                        case '#': cell = new CellDefinition(CellKind.Floor); break;
                        case '.': cell = new CellDefinition(CellKind.FixedWall); break;
                        case 'G': cell = new CellDefinition(CellKind.PlayerGate, owner); break;
                        case 'S':
                            cell = new CellDefinition(CellKind.Spawn, owner);
                            spawns[(int)owner] = position;
                            break;
                        case 'O': case 'C': case 'M': case 'R':
                            cell = new CellDefinition(CellKind.Portal, portalKey: symbol);
                            if (!portalPairs.TryGetValue(symbol, out var pair))
                                portalPairs[symbol] = pair = new List<GridPosition>(2);
                            pair.Add(position);
                            break;
                        default:
                            if (symbol >= 'a' && symbol <= 'd')
                                cell = new CellDefinition(CellKind.RotatingWallSlot,
                                    innerRing: true, slotIndex: symbol - 'a');
                            else if (symbol >= 'w' && symbol <= 'z')
                                cell = new CellDefinition(CellKind.RotatingWallSlot,
                                    innerRing: false, slotIndex: symbol - 'w');
                            else
                                throw new InvalidOperationException($"Unknown board symbol '{symbol}' at {position}.");
                            rotatingSlots.Add(position);
                            break;
                    }

                    cells[column, row] = cell;
                }
            }

            foreach (var pair in portalPairs)
            {
                if (pair.Value.Count != 2)
                    throw new InvalidOperationException($"Portal {pair.Key} must have exactly two cells.");
                portals.Add(pair.Key, pair.Value.ToArray());
            }
        }

        public static bool Contains(GridPosition position) =>
            position.Column >= 0 && position.Column < Size &&
            position.Row >= 0 && position.Row < Size;

        public CellDefinition CellAt(GridPosition position)
        {
            if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return cells[position.Column, position.Row];
        }

        public GridPosition SpawnOf(PlayerColor player) => spawns[(int)player];

        public GridPosition PortalExit(GridPosition entry)
        {
            var cell = CellAt(entry);
            if (cell.Kind != CellKind.Portal)
                throw new ArgumentException("Cell is not a portal.", nameof(entry));
            var pair = portals[cell.PortalKey];
            return pair[0] == entry ? pair[1] : pair[0];
        }

        public bool IsScheduledWall(GridPosition position, int phase)
        {
            var cell = CellAt(position);
            if (cell.Kind != CellKind.RotatingWallSlot)
                throw new ArgumentException("Cell is not a rotating wall slot.", nameof(position));
            var evenSlot = cell.SlotIndex % 2 == phase % 2;
            return cell.InnerRing ? evenSlot : !evenSlot;
        }

        private static PlayerColor CornerOwner(GridPosition position)
        {
            if (position.Column < 9 && position.Row < 9) return PlayerColor.Red;
            if (position.Column > 9 && position.Row < 9) return PlayerColor.Blue;
            if (position.Column > 9 && position.Row > 9) return PlayerColor.Green;
            return PlayerColor.Yellow;
        }
    }
}
