using UnityEngine;

/// <summary>
/// 大门交互组件。玩家集齐钥匙后按交互键解锁并逃脱。
/// 挂载在大门 GameObject 上，需配合 InteractableItem 使用。
/// </summary>
public class MainDoor : MonoBehaviour
{
    [SerializeField] private SpriteRenderer doorSprite;
    [SerializeField, Range(0f, 1f)] private float unlockedAlpha = 0.5f;
    [SerializeField, Min(1)] private int requiredKeys = 3;
    [SerializeField] private string missingKeysMessage = "钥匙不够，无法打开大门";

    private bool isUnlocked = false;

    /// <summary>
    /// 由 InteractableItem 的 onInteracted 事件调用。
    /// </summary>
    public void TryUnlock(PlayerController player)
    {
        if (isUnlocked) return;

        if (!HasEnoughKeys(player))
        {
            ScreenHintPanel.Show(missingKeysMessage);
            return;
        }

        isUnlocked = true;

        // 播放解锁音效
        AudioManager.Instance.Play(SFX.MainDoorUnlock);

        // 视觉反馈：门变半透明
        if (doorSprite != null)
        {
            Color c = doorSprite.color;
            c.a = unlockedAlpha;
            doorSprite.color = c;
        }

        // 触发逃脱
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerEscape();
    }

    private bool HasEnoughKeys(PlayerController player)
    {
        var keyManager = FindObjectOfType<KeyManager>();
        int targetKeyCount = keyManager != null ? keyManager.totalKeys : requiredKeys;

        return (player != null && player.CurrentKey >= targetKeyCount)
            || (keyManager != null && keyManager.HasAllKeys());
    }
}
