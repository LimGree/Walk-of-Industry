using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Детали экрана настроек. Каждая строка: слева название (+ описание), справа контрол.
/// Тумблер — переключатель, слайдер — полоса + значение, выбор — сегменты, список — dropdown.
/// </summary>
public static class SettingsControls
{
    /// <summary>Заголовок группы внутри вкладки.</summary>
    public static VisualElement Group(string titleKey)
    {
        var head = IndustryUi.El("Group", "set-group");
        head.Add(IndustryUi.Text("T", UiLocale.T(titleKey), "set-group-title"));
        head.Add(IndustryUi.El("Line", "set-group-line"));
        return head;
    }

    /// <summary>Добавляет серое пояснение под названием строки.</summary>
    public static T Describe<T>(T row, string descKey) where T : VisualElement
    {
        if (row == null || string.IsNullOrEmpty(descKey))
            return row;
        VisualElement text = row.Q(className: "set-text");
        if (text != null)
            text.Add(IndustryUi.Text("D", UiLocale.T(descKey), "set-desc"));
        return row;
    }

    /// <summary>Перерисовать строку после изменения значения извне (например, после окна подтверждения).</summary>
    public static void Sync(VisualElement row)
    {
        (row?.userData as Action)?.Invoke();
    }

    public static VisualElement Toggle(string key, Func<bool> get, Action<bool> set)
    {
        VisualElement row = Row(UiLocale.T(key), out VisualElement ctl);
        row.AddToClassList("set-row-toggle");
        var state = IndustryUi.Text("State", "", "set-switch-state");
        var track = IndustryUi.El("Switch", "set-switch");
        track.Add(IndustryUi.El("Knob", "set-knob"));
        ctl.Add(state);
        ctl.Add(track);

        void Refresh()
        {
            bool on = get();
            IndustryUi.SetOn(row, on, "is-on");
            state.text = on ? UiLocale.T("settings.on") : UiLocale.T("settings.off");
        }

        row.userData = (Action)Refresh;
        row.AddManipulator(new Clickable(() =>
        {
            set(!get());
            UiAudio.PlayToggle();
            Refresh();
        }));
        Refresh();
        return row;
    }

    /// <summary>
    /// Слайдер. Ключ локали вида «Название  {0} ед.»: часть до {0} — название, после — единица у значения.
    /// </summary>
    public static VisualElement SliderRow(string key, float min, float max, Func<float> get, Action<float> set, Func<float, object> format)
    {
        SplitTemplate(key, out string name, out string suffix);
        VisualElement row = Row(name, out VisualElement ctl);
        row.AddToClassList("set-row-slider");
        var slider = new Slider(min, max) { value = get() };
        slider.AddToClassList("set-slider");
        var value = IndustryUi.Text("V", "", "set-value");

        void Refresh()
        {
            string v = Convert.ToString(format(get()), System.Globalization.CultureInfo.CurrentCulture);
            if (string.IsNullOrEmpty(suffix))
                value.text = v;
            else if (suffix.StartsWith("%", StringComparison.Ordinal))
                value.text = v + suffix;
            else
                value.text = v + " " + suffix;
        }

        slider.RegisterValueChangedCallback(evt =>
        {
            set(evt.newValue);
            Refresh();
        });
        row.userData = (Action)(() =>
        {
            slider.SetValueWithoutNotify(get());
            Refresh();
        });
        Refresh();
        ctl.Add(slider);
        ctl.Add(value);
        return row;
    }

    public static VisualElement ChipRow(string labelKey, params (string label, Func<bool> on, Action click)[] chips)
    {
        VisualElement row = Row(UiLocale.T(labelKey), out VisualElement ctl);
        row.AddToClassList("set-row-choice");
        if (chips.Length > 4)
            row.AddToClassList("set-row-wide");
        var seg = IndustryUi.El("Seg", "set-seg");
        var buttons = new Button[chips.Length];

        void Refresh()
        {
            for (int n = 0; n < chips.Length; n++)
                IndustryUi.SetOn(buttons[n], chips[n].on(), "is-selected");
        }

        for (int i = 0; i < chips.Length; i++)
        {
            int idx = i;
            Button b = Chip(chips[i].label, () =>
            {
                chips[idx].click();
                Refresh();
            });
            buttons[i] = b;
            seg.Add(b);
        }
        row.userData = (Action)Refresh;
        Refresh();
        ctl.Add(seg);
        return row;
    }

    public static Button Chip(string label, Action onClick)
    {
        Button button = IndustryUi.Btn(label, onClick, "set-seg-btn");
        button.RemoveFromClassList("btn");
        return button;
    }

    public static VisualElement Dropdown(string labelKey, List<string> choices, int selected, Action<string> onPick)
    {
        VisualElement row = Row(UiLocale.T(labelKey), out VisualElement ctl);
        var field = new DropdownField(choices, Mathf.Clamp(selected, 0, Mathf.Max(0, choices.Count - 1)));
        field.AddToClassList("field");
        field.AddToClassList("set-dropdown");
        field.RegisterValueChangedCallback(evt => onPick?.Invoke(evt.newValue));
        ctl.Add(field);
        return row;
    }

    /// <summary>Строка с кнопкой действия справа (открыть папку, переместить миникарту…).</summary>
    public static VisualElement ActionRow(string labelKey, string buttonKey, Action onClick)
    {
        VisualElement row = Row(UiLocale.T(labelKey), out VisualElement ctl);
        ctl.Add(IndustryUi.Btn(UiLocale.T(buttonKey), onClick, "btn-small", "set-action"));
        return row;
    }

    /// <summary>Окно «Это чит» — действие выполняется только после подтверждения.</summary>
    public static void ConfirmCheat(string bodyKey, Action onConfirm)
    {
        UiModal.Confirm(
            UiLocale.T("settings.cheat_title"),
            UiLocale.T(bodyKey),
            UiLocale.T("settings.cheat_enable"),
            onConfirm);
    }

    /// <summary>Тумблер-чит: включение через окно подтверждения, во включённом виде горит красным.</summary>
    public static VisualElement CheatToggle(string key, string bodyKey, Func<bool> get, Action<bool> set, string descKey = null)
    {
        VisualElement row = null;
        row = Toggle(key, get, v =>
        {
            if (!v)
            {
                set(false);
                return;
            }
            ConfirmCheat(bodyKey, () =>
            {
                set(true);
                Sync(row);
            });
        });
        row.AddToClassList("is-cheat");
        return Describe(row, descKey);
    }

    static VisualElement Row(string name, out VisualElement ctl)
    {
        var row = IndustryUi.El("Row", "set-row");
        var text = IndustryUi.El("Text", "set-text");
        text.Add(IndustryUi.Text("L", name, "set-label"));
        row.Add(text);
        ctl = IndustryUi.El("Ctl", "set-ctl");
        row.Add(ctl);
        return row;
    }

    static void SplitTemplate(string key, out string name, out string suffix)
    {
        const string mark = "\u0001";
        string t = UiLocale.T(key, mark);
        int i = t.IndexOf(mark, StringComparison.Ordinal);
        if (i < 0)
        {
            name = t.Trim();
            suffix = "";
            return;
        }
        name = t.Substring(0, i).Trim();
        suffix = t.Substring(i + mark.Length).Trim();
    }
}
