using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

public static class KeybindSettingsUI
{
    public static void Fill(VisualElement parent, Action onBack, bool page = false)
    {
        if (parent == null)
            return;

        if (!page)
            parent.Clear();
        if (!page)
            parent.Add(IndustryUi.Text("T", UiLocale.T("keys.title"), "title-hero"));
        parent.Add(IndustryUi.Text("H", UiLocale.T("keys.hint"), "muted"));
        VisualElement list = parent;
        if (!page)
        {
            var scroll = IndustryUi.Scroll("Keys");
            scroll.style.maxHeight = 420;
            parent.Add(scroll);
            list = scroll;
        }

        var entries = KeybindStore.BuildEntries();
        var keyLabels = new List<Label>(entries.Count);
        string lastGroup = null;
        for (int i = 0; i < entries.Count; i++)
        {
            KeybindStore.Entry entry = entries[i];
            string group = GroupOf(entry.actionName);
            if (group != lastGroup)
            {
                list.Add(IndustryUi.Text("G" + group, group, "key-group"));
                lastGroup = group;
            }

            var row = IndustryUi.El("R", "key-row");
            row.Add(IndustryUi.Text("L", entry.label, "body-text", "grow"));
            Button key = IndustryUi.Btn(KeybindStore.Format(entry), null, "btn-small");
            key.AddToClassList("keycap");
            Label lab = key.Q<Label>(className: "btn-label");
            keyLabels.Add(lab);
            key.clicked += () =>
            {
                if (lab != null)
                    lab.text = "...";
                KeybindStore.StartRebind(entry, () => Refresh(entries, keyLabels));
            };
            row.Add(key);
            Button reset = IndustryUi.Btn("↺", () =>
            {
                if (KeybindStore.IsListening)
                    return;
                KeybindStore.ResetBinding(entry);
                Refresh(entries, keyLabels);
            }, "btn-small", "btn-ghost");
            row.Add(reset);
            list.Add(row);
        }

        parent.Add(IndustryUi.El("Div", "divider"));
        parent.Add(IndustryUi.Btn(UiLocale.T("keys.reset_all"), () =>
        {
            UiModal.Confirm(
                UiLocale.T("keys.reset_title"),
                UiLocale.T("keys.reset_body"),
                UiLocale.T("keys.reset"),
                () =>
                {
                    KeybindStore.ResetAll();
                    Refresh(entries, keyLabels);
                });
        }, "btn-ghost"));
        if (!page && onBack != null)
            parent.Add(IndustryUi.Btn(UiLocale.T("menu.back"), onBack, "btn-ghost"));
        Refresh(entries, keyLabels);
    }

    static string GroupOf(string action)
    {
        switch (action)
        {
            case "Move":
            case "Jump":
            case "Sprint":
            case "Zoom":
                return UiLocale.T("keys.movement");
            case "Place":
            case "Demolish":
            case "Rotate":
            case "BuildMode":
            case "SelectMode":
            case "ClearSelection":
            case "Copy":
            case "Paste":
            case "MoveSelection":
            case "Modifier":
            case "Delete":
                return UiLocale.T("keys.build");
            case "Pause":
                return UiLocale.T("keys.system");
            default:
                return UiLocale.T("keys.game");
        }
    }

    static void Refresh(List<KeybindStore.Entry> entries, List<Label> labels)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            string path = KeybindStore.EffectivePath(entries[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            counts.TryGetValue(path, out int n);
            counts[path] = n + 1;
        }

        for (int i = 0; i < labels.Count && i < entries.Count; i++)
        {
            if (labels[i] == null)
                continue;
            string path = KeybindStore.EffectivePath(entries[i]);
            bool conflict = !string.IsNullOrEmpty(path) && counts.TryGetValue(path, out int n) && n > 1;
            labels[i].text = KeybindStore.Format(entries[i]);
            labels[i].EnableInClassList("warn", conflict);
        }
    }
}
