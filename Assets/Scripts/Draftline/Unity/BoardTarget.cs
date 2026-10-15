using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    public sealed class BoardTarget : MonoBehaviour
    {
        public int PieceId, Revision, StopIndex = -1;
        public SlideDirection Direction;
        public bool Deploy, DirectionChoice;
        public GridPosition Cell;
    }
}
