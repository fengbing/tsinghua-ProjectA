# 飞机躲避障碍物 - 场景搭建说明

按 OpenSpec 从「项目与场景准备」到「UI 与反馈」的步骤，在 Unity 中完成以下搭建后即可运行游戏。

---

## 1. 场景与空物体

- 新建场景并保存为 `Assets/Scenes/PlaneGame.unity`（或沿用 SampleScene）。
- 在 Hierarchy 中创建空物体，命名为 **LevelRoot**（用于挂地图生成器并作为障碍物父节点）。
- 再创建一个空物体，命名为 **PlaneStart**，放在希望飞机出生的位置（例如 `(0, 0, 0)`），朝向 Z 轴正方向。

---

## 2. 摄像机与灯光

- 保留或创建 **Main Camera**。
- 给 Main Camera 挂上 **FollowCamera** 脚本，运行后把 **Target** 拖成飞机（见下）。
- 添加 **Directional Light**（Directional Light），照亮场景。

---

## 3. 飞机预制体

- 在场景中创建一个 **Cube**（或多个 Cube/Cylinder 拼成简易飞机外形），重命名为 **Plane**。
- 在 Plane 上添加组件：
  - **Rigidbody**：Use Gravity 勾选，Mass 约 1，Drag 约 0.5，Angular Drag 约 2。
  - **PlaneController**
  - **PlaneCollisionReporter**
  - **Collider**（Cube 自带 Box Collider 即可，确保不是 Trigger）。
- 将 Plane 从 Hierarchy 拖到 `Assets/Prefabs` 文件夹，生成预制体；场景里保留一个实例，位置与 **PlaneStart** 对齐（或先放在 PlaneStart 下作为子物体，再拆开）。

---

## 4. 地图生成器

- 选中 **LevelRoot**，Add Component → **CubeMapGenerator**。
- 在 CubeMapGenerator 中：
  - **Level Root** 拖成自己（LevelRoot）或留空（默认用自身）。
  - 按需调整 Segment Count、Segment Length、Corridor Width/Height、Gap 范围等；**Seed** 填 0 表示每次随机。

---

## 5. 游戏管理

- 在 Hierarchy 中创建空物体，命名为 **GameManager**。
- 挂上 **GameManager** 脚本。
- 在 Inspector 中：
  - **Plane** 拖成场景里的飞机。
  - **Plane Start Point** 拖成 **PlaneStart**。
  - **Map Generator** 拖成 **LevelRoot**（带 CubeMapGenerator 的对象）。
  - **Game UI** 拖成下面创建的 UI 根物体。

---

## 6. UI

- 右键 Hierarchy → UI → **Canvas**（若提示 EventSystem 一并创建）。
- 在 Canvas 下：
  - 创建 **TextMeshPro - Text**，命名 **TimeText**，放在左上角，用于显示「时间: xx.xs」；把 **TimeText** 拖到 GameUI 的 **Time Text**。
  - 创建 **Panel**，命名 **GameOverPanel**，下面放一个 **TextMeshPro - Text**（如「撞机了！按重开」）和一个 **Button**（文字改为「重开」）。
  - 把 **GameOverPanel** 拖到 GameUI 的 **Game Over Panel**，把 **Button** 拖到 **Restart Button**。
  - 默认将 **GameOverPanel** 设为不激活（取消勾选），游戏开始时由代码关闭，撞机时打开。
- 将 **Canvas**（或挂有 GameUI 的物体）拖到 GameManager 的 **Game UI**；若 GameUI 挂在 Canvas 上，就拖 Canvas。

---

## 7. 摄像机跟随

- 选中 **Main Camera**，在 **FollowCamera** 的 **Target** 中拖入场景里的 **Plane**。

---

## 8. 运行与重开

- 按 Play：会先在 LevelRoot 下生成随机 Cube 通道，飞机从 PlaneStart 位置起飞：
  - **W/A/S/D**：前后左右移动
  - **空格 / 左 Ctrl**：上 / 下移动
  - 松开按键时默认“完全悬停”（位置不变化，可在 `PlaneController` 里关闭 `Hold Altitude When Idle`）
  - 撞到障碍物会 Game Over，点「重开」会重新生成地图并重置飞机。

---

## 脚本一览

| 脚本 | 挂载位置 |
|------|----------|
| PlaneController | 飞机 |
| PlaneCollisionReporter | 飞机 |
| Obstacle | 由 CubeMapGenerator 自动挂到生成的 Cube 上 |
| CubeMapGenerator | LevelRoot |
| GameManager | 空物体 GameManager |
| GameUI | Canvas 或任意 UI 根物体 |
| FollowCamera | Main Camera |
