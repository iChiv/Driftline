using Draftline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Draftline.Unity
{
    public sealed class BoardContextMenu : MonoBehaviour
    {
        public Button[] ItemButtons;
        public Text[] ItemLabels;
        public Text Description;
        private DraftlineGameController controller;
        public void Initialize(DraftlineGameController owner)
        {
            controller=owner;
            GetComponent<Canvas>().worldCamera=Camera.main;
            var items=controller.Rules.State.Players[(int)controller.Rules.State.CurrentPlayer].Items;
            for(var i=0;i<ItemButtons.Length;i++)
            {
                var slot=i;
                ItemButtons[i].gameObject.SetActive(i<items.Count);
                if(i>=items.Count)continue;
                var item=items[i];
                ItemLabels[i].text=MatchLabels.Item(item);
                var tooltip=ItemButtons[i].GetComponent<BoardItemAction>();
                if(tooltip!=null){tooltip.Type=item;tooltip.Description=Description;tooltip.View=controller.CanvasUI;}
                ItemButtons[i].onClick.AddListener(()=>controller.UseItemFromUI(items[slot]));
                ItemButtons[i].GetComponent<Image>().color=controller.BoardInteraction.PendingItem==item
                    ? new Color(.95f,.75f,.38f) : new Color(.10f,.16f,.20f);
                ItemLabels[i].color=controller.BoardInteraction.PendingItem==item?new Color(.05f,.08f,.10f):Color.white;
            }
            Description.text="道具 · 点目标确认";
        }
        private void LateUpdate()
        {
            if(Camera.main!=null)transform.rotation=Camera.main.transform.rotation;
        }
    }
}
