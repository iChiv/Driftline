using Draftline.Unity;
using UnityEditor;
using UnityEngine;

namespace Draftline.EditorTools
{
    [CustomEditor(typeof(DraftlineCanvasUI))]
    public sealed class DraftlineCanvasUIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var view=(DraftlineCanvasUI)target;
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("预览只切换保存的页面，不启动对局，也不重新生成控件。直接编辑子节点或 Prefab。",MessageType.Info);
                var pages=new[]{view.MainMenu,view.Setup,view.Lobby,view.Hud,view.Settings,view.Result,view.GuidePanel};
                var names=new[]{"主菜单","席位配置","LAN 房间","对局 HUD","设置","结算","规则引导"};
                for(var i=0;i<pages.Length;i++)
                    if(GUILayout.Button("预览 · "+names[i]))
                    {
                        for(var j=0;j<pages.Length;j++)
                        {
                            if(pages[j]==null)continue;
                            Undo.RecordObject(pages[j],"Preview saved UI page");
                            pages[j].SetActive(i==j||(i>=4&&j==3));
                            EditorUtility.SetDirty(pages[j]);
                        }
                    }
            }
            DrawDefaultInspector();
        }
    }
}
