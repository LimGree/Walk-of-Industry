using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class SettingsControls
{
    public static VisualElement Toggle(string key, Func<bool> get, Action<bool> set)
    {
        Button button = null;
        button = IndustryUi.Btn("", () =>
        {
            set(!get());
            IndustryUi.SetButtonLabel(button, Label(key, get()));
        });
        IndustryUi.SetButtonLabel(button, Label(key, get()));
        return button;
    }

    public static VisualElement SliderRow(string key, float min, float max, Func<float> get, Action<float> set, Func<float, object> format)
    {
        var box = IndustryUi.El("S", "volume-row", "col");
        var label = IndustryUi.Text("L", "", "caption");
        var slider = new Slider(min, max) { value = get() };
        void Refresh()
        {
            label.text = UiLocale.T(key, format(get()));
        }
        slider.RegisterValueChangedCallback(evt =>
        {
            set(evt.newValue);
            Refresh();
        });
        Refresh();
        box.Add(label);
        box.Add(slider);
        return box;
    }

    public static VisualElement ChipRow(string labelKey, params (string label, Func<bool> on, Action click)[] chips)
    {
        var row = IndustryUi.El("Chips", "lang-row");
        row.style.flexWrap = Wrap.Wrap;
        row.Add(IndustryUi.Text("L", UiLocale.T(labelKey), "body-text", "grow"));
        var buttons = new Button[chips.Length];
        for (int i = 0; i < chips.Length; i++)
        {
            int idx = i;
            Button b = Chip(chips[i].label, () =>
            {
                chips[idx].click();
                for (int n = 0; n < chips.Length; n++)
                    IndustryUi.SetOn(buttons[n], chips[n].on(), "is-selected");
            });
            buttons[i] = b;
            IndustryUi.SetOn(b, chips[i].on(), "is-selected");
            row.Add(b);
        }
        return row;
    }

    public static Button Chip(string label, Action onClick)
    {
        Button button = IndustryUi.Btn(label, onClick, "btn-small", "cat-chip");
        button.RemoveFromClassList("btn");
        return button;
    }

    public static VisualElement Dropdown(string labelKey, System.Collections.Generic.List<string> choices, int selected, Action<string> onPick)
    {
        var box = IndustryUi.El("Dd", "col");
        box.Add(IndustryUi.Text("L", UiLocale.T(labelKey), "caption"));
        var field = new DropdownField(choices, Mathf.Clamp(selected, 0, Mathf.Max(0, choices.Count - 1)));
        field.AddToClassList("field");
        field.RegisterValueChangedCallback(evt => onPick?.Invoke(evt.newValue));
        box.Add(field);
        return box;
    }

    static string Label(string key, bool on)
    {
        return UiLocale.T(key) + "  ·  " + (on ? UiLocale.T("settings.on") : UiLocale.T("settings.off"));
    }
}
