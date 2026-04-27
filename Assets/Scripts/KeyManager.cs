using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KeyManager : MonoBehaviour
{
    public int keysCollected = 0;
    public int totalKeys = 3;

    public void CollectKey()
    {
        keysCollected++;
        Debug.Log("收集到钥匙！当前数量：" + keysCollected);
    }
    public Text keyText;
    // 更新UI显示
    private void UpdateKeyUI()
    {
        if (keyText != null)
        {
            keyText.text = $"钥匙: {keysCollected}";
        }
    }

    public bool HasAllKeys()
    {
        return keysCollected >= totalKeys;
    }

}
