using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region --- Singleton ---
    public static GameManager Instance { get; private set; }
    public bool IsGameActive = false;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    #region --- Configuration ---

    [Header("Game Settings")]
    [SerializeField] private LayerMask bottleLayer;
    [SerializeField] private float selectMoveY = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip selectClip;
    [SerializeField] private AudioClip deselectClip;
    [SerializeField] private AudioClip winClip;

    #endregion

    #region --- State ---

    private BottleController _selectedBottle;
    private bool _isBusy;

    #endregion

    #region --- Game Loop & Input ---

    private void Update()
    {
        if (_isBusy) return;

        // Xử lý Input
        if (Input.GetMouseButtonDown(0))
        {
            HandleSelectionInput();
        }
    }
    //Hàm xử lý đầu vào lựa chọn
    private void HandleSelectionInput()
    {
        Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero, 100f, bottleLayer);

        if (hit.collider != null && hit.collider.TryGetComponent(out BottleController clickedBottle))
        {
            ProcessBottleClick(clickedBottle);
        }
        else
        {
            DeselectCurrentBottle();
        }
    }

    #endregion

    #region --- Core Interaction Logic ---

    private void ProcessBottleClick(BottleController clickedBottle)
    {
        // 1. Nếu chưa chọn chai nào -> Chọn chai này (Nguồn)
        if (_selectedBottle == null)
        {
            // Chỉ chọn nếu chai có nước và chưa hoàn thành
            if (!clickedBottle.IsEmpty && !clickedBottle.IsCompleted())
            {
                SelectBottle(clickedBottle);
            }
            return;
        }

        if (_selectedBottle == clickedBottle)
        {
            DeselectCurrentBottle();
        }
        else
        {
            PerformPourAction(_selectedBottle, clickedBottle);
        }
    }
    //Hàm chọn chai
    private void SelectBottle(BottleController bottle)
    {
        _selectedBottle = bottle;
        bottle.transform.DOKill(); 
        bottle.transform.DOMoveY(bottle.transform.position.y + selectMoveY, 0.2f);
        PlaySound(selectClip);
    }
    //Hàm huỷ chọn chai đang chọn
    private void DeselectCurrentBottle()
    {
        if (_selectedBottle == null) return;
        _selectedBottle.transform.DOKill();
        _selectedBottle.transform.DOMoveY(_selectedBottle.transform.position.y - selectMoveY, 0.2f);
        _selectedBottle.transform.DOScale(1f, 0.2f);
        PlaySound(deselectClip);
        _selectedBottle = null;
    }

    #endregion

    #region --- Gameplay Actions ---
    //Kiểm tra hợp lệ
    private void PerformPourAction(BottleController source, BottleController target)
    {
        if (!source.CanPourInto(target))
        {
            Debug.Log("Invalid Move: Cannot pour.");
            DeselectCurrentBottle();
            return;
        }

        // 2. Tính lượng nước
        int sourceAmount = source.GetTopColorCount();
        int targetSpace = target.GetFreeSpace();
        int amountToPour = Mathf.Min(sourceAmount, targetSpace);

        if (amountToPour <= 0)
        {
            DeselectCurrentBottle();
            return;
        }
        _isBusy = true;

        source.transform.DOScale(1f, 0.1f);

        source.PourInto(target, amountToPour, OnPouringFinished);

        _selectedBottle = null;
    }

    private void OnPouringFinished()
    {
        _isBusy = false;
        CheckGameStatus();
    }

    #endregion

    #region --- Game State (Win/Loss) ---

    public void CheckGameStatus()
    {
        List<BottleController> bottles = LevelManager.Instance.AllBottles;


        if (CheckIfWon(bottles))
        {
            Debug.Log("VICTORY!");
            if (LevelManager.Instance) LevelManager.Instance.PlayWinSound();
            if (UIManager.Instance) UIManager.Instance.ShowWinPanel();
            IsGameActive = false;
            return;
        }
        if (!HasAnyMoveLeft(bottles))
        {
            Debug.Log("GAME OVER");
            if (LevelManager.Instance) LevelManager.Instance.PlayLoseSound();
            if (UIManager.Instance) UIManager.Instance.ShowLosePanel();
            IsGameActive = false;
            return;
        }

        Debug.Log("Vẫn còn nước đi, tiếp tục chơi...");
    }

    // Kiểm tra xem còn có nước nào để đi hay không
    private bool HasAnyMoveLeft(List<BottleController> bottles)
    {

        for (int i = 0; i < bottles.Count; i++)
        {

            if (bottles[i].IsEmpty || bottles[i].IsCompleted()) continue;
            for (int j = 0; j < bottles.Count; j++)
            {
                if (i == j) continue;
                if (CanPour(bottles[i], bottles[j]))
                {
                    Debug.LogWarning($"Có nước đi " +
                                     $"Từ chai {i} (Màu {bottles[i].GetTopColor()}) " +
                                     $"Sang chai {j} (Màu {bottles[j].GetTopColor()}) " +
                                     $"| Đích đầy chưa? {bottles[j].IsFull}");
                    return true;
                }
            }
        }

        
        return false;
    }

    // Hàm kiểm tra logic rót nước cơ bản
    private bool CanPour(BottleController source, BottleController target)
    {
        if (target.IsFull) return false;       
        if (target.IsEmpty) return true;    

        return source.GetTopColor() == target.GetTopColor();
    }

    private bool CheckIfWon(List<BottleController> bottles)
    {
        foreach (var bottle in bottles)
        {
            if (bottle.IsEmpty) continue;
            if (!bottle.IsCompleted()) return false;
        }
        return true;
    }

    // Hàm Reset dùng khi bấm nút Replay
    public void ResetGameState()
    {
        IsGameActive = true;
        if (UIManager.Instance) UIManager.Instance.HideAllPanels();
    }
    #endregion

    //Hàm chạy Sound

    private void PlaySound(AudioClip clip)
    {
        if (sfxSource && clip) sfxSource.PlayOneShot(clip);
    }

}