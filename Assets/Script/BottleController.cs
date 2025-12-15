using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

[SelectionBase]
public class BottleController : MonoBehaviour
{
    #region --- Configuration ---

    [Header("Settings")]
    [SerializeField] private int maxCapacity = 4;

    [Header("Visual References")]
    [SerializeField] private BottleVisual bottleVisual;
    [SerializeField] private GameObject stopperObject;
    [SerializeField] private ParticleSystem fireworksVFX;

    [Header("Animation Config")]
    [SerializeField] private float stopperDropHeight = 1.5f;
    [SerializeField] private float stopperAnimDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pourStartClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip pouringLoopClip;

    #endregion

    #region --- State Data ---

    // Dùng Stack để quản lý LIFO (Vào sau ra trước)
    private Stack<LiquidType> _liquids = new Stack<LiquidType>();
    private Vector3 _originalPosition;
    private Vector3 _stopperFinalPos;

    public int CurrentAmount => _liquids.Count;
    public int MaxCapacity => maxCapacity;
    public bool IsFull => _liquids.Count >= maxCapacity;
    public bool IsEmpty => _liquids.Count == 0;

    public LiquidType TopLiquid => IsEmpty ? LiquidType.None : _liquids.Peek();

    #endregion

    #region --- Unity Lifecycle ---

    private void Awake()
    {
        if (bottleVisual == null) bottleVisual = GetComponent<BottleVisual>();

        _originalPosition = transform.position;

        if (stopperObject != null)
        {
            _stopperFinalPos = stopperObject.transform.localPosition;
            stopperObject.SetActive(false);
        }
    }

    #endregion

    #region --- Initialization ---

    public void Init(LiquidType[] types, LiquidThemeSO theme)
    {
        _originalPosition = transform.position;
        _liquids.Clear();

        foreach (var t in types)
        {
            if (t != LiquidType.None) _liquids.Push(t);
        }

        UpdateVisualsInstant(theme);

        if (stopperObject) stopperObject.SetActive(false);
    }

    #endregion

    #region --- Core Gameplay Logic (Validation) ---

    // Kiểm tra xem CÓ THỂ rót sang chai đích hay không.
    public bool CanPourInto(BottleController targetBottle)
    {
        if (this.IsEmpty) return false;                 
        if (targetBottle.IsFull) return false;         
        if (this == targetBottle) return false;         

        if (!targetBottle.IsEmpty)
        {
            if (this.TopLiquid != targetBottle.TopLiquid) return false;
        }        
        return targetBottle.GetFreeSpace() > 0;
    }
    public LiquidType GetTopColor()
    {
        if (_liquids.Count == 0) return LiquidType.None;
        return _liquids.Peek();
    }



    // Đếm số lượng đốt cùng màu liên tiếp ở trên cùng.
    public int GetTopColorCount()
    {
        if (IsEmpty) return 0;

        LiquidType topType = _liquids.Peek();
        int count = 0;
        foreach (var liquid in _liquids)
        {
            if (liquid == topType) count++;
            else break;
        }
        return count;
    }

    public int GetFreeSpace() => maxCapacity - _liquids.Count;


    // Kiểm tra chai đã hoàn thành
    public bool IsCompleted()
    {
        if (!IsFull) return false;

        LiquidType firstType = _liquids.Peek();
        return _liquids.All(l => l == firstType);
    }

    #endregion

    #region --- Action: Pouring ---


