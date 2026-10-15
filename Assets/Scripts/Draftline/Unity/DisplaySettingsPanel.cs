using UnityEngine;
using UnityEngine.UI;

namespace Draftline.Unity
{
    /// <summary>Saved UI bindings; display changes are local and confirmed before persistence.</summary>
    public sealed class DisplaySettingsPanel : MonoBehaviour
    {
        public Dropdown Resolution,Mode;
        public Button Apply,Keep,Revert;
        public GameObject SettingsPage,Confirmation;
        public Text ConfirmationText;
        public float ConfirmationSeconds=12f;
        public static readonly Vector2Int[] Presets={new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160)};
        public const string WidthKey="Driftline.Display.Width",HeightKey="Driftline.Display.Height",ModeKey="Driftline.Display.Mode";
        public bool Pending {get;private set;}
        public Vector2Int RequestedSize {get;private set;}
        public FullScreenMode RequestedMode {get;private set;}
        Vector2Int previousSize;
        FullScreenMode previousMode;
        float deadline;
        bool initialized;
        void Start()
        {
            Initialize();
            if(!Application.isEditor&&PlayerPrefs.HasKey(WidthKey))
            {
                var size=new Vector2Int(PlayerPrefs.GetInt(WidthKey),PlayerPrefs.GetInt(HeightKey));
                if(System.Array.IndexOf(Presets,size)>=0)
                    SetDisplay(size,PlayerPrefs.GetInt(ModeKey)==1?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
                SyncSelections();
            }
        }
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            RequestedSize=new Vector2Int(Screen.width,Screen.height);RequestedMode=Screen.fullScreenMode;
            Apply.onClick.AddListener(ApplySelection);Keep.onClick.AddListener(Confirm);Revert.onClick.AddListener(Cancel);
            SyncSelections();Confirmation.SetActive(false);
        }
        void SyncSelections()
        {
            int index=System.Array.IndexOf(Presets,RequestedSize);
            if(index<0){index=0;for(int i=1;i<Presets.Length;i++)if(Mathf.Abs(Presets[i].x-RequestedSize.x)<Mathf.Abs(Presets[index].x-RequestedSize.x))index=i;}
            Resolution.SetValueWithoutNotify(index);Mode.SetValueWithoutNotify(RequestedMode==FullScreenMode.Windowed?0:1);
        }
        public void ApplySelection()
        {
            if(Pending)return;
            previousSize=RequestedSize;previousMode=RequestedMode;
            Pending=true;deadline=Time.unscaledTime+ConfirmationSeconds;
            SetDisplay(Presets[Resolution.value],Mode.value==0?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow);
            Confirmation.SetActive(true);RefreshControls();
        }
        void SetDisplay(Vector2Int size,FullScreenMode mode)
        {
            RequestedSize=size;RequestedMode=mode;
            // Game View is an Editor preview; only standalone owns the OS window.
            if(!Application.isEditor)Screen.SetResolution(size.x,size.y,mode);
        }
        public void Confirm()
        {
            if(!Pending)return;
            PlayerPrefs.SetInt(WidthKey,RequestedSize.x);PlayerPrefs.SetInt(HeightKey,RequestedSize.y);
            PlayerPrefs.SetInt(ModeKey,RequestedMode==FullScreenMode.Windowed?0:1);PlayerPrefs.Save();
            Finish();
        }
        public void Cancel()
        {
            if(!Pending)return;
            SetDisplay(previousSize,previousMode);SyncSelections();Finish();
        }
        void Finish(){Pending=false;Confirmation.SetActive(false);RefreshControls();}
        void RefreshControls(){Resolution.interactable=Mode.interactable=Apply.interactable=!Pending;}
        void Update()
        {
            if(!Pending)return;
            if(!SettingsPage.activeInHierarchy||Time.unscaledTime>=deadline){Cancel();return;}
            ConfirmationText.text="保留显示设置？ "+Mathf.CeilToInt(deadline-Time.unscaledTime)+" 秒";
        }
        void OnDisable(){if(Pending)Cancel();}
    }
}
