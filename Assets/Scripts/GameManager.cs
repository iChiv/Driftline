using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // 使用结构体，可以在 Inspector 面板里直观地为每个玩家分配多个棋子
    [System.Serializable]
    public struct PlayerConfig
    {
        public string playerName;        // 玩家名字（如：红方、蓝方）
        public Transform[] myPieces;     // 该玩家控制的所有棋子物体
    }

    [Header("4个玩家的多棋子配置")]
    public PlayerConfig[] players = new PlayerConfig[4];

    [Header("当前回合状态")]
    public int currentTurn = 0;           // 0=玩家1, 1=玩家2, 2=玩家3, 3=玩家4
    public int currentDicePoints = 0;     // 当前摇出的骰子点数

    // 游戏状态枚举
    public enum GameState { WaitingForRoll, WaitingForSelectPiece, PieceMoving }
    [Header("当前游戏所处阶段")]
    public GameState currentState = GameState.WaitingForRoll;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        Debug.Log($"游戏开始！当前是【{players[currentTurn].playerName}】的回合，请摇骰子。");
    }

    // 1. 骰子停稳后由骰子脚本调用
    public void OnDiceFinished(int points)
    {
        currentDicePoints = points;
        currentState = GameState.WaitingForSelectPiece; // 进入【等待选择棋子】阶段
        Debug.Log($"【{players[currentTurn].playerName}】摇出了 {points} 点！请在场景中用鼠标点击你的一枚棋子来移动它。");
    }

    // 2. 当玩家成功用鼠标点击了属于自己的棋子后，由棋子点击脚本调用
    public void SelectPieceToMove(Transform selectedPiece)
    {
        // 安全检查：确保当前处于选择阶段
        if (currentState != GameState.WaitingForSelectPiece) return;

        // 验证被点击的棋子是否真的属于当前回合的玩家
        if (IsPieceOwnedByCurrentPlayer(selectedPiece))
        {
            currentState = GameState.PieceMoving; // 进入【棋子移动中】阶段
            SlidingPiece slidingScript = selectedPiece.GetComponent<SlidingPiece>();

            if (slidingScript != null)
            {
                // 2. 正式调用你截图中的方法，把当前骰子摇出的总步数（currentDicePoints）传进去
                slidingScript.StartSlidingRoutine(currentDicePoints);
            }
            else
            {
                Debug.LogError($"{selectedPiece.name} 身上找不到 SlidingPiece 脚本！");
                SwitchToNextTurn();
            }
        }
        else
        {
            Debug.LogWarning($"选错啦！这不是【{players[currentTurn].playerName}】的棋子，请重新选择！");
        }
    }

    // 检查棋子所有权的工具函数
    private bool IsPieceOwnedByCurrentPlayer(Transform piece)
    {
        foreach (Transform p in players[currentTurn].myPieces)
        {
            if (p == piece) return true;
        }
        return false;
    }

    // 3. 移动选中棋子的协程
    /*private System.Collections.IEnumerator MovePlayerPiece(Transform piece, int steps)
    {
        // 假设每步往前移动 1 米
        for (int i = 0; i < steps; i++)
        {
            piece.position += Vector3.forward * 1.0f; 
            yield return new WaitForSeconds(0.4f); 
        }

        // 4. 移动完毕，切换回合
        SwitchToNextTurn();
    }*/

    public void SwitchToNextTurn()
    {
        currentTurn = (currentTurn + 1) % 4;
        currentState = GameState.WaitingForRoll; // 重置回【等待摇骰子】阶段
        Debug.Log($"【回合切换】现在轮到【{players[currentTurn].playerName}】的回合了！");
    }
}
