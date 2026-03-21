## 1. 距离数据契约与占位

- [x] 1.1 定义 `IDistanceHudSource`（或事件委托），提供 `float` 米制距离；在 `Assets/Scripts` 中新增独立小文件，避免与 UI 耦合。
- [x] 1.2 实现一个 **Development 占位**（如 `ConstantDistanceHudSource` 或键盘调距离），便于无玩法测距时验收 HUD。

## 2. 底部条带布局与中心向两侧衰减

- [x] 2.1 在主 HUD Canvas（或新建 `DistanceHud` 预制体）下添加底部锚定条带：Screen Space，底部对齐，条带高度可配置（Inspector）。
- [x] 2.2 使用 Image + 横向渐变纹理 **或** UI Shader，实现水平中心最亮、向左右衰减的遮罩/颜色权重。

## 3. 阈值配色与呼吸动画

- [x] 3.1 实现距离 → 基础色的连续映射：默认阈值 300 / 150 / 50 m，分段白→黄→红，使用 `Color.Lerp`（或 HSV 插值）避免硬切。
- [x] 3.2 在 View 脚本中用 `Mathf.Sin(Time.time * omega)` 调制条带整体 alpha 或叠加亮度；周期与幅度 Inspector 可配，默认约 2–4 s 周期。

## 4. 半透明渐变与模糊

- [x] 4.1 叠层实现纵向/横向半透明渐变，保证背后场景仍可见。
- [x] 4.2 按 `design.md`：在目标管线下尝试局部 UI 模糊；若不可行，用多层柔边渐变作为 **fallback**，并在组件上提供「启用模糊」开关。

## 5. 接入与验收

- [x] 5.1 将真实距离源（如飞机前向射线、最近障碍、关卡管理器）接到 `IDistanceHudSource`；若暂无，则默认使用占位并文档化替换方式。
- [x] 5.2 在 Editor Play 模式下目视验收：`openspec/changes/bottom-distance-hud/specs/distance-hud/spec.md` 中各条 Scenario；记录已知管线限制（如无模糊 fallback）。

**验收说明（5.2）**：请在 Unity 中 **Play** 后确认条带、配色、呼吸与中心衰减；`DistanceHudView` 的 XML 备注已说明 URP 下无真 GPU 模糊，由 `useFrostedLayerFallback` 多层柔边代替。菜单 **GameObject → UI → Distance HUD Strip** 可在 Canvas 下创建整条 HUD。
