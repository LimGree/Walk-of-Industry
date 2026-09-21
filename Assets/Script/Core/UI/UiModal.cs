using System;
using UnityEngine.UIElements;

public static class UiModal
{
    static VisualElement host;
    static Label title;
    static Label body;
    static TextField field;
    static Button confirm;
    static Action pending;
    static Action<string> pendingText;

    public static bool IsOpen => host != null && host.style.display == DisplayStyle.Flex;

    public static void Confirm(string heading, string message, string confirmLabel, Action onConfirm, bool danger = true)
    {
        Ensure();
        if (host == null)
            return;
        pending = onConfirm;
        pendingText = null;
        title.text = heading ?? "";
        body.text = message ?? "";
        IndustryUi.Show(field, false);
        SetConfirmDanger(danger);
        IndustryUi.SetButtonLabel(confirm, string.IsNullOrEmpty(confirmLabel) ? UiLocale.T("modal.ok") : confirmLabel);
        RelabelCancel();
        Show();
    }

    public static void Prompt(string heading, string message, string confirmLabel, string initial, Action<string> onConfirm)
    {
        Ensure();
        if (host == null)
            return;
        pending = null;
        pendingText = onConfirm;
        title.text = heading ?? "";
        body.text = message ?? "";
        if (field != null)
        {
            field.value = initial ?? "";
            IndustryUi.Show(field, true);
            field.schedule.Execute(() => field.Focus());
        }
        SetConfirmDanger(false);
        IndustryUi.SetButtonLabel(confirm, string.IsNullOrEmpty(confirmLabel) ? UiLocale.T("modal.ok") : confirmLabel);
        RelabelCancel();
        Show();
    }

    public static void Hide()
    {
        pending = null;
        pendingText = null;
        if (host != null)
            host.style.display = DisplayStyle.None;
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    static void Show()
    {
        if (host == null)
            return;
        host.pickingMode = PickingMode.Position;
        host.style.display = DisplayStyle.Flex;
        host.BringToFront();
        UiAudio.PlayModal();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    static void RelabelCancel()
    {
        Button cancel = host != null ? host.Q<Button>("Cancel") : null;
        if (cancel != null)
            IndustryUi.SetButtonLabel(cancel, UiLocale.T("modal.cancel"));
    }

    static void SetConfirmDanger(bool danger)
    {
        if (confirm == null)
            return;
        IndustryUi.SetOn(confirm, danger, "btn-danger");
        IndustryUi.SetOn(confirm, !danger, "btn-primary");
    }

    static void OnConfirm()
    {
        if (pendingText != null)
        {
            Action<string> fn = pendingText;
            string value = field != null ? field.value : "";
            Hide();
            fn(value);
            return;
        }

        Action act = pending;
        Hide();
        act?.Invoke();
    }

    static void Ensure()
    {
        if (host != null && host.panel != null)
            return;
        VisualElement root = UiRuntime.HostRoot;
        if (root == null)
            return;

        VisualTreeAsset tree = IndustryUi.LoadTree("Modal");
        if (tree != null)
        {
            host = tree.CloneTree();
            host.name = "Modal";
            host.AddToClassList("modal-host");
            host.AddToClassList("screen");
        }
        else
        {
            host = IndustryUi.El("Modal", "modal-host", "screen");
            host.Add(IndustryUi.El("Dim", "dim"));
            var box = IndustryUi.El("Box", "modal-box");
            box.Add(IndustryUi.Text("T", "", "heading-3"));
            box.Add(IndustryUi.Text("B", "", "muted"));
            var fallbackField = new TextField { name = "Field" };
            fallbackField.AddToClassList("field");
            box.Add(fallbackField);
            var actions = IndustryUi.El("A", "modal-actions", "row");
            Button cancelFallback = IndustryUi.Btn(UiLocale.T("modal.cancel"), Hide, "btn-small", "btn-ghost");
            cancelFallback.name = "Cancel";
            actions.Add(cancelFallback);
            Button confirmFallback = IndustryUi.Btn(UiLocale.T("menu.delete"), OnConfirm, "btn-small", "btn-danger");
            confirmFallback.name = "Confirm";
            actions.Add(confirmFallback);
            host.Add(box);
        }

        title = host.Q<Label>("T");
        body = host.Q<Label>("B");
        if (body != null)
            body.style.marginTop = 12;
        field = host.Q<TextField>("Field");
        if (field != null)
        {
            field.AddToClassList("field");
            field.style.marginTop = 10;
            IndustryUi.Show(field, false);
        }
        confirm = host.Q<Button>("Confirm");
        Button cancel = host.Q<Button>("Cancel");
        if (cancel != null)
        {
            cancel.clicked += Hide;
            cancel.RegisterCallback<PointerEnterEvent>(_ => UiAudio.PlayHover());
        }
        if (confirm != null)
        {
            confirm.clicked += OnConfirm;
            confirm.RegisterCallback<PointerEnterEvent>(_ => UiAudio.PlayHover());
        }
        host.style.display = DisplayStyle.None;
        root.Add(host);
    }
}
