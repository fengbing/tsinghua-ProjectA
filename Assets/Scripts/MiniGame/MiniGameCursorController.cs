using UnityEngine;
using UnityEngine.UI;

/// <summary>Hardware <see cref="Cursor.SetCursor"/> or overlay Image following the mouse.</summary>
public sealed class MiniGameCursorController : MonoBehaviour
{
    const int MinOverlayCanvasSortOrder = 5000;

    [SerializeField] Texture2D cursorTexture;
    [SerializeField] Vector2 hotspotPixels;
    [Tooltip("叠加加载时硬件光标易失效，默认开软件光标更稳。")]
    [SerializeField] bool forceSoftwareCursor = true;
    [SerializeField] int maxHardwareSize = 128;
    [Tooltip("Shown when using software cursor (assign same art as cursorTexture import as Sprite).")]
    [SerializeField] Sprite softwareCursorSprite;
    [SerializeField] Canvas overlayCanvas;
    [SerializeField] RectTransform cursorGraphic;
    [SerializeField] Vector2 softwarePixelOffset;

    bool _software;

    void Awake()
    {
        if (cursorTexture == null && softwareCursorSprite != null)
            cursorTexture = softwareCursorSprite.texture;
    }

    void OnEnable()
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.overrideSorting = true;
            if (overlayCanvas.sortingOrder < MinOverlayCanvasSortOrder)
                overlayCanvas.sortingOrder = MinOverlayCanvasSortOrder;
        }

        ApplyCursorMode();

        if (_software && cursorGraphic != null)
            cursorGraphic.SetAsLastSibling();
    }

    void OnDisable()
    {
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (cursorGraphic != null)
            cursorGraphic.gameObject.SetActive(false);
    }

    void ApplyCursorMode()
    {
        _software = forceSoftwareCursor
                    || cursorTexture == null
                    || IsCursorTextureTooLarge();

        if (_software)
        {
            Cursor.visible = false;
            if (cursorGraphic != null)
            {
                cursorGraphic.gameObject.SetActive(true);
                if (softwareCursorSprite != null)
                {
                    var img = cursorGraphic.GetComponent<Image>();
                    if (img != null)
                        img.sprite = softwareCursorSprite;
                }
            }
        }
        else
        {
            Cursor.SetCursor(cursorTexture, hotspotPixels, CursorMode.Auto);
            if (cursorGraphic != null)
                cursorGraphic.gameObject.SetActive(false);
        }
    }

    bool IsCursorTextureTooLarge()
    {
        return cursorTexture != null
               && (cursorTexture.width > maxHardwareSize || cursorTexture.height > maxHardwareSize);
    }

    void LateUpdate()
    {
        if (!_software || overlayCanvas == null || cursorGraphic == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayCanvas.transform as RectTransform,
            Input.mousePosition,
            overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera,
            out var local);
        cursorGraphic.anchoredPosition = local + softwarePixelOffset;
    }
}
