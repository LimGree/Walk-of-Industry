using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Экран «Персонаж»: живое 3D-превью и варианты внешности. Открывается из главного меню и из паузы.
/// Правки видны в превью сразу, сохраняются по «Готово» — один персонаж на все миры (<see cref="AvatarLook"/>).
/// </summary>
public static class CharacterScreen
{
    sealed class State
    {
        public AvatarLook draft;
        public AvatarPreview preview;
        public readonly List<VisualElement> rows = new List<VisualElement>();
    }

    /// <summary>Заполнить панель host. onBack — вернуться на предыдущий экран (превью уже убрано).</summary>
    public static void Fill(VisualElement host, Action onBack)
    {
        Release(host);
        host.Clear();
        host.AddToClassList("char-panel");

        var state = new State { draft = AvatarLook.Current.Clone() };
        host.userData = state;
        state.preview = AvatarPreview.Create(state.draft, 560, 800);

        host.Add(IndustryUi.Text("Title", UiLocale.T("avatar.title"), "title-hero"));
        host.Add(IndustryUi.Text("Sub", UiLocale.T("avatar.subtitle"), "caption"));

        var body = IndustryUi.El("Body", "char-body");
        host.Add(body);

        // ----- превью -----
        var left = IndustryUi.El("Left", "char-left");
        var view = IndustryUi.El("Preview", "char-preview");
        view.style.backgroundImage = Background.FromRenderTexture(state.preview.Texture);
        HookDrag(view, state);
        left.Add(view);
        left.Add(IndustryUi.Text("Hint", UiLocale.T("avatar.hint"), "caption", "char-hint"));
        var quick = IndustryUi.El("Quick", "row", "char-quick");
        quick.Add(IndustryUi.Btn(UiLocale.T("avatar.random"), () =>
        {
            state.draft = AvatarLook.Random();
            Changed(state);
        }, "btn-small", "btn-ghost"));
        quick.Add(IndustryUi.Btn(UiLocale.T("avatar.wave"), () => state.preview?.Wave(), "btn-small", "btn-ghost"));
        left.Add(quick);
        body.Add(left);

        // ----- варианты -----
        var options = IndustryUi.Scroll("Options");
        options.AddToClassList("char-options");
        body.Add(options);

        AddChoice(options, state, "avatar.row_body", AvatarLook.BodyKeys, l => l.body, (l, v) => l.body = v);
        AddSwatches(options, state, "avatar.row_skin", AvatarLook.SkinColors, l => l.skin, (l, v) => l.skin = v);
        AddChoice(options, state, "avatar.row_hair", AvatarLook.HairKeys, l => l.hair, (l, v) => l.hair = v);
        AddSwatches(options, state, "avatar.row_hair_color", AvatarLook.HairColors, l => l.hairColor, (l, v) => l.hairColor = v);
        AddSwatches(options, state, "avatar.row_eyes", AvatarLook.EyeColors, l => l.eyes, (l, v) => l.eyes = v);
        AddChoice(options, state, "avatar.row_hat", AvatarLook.HatKeys, l => l.hat, (l, v) => l.hat = v);
        AddChoice(options, state, "avatar.row_top", AvatarLook.TopKeys, l => l.top, (l, v) => l.top = v);
        AddSwatches(options, state, "avatar.row_top_color", AvatarLook.ClothColors, l => l.topColor, (l, v) => l.topColor = v);
        AddChoice(options, state, "avatar.row_bottom", AvatarLook.BottomKeys, l => l.bottom, (l, v) => l.bottom = v);
        AddSwatches(options, state, "avatar.row_bottom_color", AvatarLook.ClothColors, l => l.bottomColor, (l, v) => l.bottomColor = v);

        // ----- низ -----
        var footer = IndustryUi.El("Footer", "row", "char-footer");
        footer.Add(IndustryUi.Btn(UiLocale.T("avatar.reset"), () =>
        {
            state.draft = new AvatarLook();
            Changed(state);
        }, "btn-small", "btn-ghost"));
        footer.Add(IndustryUi.El("Gap", "grow"));
        footer.Add(IndustryUi.Btn(UiLocale.T("avatar.cancel"), () =>
        {
            Release(host);
            onBack?.Invoke();
        }, "btn-small", "btn-ghost"));
        footer.Add(IndustryUi.Btn(UiLocale.T("avatar.done"), () =>
        {
            AvatarLook.Save(state.draft);
            UiAudio.PlayConfirm();
            Release(host);
            onBack?.Invoke();
        }, "btn-small", "btn-primary"));
        host.Add(footer);

        // Панель убрали из дерева (перестройка меню, выход) — превью не должно остаться жить.
        host.UnregisterCallback(OnDetach);
        host.RegisterCallback(OnDetach);
    }

