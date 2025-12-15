using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class BottleVisual : MonoBehaviour
{
    #region --- Inspector Configuration ---

    [Header("References")]
    [SerializeField] private SpriteRenderer liquidSprite;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private SortingGroup sortingGroup;

    [Header("Visual Config")]
    [SerializeField] private LiquidThemeSO theme;
    [SerializeField] private Vector3 pourOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Pour Points (Manual Setup)")]
    [Tooltip("Transform for the left rim of the bottle")]
    [SerializeField] private Transform leftRimPoint;
    [Tooltip("Transform for the right rim of the bottle")]
    [SerializeField] private Transform rightRimPoint;

    [Header("Water Surface Settings")]
    [SerializeField] private float waterBottomLocalY = -1.5f;
    [SerializeField] private float waterTopLocalY = 1.2f;

    [Header("Animation Settings")]
    [SerializeField] private AnimationCurve fillToTiltCurve;
    [Range(0f, 1f)][SerializeField] private float moveDuration = 0.4f;
    [Range(0f, 2f)][SerializeField] private float verticalGap = 0.5f;
    [Range(0f, 1f)][SerializeField] private float horizontalGap = 0.2f;
    [Range(0f, 1f)][SerializeField] private float arcHeight = 0.3f;

    #endregion

    #region --- Internal State & Constants ---

    private MaterialPropertyBlock _propBlock;
    private int _originalSortOrder;

    private static readonly int FillId = Shader.PropertyToID("_FillAmount");
    private static readonly int AngleId = Shader.PropertyToID("_Angle");
    private static readonly int[] ColorIds = {
        Shader.PropertyToID("_Color1"),
        Shader.PropertyToID("_Color2"),
        Shader.PropertyToID("_Color3"),
        Shader.PropertyToID("_Color4")
    };

    #endregion

    #region --- Unity Lifecycle ---

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();

        if (lineRenderer)
        {
            lineRenderer.enabled = false;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
        }

        if (sortingGroup)
            _originalSortOrder = sortingGroup.sortingOrder;

        if (liquidSprite == null)
            liquidSprite = GetComponent<SpriteRenderer>();

        EnsureCurveSetup();
    }

    private void Update()
    {
        UpdateLiquidAngle();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(pourOffset), 0.1f);

        if (leftRimPoint)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(leftRimPoint.position, 0.08f);
        }
        if (rightRimPoint)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(rightRimPoint.position, 0.08f);
        }
    }

    #endregion

    #region --- Public API: Animation ---

    public void PlayPourAnimation(
        Vector3 targetMouthPos,
        float currentFill01,
        float targetFill01,
        System.Action<float> onUpdateFill,
        System.Action onComplete)
    {
        float direction = (targetMouthPos.x > transform.position.x) ? -1f : 1f;
        float startAngle = fillToTiltCurve.Evaluate(currentFill01) * direction;
        float endAngle = fillToTiltCurve.Evaluate(targetFill01) * direction;

        float xOffset = (direction == -1f) ? -horizontalGap : horizontalGap;
        Vector3 anchorBase = targetMouthPos + new Vector3(xOffset, verticalGap, 0);

        Vector3 startAnchor = anchorBase;
        Vector3 endAnchor = anchorBase + new Vector3(direction * 0.1f, -0.05f, 0);
        Vector3 startBodyPos = CalculateRotatedPosition(startAnchor, startAngle);


        if (sortingGroup) sortingGroup.sortingOrder = 20;

        Sequence s = DOTween.Sequence();

        s.Append(transform.DOMove(startBodyPos, moveDuration).SetEase(Ease.OutQuad));
        s.Join(transform.DORotate(new Vector3(0, 0, startAngle), moveDuration).SetEase(Ease.OutQuad));


        float drainAmount = Mathf.Abs(currentFill01 - targetFill01);
        float pourTime = Mathf.Max(0.5f, drainAmount * 1.5f);

        s.Append(DOVirtual.Float(0f, 1f, pourTime, (t) =>
        {

            float currentFill = Mathf.Lerp(currentFill01, targetFill01, t);
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
            Vector3 currentAnchor = Vector3.Lerp(startAnchor, endAnchor, t);


            onUpdateFill?.Invoke(currentFill);
            UpdateFillProperty(currentFill);


            transform.rotation = Quaternion.Euler(0, 0, currentAngle);
            Vector3 basePos = CalculateRotatedPosition(currentAnchor, currentAngle);

            float arcY = Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = basePos + new Vector3(0, arcY, 0);


            if (lineRenderer.enabled)
            {
                Vector3 startPos = GetWaterEmissionPoint(targetMouthPos);
                startPos.z = -1f;
                lineRenderer.SetPosition(0, startPos);
            }

        }).SetEase(Ease.Linear));


        s.AppendCallback(() => DisableLineRenderer());
        s.OnComplete(() => onComplete?.Invoke());
    }

    public void EndPour(Vector3 originalPos, System.Action onDone)
    {
        Sequence s = DOTween.Sequence();
        s.Append(transform.DOMove(originalPos, moveDuration).SetEase(Ease.OutQuad));
        s.Join(transform.DORotate(Vector3.zero, moveDuration).SetEase(Ease.OutQuad));
        s.OnComplete(() =>
        {
            if (sortingGroup) sortingGroup.sortingOrder = _originalSortOrder;
            onDone?.Invoke();
        });
    }

    #endregion

    #region --- Public API: Visual Updates ---

    public void UpdateLine(Vector3 targetWaterSurfacePos, Color color)
    {
        if (!lineRenderer.enabled)
        {
            lineRenderer.enabled = true;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.widthMultiplier = 0.15f;
            lineRenderer.sortingOrder = 25;
        }

        Vector3 startPos = GetWaterEmissionPoint(targetWaterSurfacePos);
        startPos.z = -1f;

        Vector3 endPos = targetWaterSurfacePos;
        endPos.z = -1f;

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
    }


    public void UpdateVisualsInstant(LiquidType[] liquids, int maxCap, LiquidThemeSO themeOverride = null)
    {
        ApplyColors(liquids, themeOverride);
        float fill = (float)liquids.Length / maxCap;
        UpdateFillProperty(fill);
    }


    public void UpdateColorsOnly(LiquidType[] liquids, int maxCap, LiquidThemeSO themeOverride = null)
    {
        ApplyColors(liquids, themeOverride);

    }


    public void UpdateFillOnly(float fillAmount)
    {
        UpdateFillProperty(fillAmount);
    }

    public Vector3 GetPourWorldPos() => transform.TransformPoint(pourOffset);

    public Vector3 GetWaterSurfacePosition(float fillAmount)
    {
        float currentY = Mathf.Lerp(waterBottomLocalY, waterTopLocalY, fillAmount);
        return transform.TransformPoint(new Vector3(0, currentY, 0));
    }

    #endregion

    #region --- Internal Logic & Helpers ---

    private void UpdateLiquidAngle()
    {
        if (liquidSprite == null || !liquidSprite.gameObject.activeSelf) return;

        liquidSprite.GetPropertyBlock(_propBlock);

        float bottleAngle = transform.eulerAngles.z;
        if (bottleAngle > 180) bottleAngle -= 360;

        _propBlock.SetFloat(AngleId, bottleAngle * Mathf.Deg2Rad);
        liquidSprite.SetPropertyBlock(_propBlock);
    }

    private void UpdateFillProperty(float fillAmount)
    {
        if (liquidSprite == null) return;
        liquidSprite.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(FillId, fillAmount);
        liquidSprite.SetPropertyBlock(_propBlock);
    }

    private void ApplyColors(LiquidType[] liquids, LiquidThemeSO themeOverride)
    {
        if (liquidSprite == null) return;

        liquidSprite.gameObject.SetActive(liquids.Length > 0);
        liquidSprite.GetPropertyBlock(_propBlock);

        var currentTheme = themeOverride != null ? themeOverride : theme;

        for (int i = 0; i < ColorIds.Length; i++)
        {
            Color c = (i < liquids.Length && currentTheme != null)
                ? currentTheme.GetColor(liquids[i])
                : Color.clear;
            _propBlock.SetColor(ColorIds[i], c);
        }

        liquidSprite.SetPropertyBlock(_propBlock);
    }

    private Vector3 GetWaterEmissionPoint(Vector3 targetPos)
    {
        bool isPouringLeft = targetPos.x < transform.position.x;

        if (isPouringLeft && leftRimPoint != null)
            return leftRimPoint.position;

        if (!isPouringLeft && rightRimPoint != null)
            return rightRimPoint.position;

        return transform.position; 
    }

    private void DisableLineRenderer()
    {
        if (lineRenderer) lineRenderer.enabled = false;
    }

    private Vector3 CalculateRotatedPosition(Vector3 anchorPos, float angle)
    {
        Vector3 offset = pourOffset;
        offset.x *= transform.lossyScale.x;
        offset.y *= transform.lossyScale.y;

        Vector3 rotatedOffset = Quaternion.Euler(0, 0, angle) * offset;
        return anchorPos - rotatedOffset;
    }

    private void EnsureCurveSetup()
    {
        if (fillToTiltCurve == null || fillToTiltCurve.length == 0)
        {
            fillToTiltCurve = new AnimationCurve();
            fillToTiltCurve.AddKey(0.00f, 100f);
            fillToTiltCurve.AddKey(0.25f, 75f);
            fillToTiltCurve.AddKey(0.50f, 50f);
            fillToTiltCurve.AddKey(0.75f, 25f);
            fillToTiltCurve.AddKey(1.00f, 0f);

            for (int i = 0; i < fillToTiltCurve.length; i++)
                fillToTiltCurve.SmoothTangents(i, 0);
        }
    }

    #endregion

    public Color GetThemeColor(LiquidType type)
    {
        if (theme == null) return Color.blue;
        return theme.GetColor(type);
    }
}