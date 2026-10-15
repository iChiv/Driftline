using System;
using System.Collections.Generic;
using Draftline.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Draftline.EditorTools
{
    /// <summary>Explicit, one-time migration of the saved HUD; never runs in a player.</summary>
    public static class DraftlinePresentationPolish
    {
        const string PrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";
        static readonly Color Card = new Color(.065f,.085f,.105f,.95f);
        static readonly Color Gold = new Color(.97f,.76f,.40f);

        [MenuItem("Driftline/Polish/Apply Compact HUD And Board Markers")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply outside Play Mode.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ui = root.GetComponent<DraftlineCanvasUI>();
                var panel = ui.Hud.transform.Find("PlayerPanel");
                // A migration marker protects subsequent Inspector edits.
                if (panel.Find("TurnCard") != null) throw new InvalidOperationException("Compact HUD already exists; edit the saved Prefab directly.");
                Place(panel,20,-20,310,1040);
                panel.GetComponent<Image>().color = Color.clear;
                panel.GetComponent<Image>().raycastTarget = false;
                var turn = CardPanel(panel,"TurnCard",0,0,310,388);
                ui.TurnAccent = Stripe(turn,"TurnAccent",0,0,4,388,Gold);
                Move(panel.Find("Brand"),turn,20,-16,190,34,24);
                Move(ui.GuideHud.transform,turn,226,-18,66,32);
                ui.GuideHud.GetComponentInChildren<Text>().fontSize = 16;
                Move(ui.TurnText.transform,turn,20,-66,270,64,23);
                Move(ui.PhaseText.transform,turn,20,-138,270,30,17);
                ui.PhaseText.color = new Color(.70f,.77f,.80f);
                Move(ui.DiceText.transform,turn,20,-182,270,38,20);
                Move(ui.HintText.transform,turn,20,-235,270,78,18);
                Move(ui.Roll.transform,turn,20,-324,270,44);
                ui.Roll.GetComponentInChildren<Text>().fontSize = 19;

                var inventory = CardPanel(panel,"InventoryCard",0,-410,310,192);
                ui.InventoryPanel = inventory.gameObject;
                Move(panel.Find("InventoryTitle"),inventory,20,-16,270,30,18);
                Move(ui.ItemButtons[0].transform,inventory,20,-58,130,42);
                Move(ui.ItemButtons[1].transform,inventory,160,-58,130,42);
                Move(ui.InventoryHint.transform,inventory,20,-112,270,66,16);

                var roster = CardPanel(panel,"PlayerRoster",0,82,310,0);
                var rosterRect = (RectTransform)roster;
                rosterRect.anchorMin = rosterRect.anchorMax = Vector2.zero;
                rosterRect.pivot = Vector2.zero;
                var layout = roster.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(16,16,12,12);
                layout.spacing = 8;
                layout.childControlHeight = layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                var fitter = roster.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ui.PlayerAccents = new Image[4];
                for (int i=0;i<4;i++)
                {
                    var row = CardPanel(roster,"Seat"+i,0,0,278,64);
                    row.GetComponent<Image>().color = new Color(.14f,.18f,.21f,.8f);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                    ui.PlayerAccents[i] = Stripe(row,"FactionAccent",0,0,3,64,MatchLabels.Color((Draftline.Core.PlayerColor)i));
                    Move(ui.PlayerTexts[i].transform,row,12,-6,252,52,16);
                }
                Move(ui.CameraToggle.transform,panel,0,8,149,44);
                BottomLeft((RectTransform)ui.CameraToggle.transform);
                Move(ui.HudSettings.transform,panel,161,8,149,44);
                BottomLeft((RectTransform)ui.HudSettings.transform);
                ui.CameraToggle.GetComponentInChildren<Text>().fontSize = 18;
                ui.HudSettings.GetComponentInChildren<Text>().fontSize = 18;

                var objective = ui.Hud.transform.Find("Objective");
                Place(objective,-20,-20,310,64); RightTop((RectTransform)objective);
                var goalText = objective.GetComponentInChildren<Text>();
                Place(goalText.transform,16,-12,278,42); goalText.fontSize = 17;
                var action = ui.ActionText.transform.parent;
                Place(action,-20,-104,310,370); RightTop((RectTransform)action);
                Place(ui.ActionTitle.transform,18,-16,274,34); ui.ActionTitle.fontSize = 21;
                Place(ui.ActionText.transform,18,-66,274,280); ui.ActionText.fontSize = 17;
                var log = ui.LogText.transform.parent;
                Place(log,-20,26,310,192);
                var logRect=(RectTransform)log;
                logRect.anchorMin=logRect.anchorMax=new Vector2(1,0); logRect.pivot=new Vector2(1,0);
                Place(ui.LogText.transform,18,-14,274,164); ui.LogText.fontSize = 15;
                foreach(var card in new[]{objective,action,log})
                {
                    card.GetComponent<Image>().color=Card;
                    Stripe(card,"HeaderAccent",0,0,310,2,Gold);
                }
                foreach(var text in ui.Hud.GetComponentsInChildren<Text>(true)) text.raycastTarget=false;
                ConfigureActionScroll(ui,action);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PolishMarkers();
            AssetDatabase.SaveAssets();
        }

        public static void UpgradeActionScroll()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Apply outside Play Mode.");
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ui=root.GetComponent<DraftlineCanvasUI>();
                if(ui.ActionScroll!=null)return;
                ConfigureActionScroll(ui,ui.Hud.transform.Find("ActionExplanation"));
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static void ConfigureActionScroll(DraftlineCanvasUI ui,Transform action)
        {
            ui.ActionPanel=action.gameObject;
            var area=new GameObject("ExplanationScroll",typeof(RectTransform),typeof(ScrollRect));
            area.transform.SetParent(action,false); Place(area.transform,18,-66,274,280);
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
            viewport.transform.SetParent(area.transform,false);
            var viewportRect=(RectTransform)viewport.transform;
            viewportRect.anchorMin=Vector2.zero; viewportRect.anchorMax=Vector2.one; viewportRect.sizeDelta=Vector2.zero;
            viewport.GetComponent<Image>().color=Color.clear;
            var content=ui.ActionText.rectTransform; content.SetParent(viewport.transform,false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(0,1);
            content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            ui.ActionText.verticalOverflow=VerticalWrapMode.Overflow;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=area.GetComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewportRect;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=30;ui.ActionScroll=scroll;
        }

        static void PolishMarkers()
        {
            var style=AssetDatabase.LoadAssetAtPath<BoardInteractionStyle>("Assets/UI/Config/BoardInteractionStyle.asset");
            style.TargetScale=new Vector3(.76f,.06f,.76f);
            style.PathWidth=.105f;
            style.Normal=new Color(.24f,.96f,.76f);
            style.Stop=new Color(1f,.73f,.23f);
            const string meshPath="Assets/Prefabs/BoardInteraction/StopMarker.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                mesh=new Mesh{name="Beveled stop marker"};
                var outline=new[]{new Vector2(-.33f,-.5f),new Vector2(.33f,-.5f),new Vector2(.5f,-.33f),new Vector2(.5f,.33f),new Vector2(.33f,.5f),new Vector2(-.33f,.5f),new Vector2(-.5f,.33f),new Vector2(-.5f,-.33f)};
                var vertices=new List<Vector3>(); var triangles=new List<int>();
                for(int i=0;i<8;i++)
                {
                    var a=outline[i]; var b=outline[(i+1)%8]; int start=vertices.Count;
                    vertices.Add(new Vector3(0,1,0)); vertices.Add(new Vector3(b.x,1,b.y)); vertices.Add(new Vector3(a.x,1,a.y));
                    vertices.Add(new Vector3(a.x,1,a.y)); vertices.Add(new Vector3(b.x,1,b.y)); vertices.Add(new Vector3(b.x,-1,b.y));
                    vertices.Add(new Vector3(a.x,1,a.y)); vertices.Add(new Vector3(b.x,-1,b.y)); vertices.Add(new Vector3(a.x,-1,a.y));
                    for(int j=0;j<9;j++)triangles.Add(start+j);
                }
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,meshPath);
            }
            style.StopTargetMesh=mesh; EditorUtility.SetDirty(style);
            const string path="Assets/Prefabs/BoardInteraction/BoardTarget.prefab";
            var target=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(target.transform.Find("Contrast Rim")==null)
                {
                    var rim=new GameObject("Contrast Rim",typeof(MeshFilter),typeof(MeshRenderer));
                    rim.transform.SetParent(target.transform,false);
                    rim.transform.localPosition=new Vector3(0,-.65f,0);
                    rim.transform.localScale=new Vector3(1.18f,1.1f,1.18f);
                    rim.GetComponent<MeshFilter>().sharedMesh=target.GetComponent<MeshFilter>().sharedMesh;
                    const string materialPath="Assets/Prefabs/BoardInteraction/MarkerContrast.mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null)
                    {
                        material=new Material(target.GetComponent<Renderer>().sharedMaterial){name="MarkerContrast"};
                        material.SetColor("_BaseColor",new Color(.025f,.045f,.06f));
                        material.SetColor("_Color",new Color(.025f,.045f,.06f));
                        AssetDatabase.CreateAsset(material,materialPath);
                    }
                    rim.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                foreach(var renderer in target.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows=false;
                }
                PrefabUtility.SaveAsPrefabAsset(target,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(target);}
        }

        static RectTransform CardPanel(Transform parent,string name,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=Card; Place(go.transform,x,y,width,height); return (RectTransform)go.transform;
        }
        static Image Stripe(Transform parent,string name,float x,float y,float width,float height,Color color)
        {
            var rect=CardPanel(parent,name,x,y,width,height); var image=rect.GetComponent<Image>();
            image.color=color; image.raycastTarget=false; return image;
        }
        static void Move(Transform child,Transform parent,float x,float y,float width,float height,int font=0)
        {
            child.SetParent(parent,false); Place(child,x,y,width,height);
            if(font>0)child.GetComponent<Text>().fontSize=font;
        }
        static void Place(Transform target,float x,float y,float width,float height)
        {
            var rect=(RectTransform)target;
            rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,y); rect.sizeDelta=new Vector2(width,height);
        }
        static void BottomLeft(RectTransform rect){rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;}
        static void RightTop(RectTransform rect){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);}
    }
}
