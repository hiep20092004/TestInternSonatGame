using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Components")]
    [SerializeField] private Text levelText;

    [Header("Panels")]
    [SerializeField] private CanvasGroup winPanel;
    [SerializeField] private CanvasGroup losePanel;

    [Header("Buttons")]
    [SerializeField] private Button winNextLevelBtn;
    [SerializeField] private Button winRestartBtn;
    [SerializeField] private Button loseRestartBtn;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Setup Buttons
        winNextLevelBtn.onClick.AddListener(OnNextLevelClicked);
        winRestartBtn.onClick.AddListener(OnRestartClicked);
        loseRestartBtn.onClick.AddListener(OnRestartClicked);

        // Ẩn panel lúc đầu
        HideAllPanels();
    }

    public void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = "LEVEL " + level;
        }
    }

    // --- BUTTON ACTIONS ---

    private void OnNextLevelClicked()
    {
        HideAllPanels();
        // Gọi hàm tăng level bên Manager
        LevelManager.Instance.NextLevel();
        GameManager.Instance.ResetGameState();
    }

    private void OnRestartClicked()
    {
        HideAllPanels();
        // Gọi hàm sinh lại level hiện tại
        LevelManager.Instance.GenerateLevel();
        GameManager.Instance.ResetGameState();
    }

    public void ShowWinPanel() { ShowPanel(winPanel); }
    public void ShowLosePanel() { ShowPanel(losePanel); }

    private void ShowPanel(CanvasGroup panel)
    {
        panel.gameObject.SetActive(true);
        panel.alpha = 0;
        panel.transform.localScale = Vector3.zero;
        panel.DOFade(1f, 0.5f);
        panel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
        panel.blocksRaycasts = true;
    }

    public void HideAllPanels()
    {
        if (winPanel) { winPanel.gameObject.SetActive(false); winPanel.blocksRaycasts = false; }
        if (losePanel) { losePanel.gameObject.SetActive(false); losePanel.blocksRaycasts = false; }
    }
}