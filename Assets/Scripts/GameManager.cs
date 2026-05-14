using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages game state, including opening text, escape victory, death, and scene transitions.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private GameEndMenuUI victoryMenuPrefab;
    [SerializeField] private GameEndMenuUI failureMenuPrefab;

    [Header("Black Screen Text")]
    [SerializeField] private bool showOpeningText = true;
    [SerializeField, TextArea(5, 12)]
    private string openingText =
        "黑鱼在模糊的红水中游弋，被细小的血管挟着，一股一股往前游。\n" +
        "直到那怪物的目光挪走，眼前才黑下来。你睁开眼，眼前犹残存着血液流动的余象.\n"+
        "今晚异样的安静，没有血，没有砰砰砰的脚步，也没有尖叫。从妈妈将自己送到这里以来，已经很久没有这样安静的夜。\n"+
        "你摸了摸怀里藏着的监控平板。\n"+
        "学校里充满了怪物，而自己，一定要逃出去。\n";

    [SerializeField, TextArea(5, 12)]
    private string victoryText =
        "你见到了久违的天。\n"
        + "蓝蓝的，仿佛要流淌下来的天。\n"
        + "其中有一轮极为耀眼的圆。\n"
        + "那是什么？\n"
        + "眯着眼看了又看。\n"
        + "直到金黄色的光线洒落在身上，将皮肤烫了一下，才后知后觉地\n"
        + "想起那是阳光。\n"
        + "是暖阳。\n"
        + "接下来，要不去云南看看洱海吧。\n"
        ;

    [SerializeField, TextArea(5, 12)]
    private string failureText =
        "手电筒似乎损坏了，光亮褪去，黑暗涌上来，你像是被汹涌海浪冲刷着的一小块礁石，终于不堪重负碎裂开来，转眼被海浪席卷而去。\n" 
        +"怪物抓住了你的脚，你只看见黑暗中森亮的白牙，和长长的钢铁做成的臂膀。\n"
        +"那东西狠狠落在你身上，你痛地蜷缩起来。恍惚间似乎想起了什么，你呢喃。\n"
        +"妈妈……\n"
        +"妈妈在哪呢？";

    [SerializeField] private string continueHint = "点击屏幕或按任意键继续";

    [SerializeField, Tooltip("Delay before restarting after game over if no end menu can be shown.")]
    private float gameOverDelay = 3f;
    [SerializeField, Tooltip("Delay before returning to the main menu after escape if no end menu can be shown.")]
    private float escapeDelay = 5f;

    private bool isGameOver = false;
    private bool isEscaped = false;
    private GameEndMenuUI activeEndMenu;

    public bool IsGameOver => isGameOver;
    public bool IsEscaped => isEscaped;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (showOpeningText)
        {
            ShowOpeningText();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnPlayerDeath()
    {
        if (isGameOver || isEscaped)
        {
            return;
        }

        isGameOver = true;

        AudioManager.Instance.StopBGM();
        AudioManager.Instance.StopAmbient();
        AudioManager.Instance.Play(SFX.GameOver);

        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.Input.DisablePlayerMoveInput();
        }

        ShowEndingText(failureText, ShowFailureMenuOrRestart);
    }

    public void OnPlayerEscape()
    {
        if (isGameOver || isEscaped)
        {
            return;
        }

        isEscaped = true;

        AudioManager.Instance.StopBGM();
        AudioManager.Instance.StopAmbient();
        AudioManager.Instance.PlayBGM(BGM.EscapeSuccess);

        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.Input.DisablePlayerMoveInput();
        }

        ShowEndingText(victoryText, ShowVictoryMenuOrReturn);
    }

    private void ShowOpeningText()
    {
        Time.timeScale = 0f;

        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.Input.DisablePlayerMoveInput();
        }

        BlackScreenTextUI.Show(openingText, continueHint, () =>
        {
            Time.timeScale = 1f;

            var currentPlayer = FindObjectOfType<PlayerController>();
            if (currentPlayer != null && !isGameOver && !isEscaped)
            {
                currentPlayer.Input.EnablePlayerMoveInput();
            }
        });
    }

    private void ShowEndingText(string text, Action onContinue)
    {
        Time.timeScale = 0f;
        BlackScreenTextUI.Show(text, continueHint, onContinue);
    }

    private void ShowFailureMenuOrRestart()
    {
        if (!TryShowEndMenu(
                failureMenuPrefab,
                "Assets/Prefabs/UI/FailureMenu.prefab",
                () => SceneManager.LoadScene(gameSceneName),
                () => SceneManager.LoadScene(mainMenuSceneName)))
        {
            Time.timeScale = 1f;
            StartCoroutine(RestartAfterDelay());
        }
    }

    private void ShowVictoryMenuOrReturn()
    {
        if (!TryShowEndMenu(
                victoryMenuPrefab,
                "Assets/Prefabs/UI/VictoryMenu.prefab",
                () => SceneManager.LoadScene(gameSceneName),
                () => SceneManager.LoadScene(mainMenuSceneName)))
        {
            Time.timeScale = 1f;
            StartCoroutine(ReturnToMenuAfterDelay());
        }
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(gameOverDelay);
        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator ReturnToMenuAfterDelay()
    {
        yield return new WaitForSeconds(escapeDelay);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private bool TryShowEndMenu(
        GameEndMenuUI menuPrefab,
        string prefabPath,
        UnityEngine.Events.UnityAction onRetry,
        UnityEngine.Events.UnityAction onMainMenu)
    {
        if (activeEndMenu != null)
        {
            return true;
        }

        if (menuPrefab == null)
        {
#if UNITY_EDITOR
            menuPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameEndMenuUI>(prefabPath);
#endif
        }

        if (menuPrefab != null)
        {
            activeEndMenu = Instantiate(menuPrefab.gameObject).GetComponent<GameEndMenuUI>();
        }
        else
        {
            var menuObject = new GameObject(prefabPath.Contains("Victory") ? "[VictoryMenu]" : "[FailureMenu]");
            activeEndMenu = menuObject.AddComponent<GameEndMenuUI>();
        }

        if (activeEndMenu == null)
        {
            return false;
        }

        activeEndMenu.Configure(onRetry, onMainMenu);
        activeEndMenu.Show();
        return true;
    }
}
