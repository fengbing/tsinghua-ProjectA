using UnityEngine;

/// <summary>
/// 在场景中查找系统对话框实现：优先 <see cref="SystemDialogController2"/>，否则 <see cref="SystemDialogController"/>。
/// </summary>
public static class SystemDialogLocator
{
    public static Component FindComponent()
    {
        var v2 = Object.FindFirstObjectByType<SystemDialogController2>();
        if (v2 != null)
            return v2;
        return Object.FindFirstObjectByType<SystemDialogController>();
    }

    public static ISystemDialogPresentation FindPresentation()
    {
        return FindComponent() as ISystemDialogPresentation;
    }
}
