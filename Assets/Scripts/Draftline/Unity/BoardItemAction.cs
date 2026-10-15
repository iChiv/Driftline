using Draftline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Draftline.Unity
{
    public sealed class BoardItemAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ItemType Type;
        public Text Description;
        public DraftlineCanvasUI View;
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(Description!=null)Description.text=MatchLabels.Item(Type)+" · 查看左侧说明";
            View?.SetHoveredItemType(Type);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if(Description!=null)Description.text="道具 · 点目标确认";
            View?.SetHoveredItemType(null);
        }
    }
}