    // Hàm thực hiện hành động rót nước sang chai đích.
    public void PourInto(BottleController target, int amountToPour, System.Action onComplete)
    {
        float currentSourceFill = (float)CurrentAmount / maxCapacity;
        float targetSourceFill = (float)(CurrentAmount - amountToPour) / maxCapacity;

        int targetStartCount = target.CurrentAmount;
        int targetEndCount = targetStartCount + amountToPour;

        float targetStartFill = (float)targetStartCount / maxCapacity;
        float targetEndFill = (float)targetEndCount / maxCapacity;

        LiquidType pouringType = TopLiquid;
        Color streamColor = bottleVisual.GetThemeColor(pouringType);
        LiquidType[] targetFutureColors = CreateFutureColorArray(target, pouringType, amountToPour);

        target.bottleVisual.UpdateColorsOnly(targetFutureColors, maxCapacity);
        target.bottleVisual.UpdateFillOnly(targetStartFill);


        PlayLoopSound();

        bottleVisual.PlayPourAnimation(
            target.bottleVisual.GetPourWorldPos(),
            currentSourceFill,
            targetSourceFill,
            onUpdateFill: (currentFillOfSource) =>
            {
                // Tính % tiến độ dựa trên việc giảm nước ở chai nguồn
                float totalPourDiff = currentSourceFill - targetSourceFill;
                float progress = 0f;
                if (totalPourDiff > 0)
                    progress = 1f - ((currentFillOfSource - targetSourceFill) / totalPourDiff);

                progress = Mathf.Clamp01(progress);

                // Update mức nước chai đích tăng dần
                float currentTargetFill = Mathf.Lerp(targetStartFill, targetEndFill, progress);
                target.bottleVisual.UpdateFillOnly(currentTargetFill);

                if (progress > 0.05f && progress < 0.95f)
                {
                    Color c = Color.blue;                                  
                    Vector3 targetSurface = target.bottleVisual.GetWaterSurfacePosition(currentTargetFill);                                     
                    bottleVisual.UpdateLine(targetSurface, streamColor);
                }
                else
                {
                    StopSound();
                }
            },
            onComplete: () =>
            {
                PerformDataTransfer(target, amountToPour);

                StopSound();

                UpdateVisualsInstant();
                target.UpdateVisualsInstant();

                if (target.IsCompleted())
                {
                    target.PlayCompleteEffect();
                    if (LevelManager.Instance) LevelManager.Instance.PlayBottleCompleteSound();
                }

                bottleVisual.EndPour(_originalPosition, onComplete);
            }
        );
    }
    //Hàm chuyển đổi dữ liệu màu
    private void PerformDataTransfer(BottleController target, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (_liquids.Count > 0)
            {
                target.AddLiquid(_liquids.Pop());
            }
        }
    }

    // Tạo mảng màu tương lai để setup visual
    private LiquidType[] CreateFutureColorArray(BottleController target, LiquidType incomingType, int amount)
    {
        var currentLiquids = target.GetLiquidsList();
        for (int i = 0; i < amount; i++)
        {
            currentLiquids.Add(incomingType);
        }

        return currentLiquids.ToArray();
    }

    #endregion

    #region --- Visual & Audio Helpers ---
    //Hàm cập nhật ảnh tức thì
    private void UpdateVisualsInstant(LiquidThemeSO themeOverride = null)
    {
        if (bottleVisual)
        {            
            bottleVisual.UpdateVisualsInstant(_liquids.Reverse().ToArray(), maxCapacity, themeOverride);
        }
    }

    public void AddLiquid(LiquidType type)
    {
        if (!IsFull) _liquids.Push(type);
    }

    // Trả về List từ Đáy lên Đỉnh
    public List<LiquidType> GetLiquidsList()
    {
        return _liquids.Reverse().ToList();
    }
    //Hàm chạy VFX và Sound
    public void PlayCompleteEffect()
    {
        if (!stopperObject) return;

        stopperObject.SetActive(true);
        stopperObject.transform.localPosition = _stopperFinalPos + new Vector3(0, stopperDropHeight, 0);

        PlayOneShot(closeClip);

        stopperObject.transform.DOLocalMove(_stopperFinalPos, stopperAnimDuration)
            .SetEase(Ease.OutBounce)
            .OnComplete(() => {
                if (fireworksVFX) fireworksVFX.Play();
            });
    }

    private void PlayLoopSound()
    {
        if (audioSource && pouringLoopClip)
        {
            audioSource.clip = pouringLoopClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void StopSound()
    {
        if (audioSource && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource && clip)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion
}