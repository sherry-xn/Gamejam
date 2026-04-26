using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public PlayerInputAction InputAction { get; private set; }
    public PlayerInputAction.PlayerActions PlayerActions { get; private set; }

    private void Awake()
    {
        Initialized();
    }

    private void OnEnable()
    {
        Initialized();
        InputAction.Enable();
    }

    private void OnDisable()
    {
        if (InputAction == null)
        {
            return;
        }

        InputAction.Disable();
    }

    private void Initialized()
    {
        if (InputAction != null)
        {
            return;
        }

        InputAction = new PlayerInputAction();
        PlayerActions = InputAction.Player;
    }
    
    // 开启玩家移动输入
    public void EnablePlayerMoveInput()
    {
        if (InputAction == null)
        {
            return;
        }
        PlayerActions.Move.Enable();
    }

    // 关闭玩家移动输入
    public void DisablePlayerMoveInput()
    {
        if (InputAction == null)
        {
            return;
        }
        PlayerActions.Move.Disable();
    }
}

