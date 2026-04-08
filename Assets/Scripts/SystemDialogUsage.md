# SystemDialogController Usage

## Inspector Setup

1. Add `SystemDialogController` to a persistent scene object (for example `UIRoot`).
2. Assign `Target Canvas` if you want a specific canvas.
3. (Optional) Assign `Dialog Panel` and `Dialog Text` if you already have a designed UI prefab.
4. (Optional) Assign `Voice Source`; if empty, one is auto-created.
5. Tune style:
   - `Panel Height`
   - `Panel Color` (default black)
   - `Text Color` (default white)
   - `Font Size`

## API

- `PlayDialog(IList<SystemDialogLine> lines)`: show dialog and play lines in order.
- `SkipCurrentLine()`: complete current line immediately.
- `CancelAndHide()`: stop coroutine/audio and hide panel.
- `onLineCompleted(int index)`: callback after each line.
- `onDialogCompleted`: callback after full sequence.

## Example

```csharp
systemDialog.PlayDialog(new List<SystemDialogLine>
{
    new() { text = "第一句", voiceClip = voice1, characterInterval = 0.04f },
    new() { text = "第二句", voiceClip = voice2, characterInterval = 0.04f }
});
```

## Runtime Behavior

- Repeated trigger: new `PlayDialog` cancels current session and starts new one.
- Scene/object disable or destroy: coroutine and audio are cleaned up automatically.
- Pause consistency: when `Use Unscaled Time` is enabled, typewriter speed is not affected by `Time.timeScale`.
