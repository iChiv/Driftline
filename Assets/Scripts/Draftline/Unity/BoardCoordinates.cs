using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Maps the existing rotated SampleScene board to HTML rule coordinates.</summary>
    public static class BoardCoordinates
    {
        public const float CellSize = 1f;

        // HTML (column,row)=(1,1) is red's spawn at world (-8,-8).
        // The existing scene is rotated relative to the HTML screenshot.
        public static Vector3 ToWorld(GridPosition position, float height = 0.6f) =>
            new Vector3((position.Row - 9) * CellSize, height,
                (position.Column - 9) * CellSize);

        public static bool TryFromWorld(Vector3 world, out GridPosition position)
        {
            position = new GridPosition(
                Mathf.RoundToInt(world.z / CellSize + 9),
                Mathf.RoundToInt(world.x / CellSize + 9));
            return BoardLayout.Contains(position);
        }
    }
}
