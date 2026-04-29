using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using TMPro; // 必须添加这个，因为你的 UI 是 TextMeshPro

public class KeyManager : MonoBehaviour
{
    public int keysCollected = 0;
    public int totalKeys = 3;

    public TextMeshProUGUI keyText; // 类型改为 TextMeshProUGUI

    void Start()
    {
        UpdateKeyUI(); // 初始化显示
    }

    public void CollectKey()
    {
        keysCollected++;
        Debug.Log("钥匙搜集成功，当前数量: " + keysCollected);
        UpdateKeyUI(); // 关键：每次增加都要刷新 UI
    }

    private void UpdateKeyUI()
    {
        if (keyText != null)
        {
            // 使用插值字符串更简洁
            keyText.text = $"Key Number : {keysCollected}";
        }
    }

    public bool HasAllKeys()
    {
        return keysCollected >= totalKeys;
    }
}
