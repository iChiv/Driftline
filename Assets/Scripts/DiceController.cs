using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
public class DiceController : MonoBehaviour
{
    private Rigidbody rb;
    private bool isRolling = false;
    private int points = 0;
    void Start()
    {
        // 获取骰子身上的刚体组件
        rb = GetComponent<Rigidbody>();
    }

    async void Update()
    {
        // 当玩家按下“空格键”，且骰子当前没有在滚动时
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isRolling && GameManager.Instance.currentState == GameManager.GameState.WaitingForRoll)
        {
            RollDice();
        }

        // 检测骰子是否已经停止滚动
        if (isRolling && rb.velocity.sqrMagnitude < 0.001f && rb.angularVelocity.sqrMagnitude < 0.001f)
        {
            isRolling = false;
            await Task.Delay(100); // 确保骰子不动了而非正好处于这个区间
            if (rb.velocity.sqrMagnitude < 0.001f && rb.angularVelocity.sqrMagnitude < 0.001f)
            {
                CheckDiceSide(); // 骰子停稳后，计算点数
                if (points != 0)
                {
                    GameManager.Instance.OnDiceFinished(points);
                }
            }
            else
            {
                isRolling = true;
            }
        }
    }

    // 摇骰子：给它一个向上的力和随机的旋转力
    async void RollDice()
    {
        // 1. 给骰子一个随机的初始旋转角度，防止每次落下的轨迹一样
        transform.rotation = Random.rotation;

        // 2. 给它一个向上的弹跳力
        rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);

        // 3. 给它一个随机的扭转力（让它在空中翻滚）
        Vector3 randomTorque = new Vector3(Random.Range(-8, 8), Random.Range(-8, 8), Random.Range(-8, 8));
        rb.AddTorque(randomTorque, ForceMode.Impulse);
        await Task.Delay(100);
        isRolling = true;
    }

    // 判断哪一面朝上
    void CheckDiceSide()
    {
        // 通过对比骰子的各个局部方向与世界的“正上方(Vector3.up)”来判断点数
        // 这里的点数对应关系取决于你的骰子贴图/模型方向
        if (Vector3.Dot(transform.up, Vector3.up) > 0.9f)
        {
            Debug.Log("摇出了：2 点");
            points = 2;
        }
        else if (Vector3.Dot(-transform.up, Vector3.up) > 0.9f)
        {
            Debug.Log("摇出了：5 点");
            points = 5;
        }
        else if (Vector3.Dot(transform.right, Vector3.up) > 0.9f)
        {
            Debug.Log("摇出了：4 点");
            points = 4;
        }
        else if (Vector3.Dot(-transform.right, Vector3.up) > 0.9f)
        {
            Debug.Log("摇出了：3 点");
            points = 3;
        }
        else if (Vector3.Dot(transform.forward, Vector3.up) > 0.9f)
        {
            Debug.Log("摇出了：1 点");
            points = 1;
        }
        else if (Vector3.Dot(-transform.forward, Vector3.up) > 0.9f)
        {
            Debug.Log("摇出了：6 点");
            points = 6;
        }
        else
        {
            Debug.Log("骰子好像立起来了或斜着，没停稳！请重新摇骰子");
            points = 0;
        }
    }
}