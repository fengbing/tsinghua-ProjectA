/// <summary>
/// 由 PlaneController 写入强度，由 <see cref="CameraSpeedSideBlur"/>（内置管线）读取。
/// </summary>
public static class SpeedSideBlurGlobals
{
    /// <summary>0~1，与蓄力进度对齐；为 0 时不跑模糊 Pass。</summary>
    public static float Intensity { get; set; }
}
