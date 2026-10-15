using System.Linq;
using Draftline.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Draftline.EditorTools
{
    public static class DraftlineLocalizationSetup
    {
        [MenuItem("Driftline/Install Chinese English Selector")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Apply in Edit Mode.");
            const string path="Assets/UI/Prefabs/UIRoot.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui=root.GetComponent<DraftlineCanvasUI>();var card=ui.MainMenu.transform.Find("MenuCard");
                var presenter=root.GetComponent<LocalizationPresenter>()??root.AddComponent<LocalizationPresenter>();
                var selector=card.Find("LanguageSelector");
                if(selector==null){selector=Object.Instantiate(ui.Difficulty.gameObject,card).transform;selector.name="LanguageSelector";}
                presenter.LanguageSelector=selector.GetComponent<Dropdown>();presenter.LanguageSelector.ClearOptions();presenter.LanguageSelector.AddOptions(new System.Collections.Generic.List<string>{"中文","English"});
                var rect=(RectTransform)selector;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(214,-112);rect.sizeDelta=new Vector2(210,44);
                var node=card.Find("LanguageLabel");if(node==null){node=Object.Instantiate(card.Find("BoardThemeLabel").gameObject,card).transform;node.name="LanguageLabel";}
                node.GetComponent<Text>().text="语言";rect=(RectTransform)node;rect.anchoredPosition=new Vector2(36,-119);rect.sizeDelta=new Vector2(160,30);
                rect=(RectTransform)card.Find("BoardThemeLabel");rect.anchoredPosition=new Vector2(36,-182);
                rect=(RectTransform)ui.ThemeSelector.transform;rect.anchoredPosition=new Vector2(36,-220);
                rect=(RectTransform)ui.LocalGame.transform;rect.anchoredPosition=new Vector2(36,-306);
                rect=(RectTransform)ui.Lan.transform;rect.anchoredPosition=new Vector2(36,-382);
                rect=(RectTransform)ui.GuideMenu.transform;rect.anchoredPosition=new Vector2(36,-458);
                foreach(var button in new[]{ui.OpenSettings,ui.Quit}){rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-548);}
                ui.GuideClose.GetComponentInChildren<Text>(true).text="返回";
                ((RectTransform)card).sizeDelta=new Vector2(460,650);
                foreach(var text in root.GetComponentsInChildren<Text>(true))
                {text.resizeTextForBestFit=true;text.resizeTextMaxSize=text.fontSize;text.resizeTextMinSize=Mathf.Max(13,text.fontSize-4);}
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
    }
}
