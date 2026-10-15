using System.Linq;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Batches saved, immutable scenery once. Moving walls, pieces and text stay independent.</summary>
    public sealed class ThemeStaticGeometry : MonoBehaviour
    {
        public GameObject[] StaticParts;
        private void Awake()
        {
            var objects=StaticParts.Where(p=>p!=null).SelectMany(p=>p.GetComponentsInChildren<MeshFilter>(true))
                .Where(f=>f.sharedMesh!=null&&f.sharedMesh.isReadable&&f.GetComponent<MeshRenderer>()!=null)
                .Select(f=>f.gameObject).ToArray();
            if(objects.Length>0)StaticBatchingUtility.Combine(objects,gameObject);
        }
    }
}
