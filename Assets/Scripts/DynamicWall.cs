using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class DynamicWall : MonoBehaviour
{
    [Header("墙壁类型 (0=常驻, 1=外圈, 2=内圈)")]
    [Range(0, 2)]
    public int wallType = 0; 

    [Header("位移配置 (仅对1和2有效)")]
    public Vector3 moveDirection;
    public Vector3 endPosition;
    public float gridSize = 1.0f;                  
    private Vector3 initialPosition;
    private Vector3 nextPosition; 
    private Collider myCollider;
    private MeshRenderer myRenderer;

    void Awake()
    {
        myCollider = GetComponent<Collider>();
        myRenderer = GetComponent<MeshRenderer>();
        nextPosition = transform.position;
        initialPosition = transform.position; 
        
    }

    // 当不属于自己的回合时：隐藏并暗中往前挪一格（常驻墙0会直接跳过此逻辑）
    public void HideAndAdvance()
    {
        if (wallType == 0) return; // 常驻墙不动、不隐藏

        // 1. 隐藏自己
        myCollider.enabled = false;
        myRenderer.enabled = false;
        gameObject.tag = "Untagged";

        // 2. 暗中位移一格
        if (transform.position.Equals(endPosition))
        {
            nextPosition = initialPosition;
        }
        else
        {
            nextPosition = transform.position + moveDirection.normalized * gridSize;
        }
        transform.position = nextPosition;
    }

    // 当轮到自己出现的回合时：检查棋子挤压保护并尝试现身
    public void TryToSpawn()
    {
        if (wallType == 0) return; // 常驻墙始终保持显示

        // 1. 扫描当前目标格点有没有【PieceLayer（棋子层）】的物体
        int pieceLayerMask = LayerMask.GetMask("PieceLayer");
        bool isPieceHere = Physics.CheckSphere(transform.position + Vector3.up * 0.1f, 0.35f, pieceLayerMask);

        if (isPieceHere)
        {
            // 🔴 保护触发：有棋子，本回合直接不出现
            myCollider.enabled = false;
            myRenderer.enabled = false;
            gameObject.tag = "Untagged"; 
            Debug.Log($"[墙壁调度] 墙壁 {gameObject.name} (类型{wallType}) 被棋子占用，本回合不予生成。");
        }
        else
        {
            // 🟢 安全：正常现身
            myCollider.enabled = true;
            myRenderer.enabled = true;
            gameObject.tag = "Wall"; 
        }
    }
}