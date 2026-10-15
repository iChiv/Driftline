using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Explicit cell binding to the existing rule-controlled animation driver.</summary>
    public sealed class ThemeMovingWall : MonoBehaviour
    {
        public int Column, Row;
        public Transform Body;
        public int GeometryVersion;
        [System.NonSerialized] public Renderer Driver;
        private float driverY, driverScaleY;
        public void Bind(Renderer driver,Vector3 restPosition,Vector3 restScale)
        {
            Driver=driver;
            if(driver==null)return;
            driverY=restPosition.y;driverScaleY=restScale.y;
            Follow();
        }
        private void LateUpdate() => Follow();
        private void Follow()
        {
            if(Driver==null || Body==null)return;
            Body.gameObject.SetActive(Driver.enabled);
            Body.localPosition=new Vector3(0,Driver.transform.position.y-driverY,0);
            Body.localScale=new Vector3(1,driverScaleY>.001f?Driver.transform.localScale.y/driverScaleY:1,1);
        }
    }
}
