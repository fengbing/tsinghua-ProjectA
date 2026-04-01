using System;
using UnityEngine;

public class DecryptPuzzleSystem : MonoBehaviour
{
    public const int CodeLength = 4;

    public event Action<int> OnDigitEntered;
    public event Action OnPuzzleSolved;
    public event Action OnPuzzleFailed;
    public event Action OnCodeGenerated;

    private int[] _correctCode;
    private int[] _currentInput;
    private int _inputLength;

    public string CurrentInput => string.Join("", _currentInput);
    public string CorrectCode => string.Join("", _correctCode);
    public int[] CorrectCodeDigits => _correctCode;
    public bool IsComplete => _inputLength == CodeLength;

    void Awake()
    {
        _currentInput = new int[CodeLength];
        GenerateNewCode();
    }

    public void Initialize()
    {
        GenerateNewCode();
        ClearInput();
    }

    public void GenerateNewCode()
    {
        _correctCode = new int[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            _correctCode[i] = UnityEngine.Random.Range(0, 10);
        }
        Debug.Log($"[DecryptPuzzle] 新密码已生成: {CorrectCode}");
        OnCodeGenerated?.Invoke();
    }

    public bool TryEnterDigit(int digit)
    {
        if (_inputLength >= CodeLength)
            return false;

        _currentInput[_inputLength] = digit;
        _inputLength++;
        OnDigitEntered?.Invoke(_inputLength);

        if (IsComplete)
        {
            CheckSolution();
        }
        return true;
    }

    public void ClearInput()
    {
        _currentInput = new int[CodeLength];
        _inputLength = 0;
        OnDigitEntered?.Invoke(0);
    }

    public bool TryConfirm()
    {
        if (!IsComplete)
            return false;

        CheckSolution();
        return true;
    }

    public void Backspace()
    {
        if (_inputLength > 0)
        {
            _inputLength--;
            _currentInput[_inputLength] = 0;
            OnDigitEntered?.Invoke(_inputLength);
        }
    }

    private void CheckSolution()
    {
        bool correct = true;
        for (int i = 0; i < CodeLength; i++)
        {
            if (_currentInput[i] != _correctCode[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            Debug.Log("[DecryptPuzzle] 密码正确！解锁成功！");
            OnPuzzleSolved?.Invoke();
        }
        else
        {
            Debug.Log("[DecryptPuzzle] 密码错误！");
            ClearInput();
            OnPuzzleFailed?.Invoke();
        }
    }
}
