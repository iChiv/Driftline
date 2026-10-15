using UnityEngine;
using UnityEngine.EventSystems;

namespace Draftline.Unity
{
    public sealed class InventoryHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public DraftlineCanvasUI View;
        public int Slot;
        public void OnPointerEnter(PointerEventData data)=>View.SetHoveredItem(Slot,true);
        public void OnPointerExit(PointerEventData data)=>View.SetHoveredItem(Slot,false);
    }
}
