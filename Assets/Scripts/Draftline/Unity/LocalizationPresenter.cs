using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Draftline.Unity
{
    /// <summary>Translates saved and dynamic uGUI views at render time, retaining the source text for switching back.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class LocalizationPresenter : MonoBehaviour
    {
        public Dropdown LanguageSelector;
        sealed class TextState {public string Source,Rendered;}
        readonly Dictionary<Text,TextState> texts=new Dictionary<Text,TextState>();
        bool refreshing;
        void Awake()
        {
            GameLocalization.Initialize();LanguageSelector.SetValueWithoutNotify((int)GameLocalization.Language);
            LanguageSelector.onValueChanged.AddListener(value=>GameLocalization.SetLanguage((GameLanguage)value));
        }
        void OnEnable(){GameLocalization.Changed+=Refresh;}
        void OnDisable(){GameLocalization.Changed-=Refresh;}
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(refreshing)return;refreshing=true;
            try
            {
                LanguageSelector.SetValueWithoutNotify((int)GameLocalization.Language);
                foreach(var dropdown in FindObjectsOfType<Dropdown>(true))
                {
                    bool changed=false;
                    foreach(var option in dropdown.options){var rendered=GameLocalization.Translate(option.text);if(rendered!=option.text){option.text=rendered;changed=true;}}
                    if(changed)dropdown.RefreshShownValue();
                }
                foreach(var text in FindObjectsOfType<Text>(true))
                {
                    if(!texts.TryGetValue(text,out var state)){state=new TextState();texts.Add(text,state);}
                    if(text.text!=state.Rendered)state.Source=GameLocalization.Original(text.text);
                    state.Rendered=GameLocalization.Translate(state.Source);if(text.text!=state.Rendered)text.text=state.Rendered;
                }
                if(texts.Count>400)foreach(var dead in texts.Keys.Where(t=>t==null).ToArray())texts.Remove(dead);
            }
            finally{refreshing=false;}
        }
    }
}