    static readonly EventCallback<DetachFromPanelEvent> OnDetach = e => Release(e.currentTarget as VisualElement);

    /// <summary>Убрать превью (камера и текстура живут, только пока экран открыт).</summary>
    public static void Release(VisualElement host)
    {
        if (host?.userData is State state && state.preview != null)
        {
            UnityEngine.Object.Destroy(state.preview.gameObject);
            state.preview = null;
        }
    }

    static void Changed(State state)
    {
        state.draft.Clamp();
        state.preview?.SetLook(state.draft);
        for (int i = 0; i < state.rows.Count; i++)
            SettingsControls.Sync(state.rows[i]);
    }

    static void AddChoice(VisualElement parent, State state, string labelKey, string[] keys,
        Func<AvatarLook, int> get, Action<AvatarLook, int> set)
    {
        var chips = new (string label, Func<bool> on, Action click)[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            int idx = i;
            chips[i] = (UiLocale.T(keys[i]), () => get(state.draft) == idx, () =>
            {
                set(state.draft, idx);
                UiAudio.PlaySelect();
                Changed(state);
            });
        }
        VisualElement row = SettingsControls.ChipRow(labelKey, chips);
        row.AddToClassList("char-row");
        state.rows.Add(row);
        parent.Add(row);
    }

    static void AddSwatches(VisualElement parent, State state, string labelKey, Color32[] colors,
        Func<AvatarLook, int> get, Action<AvatarLook, int> set)
    {
        var row = IndustryUi.El("Row", "set-row", "char-row");
        var text = IndustryUi.El("Text", "set-text");
        text.Add(IndustryUi.Text("L", UiLocale.T(labelKey), "set-label"));
        row.Add(text);
        var ctl = IndustryUi.El("Ctl", "set-ctl", "char-swatches");
        row.Add(ctl);

        var swatches = new VisualElement[colors.Length];
        void Refresh()
        {
            int on = get(state.draft);
            for (int n = 0; n < swatches.Length; n++)
                IndustryUi.SetOn(swatches[n], n == on, "is-selected");
        }

        for (int i = 0; i < colors.Length; i++)
        {
            int idx = i;
            var sw = IndustryUi.El("Swatch" + i, "char-swatch");
            sw.style.backgroundColor = (Color)colors[i];
            sw.AddManipulator(new Clickable(() =>
            {
                set(state.draft, idx);
                UiAudio.PlaySelect();
                Changed(state);
            }));
            swatches[i] = sw;
            ctl.Add(sw);
        }

        row.userData = (Action)Refresh;
        Refresh();
        state.rows.Add(row);
        parent.Add(row);
    }

    static void HookDrag(VisualElement view, State state)
    {
        bool dragging = false;
        Vector2 last = Vector2.zero;
        view.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
                return;
            dragging = true;
            last = e.position;
            view.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        view.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!dragging)
                return;
            Vector2 p = e.position;
            state.preview?.Spin(-(p.x - last.x) * 0.6f);
            last = p;
        });
        view.RegisterCallback<PointerUpEvent>(e =>
        {
            dragging = false;
            if (view.HasPointerCapture(e.pointerId))
                view.ReleasePointer(e.pointerId);
        });
        view.RegisterCallback<WheelEvent>(e =>
        {
            state.preview?.Zoom(-Mathf.Sign(e.delta.y) * 0.15f);
            e.StopPropagation();
        });
    }
}
