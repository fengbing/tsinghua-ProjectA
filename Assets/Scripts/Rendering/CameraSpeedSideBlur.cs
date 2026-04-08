using UnityEngine;

/// <summary>
/// 内置渲染管线：在相机上用 OnRenderImage 做两侧模糊，强度来自 <see cref="SpeedSideBlurGlobals"/>（由 PlaneController 写入）。
/// 挂到主相机（与 FollowCamera 同一相机）即可；勿与 URP 同时使用同一效果。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraSpeedSideBlur : MonoBehaviour
{
    const string DefaultShaderName = "Hidden/Drone/SpeedSideBlur";

    [Tooltip("留空则使用 Hidden/Drone/SpeedSideBlur")]
    [SerializeField] Shader blurShader;

    [Tooltip("越大中心清晰区越宽")]
    [Range(0.1f, 0.55f)]
    [SerializeField] float clearCenter = 0.38f;

    [Tooltip("两侧最大模糊采样半径（像素量级）")]
    [Range(2f, 64f)]
    [SerializeField] float maxBlurPixels = 18f;

    static readonly int BlurIntensityId = Shader.PropertyToID("_BlurIntensity");
    static readonly int ClearCenterId = Shader.PropertyToID("_ClearCenter");
    static readonly int MaxBlurPxId = Shader.PropertyToID("_MaxBlurPx");

    Material _material;

    void OnEnable()
    {
        if (blurShader == null)
            blurShader = Shader.Find(DefaultShaderName);
        if (blurShader != null && !blurShader.isSupported)
            blurShader = null;
        if (blurShader == null)
            return;
        _material = new Material(blurShader);
    }

    void OnDisable()
    {
        if (_material != null)
        {
            Destroy(_material);
            _material = null;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_material == null || SpeedSideBlurGlobals.Intensity < 0.0005f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        _material.SetFloat(BlurIntensityId, Mathf.Clamp01(SpeedSideBlurGlobals.Intensity));
        _material.SetFloat(ClearCenterId, clearCenter);
        _material.SetFloat(MaxBlurPxId, maxBlurPixels);
        Graphics.Blit(source, destination, _material);
    }
}
