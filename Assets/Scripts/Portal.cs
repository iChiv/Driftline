using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("对应的另一个传送门")]
    public Portal targetPortal; 

    [Header("穿出该传送门后的方向")]
    public Vector3 exitDirection;

    private Renderer rend;
    private Color originalColor;
    private bool isFlashing = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // 记录门原本的紫色，方便闪烁完恢复
            originalColor = rend.material.GetColor("_BaseColor");
        }
    }

    // 提供给棋子调用的公共方法：触发闪烁特效
    public void TriggerFlash()
    {
        if (rend != null && !isFlashing)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        isFlashing = true;

        // 1. 瞬间变白（高亮闪烁）
        rend.material.SetColor("_BaseColor", Color.white);
        
        // 如果你的材质开启了自发光(Emission)，还可以顺便拉高自发光强度
        // rend.material.SetColor("_EmissionColor", Color.white * 4f); 

        // 2. 等待极短的时间（如 0.08 秒）
        yield return new WaitForSeconds(0.08f);

        // 3. 恢复原本的紫色
        rend.material.SetColor("_BaseColor", originalColor);
        // rend.material.SetColor("_EmissionColor", originalColor * 0f);

        isFlashing = false;
    }

    void OnDrawGizmos()
    {
        if (targetPortal != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, targetPortal.transform.position);
        }
    }
}
