using UnityEngine;
using TMPro;

/// <summary>
/// 显示 Plane 与目标阳台之间的距离（米），保留一位小数。
/// 字段暴露给 Inspector，由用户自行拖入引用。
/// </summary>
public class DistanceDisplay : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] TextMeshProUGUI distanceText;
    [SerializeField] Transform planeTransform;
    [SerializeField] Transform targetBalconyTransform;

    [Header("显示设置")]
    [Tooltip("超过此距离时隐藏距离文字（米）")]
    [SerializeField] float maxDisplayDistance = 100f;
    [Tooltip("接近阈值，距离小于此值时高亮显示（米）")]
    [SerializeField] float closeThreshold = 5f;
    [Tooltip("延迟几秒后开始显示")]
    [SerializeField] float showDelay = 3f;

    private Color _defaultColor;
    private Color _closeColor;
    private float _timer;

    void Start()
    {
        if (distanceText != null)
        {
            _defaultColor = distanceText.color;
            _closeColor = Color.yellow;
            distanceText.gameObject.SetActive(false);
        }
        _timer = 0f;
    }

    void Update()
    {
        if (distanceText == null || planeTransform == null || targetBalconyTransform == null)
            return;

        _timer += Time.deltaTime;
        if (_timer < showDelay)
            return;

        distanceText.gameObject.SetActive(true);

        float dist = Vector3.Distance(planeTransform.position, targetBalconyTransform.position);
        float rounded = Mathf.Round(dist * 10f) / 10f;
        distanceText.text = $"{rounded:F1}m";

        if (dist > maxDisplayDistance)
        {
            distanceText.gameObject.SetActive(false);
        }
        else
        {
            distanceText.color = dist <= closeThreshold ? _closeColor : _defaultColor;
        }
    }
}
