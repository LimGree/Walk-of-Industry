using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Стек игровых окон и единый шлюз ввода.
///
/// Окна (сумка, стройка, станок/исследования, чертежи, выделение, магазин, карта, ремонт, дроны)
/// сообщают о себе через Opened / Closed. Последнее открытое — сверху: его панель рисуется выше остальных,
/// Esc закрывает именно его. Своя горячая клавиша окна: верхнее — закрыть, открытое ниже — поднять наверх,
/// закрытое — открыть поверх (Hotkey).
///
/// Пока открыто хоть одно окно (или пауза, модалка, консоль, ввод текста, модальный шаг обучения),
/// игровой ввод выключен целиком: камера, ходьба, клики по миру, хотбар, клавиши стройки (GameplayBlocked).
/// </summary>
public static class UiStack
{
    /// <summary>Порядок панелей стека: выше HUD и обучения (до 160), ниже паузы (500).</summary>
    const int StackBase = 300;
    const int StackStep = 4;

    sealed class Entry
    {
        public string id;
        public Action close;
        public int baseOrder;
    }

    static readonly List<Entry> stack = new List<Entry>();
    static bool closingAll;

    public static event Action Changed;

    public static bool Any => stack.Count > 0;
    public static int Count => stack.Count;
    public static string Top => stack.Count > 0 ? stack[stack.Count - 1].id : null;

    public static bool IsOpen(string id) => IndexOf(id) >= 0;
    public static bool IsTop(string id) => stack.Count > 0 && stack[stack.Count - 1].id == id;

    /// <summary>
    /// Весь игровой ввод (камера, ходьба, мир, хотбар, стройка) выключен.
    /// </summary>
    public static bool GameplayBlocked =>
        Any
        || UiModal.IsOpen
        || KeybindStore.BlocksGameplayInput
        || DevConsole.IsOpen
        || (GameManager.Instance != null && GameManager.Instance.IsPaused)
        || (TutorialSystem.Instance != null && TutorialSystem.Instance.IsModal);

    /// <summary>
    /// Горячие клавиши окон (I, M, T, P, магазин…) не работают: модалка, пауза, консоль, ввод текста.
    /// Открытые окна им не мешают — это стек.
    /// </summary>
    public static bool HotkeysBlocked =>
        UiModal.IsOpen
        || KeybindStore.BlocksGameplayInput
        || DevConsole.IsOpen
        || PhotoMode.IsActive
        || (GameManager.Instance != null && GameManager.Instance.IsPaused)
        || (TutorialSystem.Instance != null && TutorialSystem.Instance.IsModal);

    /// <summary>Окно открылось (или поднято наверх).</summary>
    public static void Opened(string id, Action close, int baseOrder)
    {
        if (string.IsNullOrEmpty(id))
            return;
        int i = IndexOf(id);
        Entry e = i >= 0 ? stack[i] : new Entry { id = id };
        if (i >= 0)
            stack.RemoveAt(i);
        e.close = close;
        e.baseOrder = baseOrder;
        stack.Add(e);
        Reorder();
        Refocus();
    }

    /// <summary>Окно закрылось.</summary>
    public static void Closed(string id)
    {
        int i = IndexOf(id);
        if (i < 0)
            return;
        Entry e = stack[i];
        stack.RemoveAt(i);
        RestoreOrder(e);
        Reorder();
        if (!closingAll)
            Refocus();
    }

    /// <summary>
    /// Горячая клавиша окна: верхнее — закрыть, открытое ниже — поднять, закрытое — открыть.
    /// </summary>
    public static void Hotkey(string id, Action open, Action close, int baseOrder)
    {
        if (HotkeysBlocked)
            return;
        if (IsTop(id))
        {
            close?.Invoke();
            return;
        }
        if (IsOpen(id))
        {
            Opened(id, close, baseOrder);
            UiAudio.PlayOpen();
            return;
        }
        open?.Invoke();
    }

    /// <summary>Esc: сначала модалка, потом верхнее окно. false — закрывать нечего (можно ставить паузу).</summary>
    public static bool CloseTop()
    {
        if (UiModal.IsOpen)
        {
            UiModal.Hide();
            return true;
        }
        if (stack.Count == 0)
            return false;
        Entry top = stack[stack.Count - 1];
        top.close?.Invoke();
        // Окно не отписалось само — убираем, чтобы Esc не застревал.
        if (IsOpen(top.id) && IsTop(top.id))
            Closed(top.id);
        return true;
    }

    /// <summary>Закрыть все окна (пауза, выход из режима стройки, смена сцены).</summary>
    public static void CloseAll()
    {
        if (stack.Count == 0)
            return;
        closingAll = true;
        var copy = new List<Entry>(stack);
        for (int i = copy.Count - 1; i >= 0; i--)
        {
            copy[i].close?.Invoke();
            if (IsOpen(copy[i].id))
                Closed(copy[i].id);
        }
        closingAll = false;
        Refocus();
    }

    /// <summary>Сбросить стек без вызова закрытий (выгрузка сцены).</summary>
    public static void Clear()
    {
        for (int i = 0; i < stack.Count; i++)
            RestoreOrder(stack[i]);
        stack.Clear();
        Changed?.Invoke();
    }

    static int IndexOf(string id)
    {
        for (int i = 0; i < stack.Count; i++)
        {
            if (stack[i].id == id)
                return i;
        }
        return -1;
    }

    static void Reorder()
    {
        for (int i = 0; i < stack.Count; i++)
        {
            PanelSettings ps = IndustryUi.Settings(stack[i].baseOrder);
            if (ps != null)
                ps.sortingOrder = StackBase + i * StackStep;
        }
        Changed?.Invoke();
    }

    static void RestoreOrder(Entry e)
    {
        PanelSettings ps = IndustryUi.Settings(e.baseOrder);
        if (ps != null)
            ps.sortingOrder = e.baseOrder;
    }

    static void Refocus()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = Any ? CursorLockMode.None : UnityEngine.Cursor.lockState;
            UnityEngine.Cursor.visible = Any || UnityEngine.Cursor.visible;
        }
    }
}
