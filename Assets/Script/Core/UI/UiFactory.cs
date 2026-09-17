using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UiFactory
{
    public static GameObject CreateRecipeCard(Transform parent, RecipeData recipe, bool selected, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "RecipeCard", selected);
        Button button = card.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        Image output = CreateIcon(card.transform, "Output", Vector2.zero, new Vector2(72f, 72f));
        RectTransform iconRt = output.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(16f, 6f);
        ItemData outItem = FirstItem(recipe != null ? recipe.outputs : null);
        SetIcon(output, outItem != null ? outItem.icon : null);

        TextMeshProUGUI title = UiTheme.AddText(card.transform, "Title", recipe != null ? recipe.displayName : "Recipe", 22f, UiTheme.Text);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(104f, -10f);
        titleRt.sizeDelta = new Vector2(-122f, 28f);

        RectTransform row = CreateChipRow(card.transform, "IO", 104f, 12f, -16f, 52f);
        AppendStacks(row, recipe != null ? recipe.inputs : null);
        CreateArrow(row);
        AppendStacks(row, recipe != null ? recipe.outputs : null);

        return card;
    }

    public static GameObject CreateFilterCard(Transform parent, Sprite icon, string title, string subtitle, bool selected, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "FilterCard", selected);
        Button button = card.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        Image image = CreateIcon(card.transform, "Icon", Vector2.zero, new Vector2(56f, 56f));
        RectTransform iconRt = image.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(12f, 0f);
        SetIcon(image, icon);

        TextMeshProUGUI titleText = UiTheme.AddText(card.transform, "Title", title, 20f, selected ? UiTheme.Accent : UiTheme.Text);
        titleText.fontStyle = FontStyles.Bold;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.48f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(80f, 2f);
        titleRt.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI sub = UiTheme.AddText(card.transform, "Subtitle", subtitle ?? "", 15f, UiTheme.TextDim);
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0.52f);
        subRt.offsetMin = new Vector2(80f, 8f);
        subRt.offsetMax = new Vector2(-12f, -2f);

        return card;
    }

    public static GameObject CreateActionButton(Transform parent, string title, string subtitle, bool enabled, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "ActionButton", enabled);
        Button button = card.GetComponent<Button>();
        button.interactable = enabled;
        button.onClick.AddListener(() => onClick?.Invoke());

        if (!enabled)
        {
            Image bg = card.GetComponent<Image>();
            if (bg != null)
                bg.color = UiTheme.Locked;
        }

        TextMeshProUGUI titleText = UiTheme.AddText(
            card.transform,
            "Title",
            title,
            24f,
            enabled ? UiTheme.Accent : UiTheme.TextDim);
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.48f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(22f, 4f);
        titleRt.offsetMax = new Vector2(-22f, -10f);

        TextMeshProUGUI sub = UiTheme.AddText(
            card.transform,
            "Subtitle",
            subtitle ?? "",
            18f,
            UiTheme.TextDim);
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0.52f);
        subRt.offsetMin = new Vector2(22f, 12f);
        subRt.offsetMax = new Vector2(-22f, -4f);

        return card;
    }

    public static GameObject CreateBuildingCard(Transform parent, BuildingData data, bool unlocked, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "BuildingCard", false);
        Image bg = card.GetComponent<Image>();
        if (!unlocked)
            bg.color = UiTheme.Locked;

        Button button = card.GetComponent<Button>();
        button.interactable = unlocked;
        button.onClick.AddListener(() => onClick?.Invoke());

        Image icon = CreateIcon(card.transform, "Icon", new Vector2(0f, 18f), new Vector2(96f, 96f));
        SetIcon(icon, data != null ? data.icon : null);
        if (!unlocked)
            icon.color = new Color(1f, 1f, 1f, 0.35f);

        string titleValue = data != null ? data.displayName : "Building";
        int cost = Economy.BuildCost(data);
        if (cost > 0)
            titleValue += "  ·  " + cost + "¤";

        TextMeshProUGUI title = UiTheme.AddText(
            card.transform,
            "Title",
            titleValue,
            16f,
            unlocked ? UiTheme.Text : UiTheme.TextDim);
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(1f, 0f);
        titleRt.pivot = new Vector2(0.5f, 0f);
        titleRt.anchoredPosition = new Vector2(0f, 10f);
        titleRt.sizeDelta = new Vector2(-12f, 28f);

        if (!unlocked)
        {
            TextMeshProUGUI lockText = UiTheme.AddText(card.transform, "Lock", "LOCKED", 12f, UiTheme.Warn);
            lockText.alignment = TextAlignmentOptions.Center;
            RectTransform lockRt = lockText.rectTransform;
            lockRt.anchoredPosition = new Vector2(0f, 42f);
            lockRt.sizeDelta = new Vector2(100f, 18f);
        }

        return card;
    }

    public static GameObject CreateResearchCard(Transform parent, ResearchNodeData node, string status, bool canStart, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "ResearchCard", status == "ACTIVE");
        Button button = card.GetComponent<Button>();
        button.interactable = canStart;
        button.onClick.AddListener(() => onClick?.Invoke());

        Image icon = CreateIcon(card.transform, "Icon", Vector2.zero, new Vector2(72f, 72f));
        RectTransform iconRt = icon.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(16f, 4f);
        SetIcon(icon, node != null ? node.icon : null);

        TextMeshProUGUI title = UiTheme.AddText(card.transform, "Title", node != null ? node.displayName : "Research", 22f, UiTheme.Text);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(104f, -8f);
        titleRt.sizeDelta = new Vector2(-200f, 26f);

        Color badgeColor = status == "DONE" ? UiTheme.Ok : status == "ACTIVE" ? UiTheme.Accent : status == "LOCKED" ? UiTheme.Warn : UiTheme.TextDim;
        TextMeshProUGUI badge = UiTheme.AddText(card.transform, "Status", status, 15f, badgeColor);
        badge.alignment = TextAlignmentOptions.MidlineRight;
        badge.fontStyle = FontStyles.Bold;
        RectTransform badgeRt = badge.rectTransform;
        badgeRt.anchorMin = new Vector2(1f, 1f);
        badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(1f, 1f);
        badgeRt.anchoredPosition = new Vector2(-14f, -10f);
        badgeRt.sizeDelta = new Vector2(88f, 22f);

        RectTransform need = CreateChipRow(card.transform, "Need", 104f, 78f, -16f, 44f);
        AppendLabel(need, "нужно");
        AppendStacks(need, node != null ? node.requiredItems : null);

        RectTransform reward = CreateChipRow(card.transform, "Reward", 104f, 14f, -16f, 44f);
        AppendLabel(reward, "даст");
        if (node != null)
        {
            AppendBuildings(reward, node.unlockedBuildings);
            AppendRecipes(reward, node.unlockedRecipes);
        }

        return card;
    }

    public static GameObject CreateStorageSlot(Transform parent)
    {
        GameObject slot = new GameObject("StorageSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slot.transform.SetParent(parent, false);
        UiTheme.StyleImage(slot.GetComponent<Image>(), UiTheme.Chip);

        Image icon = CreateIcon(slot.transform, "Icon", new Vector2(0f, 8f), new Vector2(72f, 72f));
        icon.enabled = false;

        TextMeshProUGUI count = UiTheme.AddText(slot.transform, "Count", "", 16f, UiTheme.Text);
        count.alignment = TextAlignmentOptions.BottomRight;
        count.fontStyle = FontStyles.Bold;
        RectTransform countRt = count.rectTransform;
        countRt.anchorMin = new Vector2(0f, 0f);
        countRt.anchorMax = new Vector2(1f, 0f);
        countRt.pivot = new Vector2(1f, 0f);
        countRt.offsetMin = new Vector2(6f, 4f);
        countRt.offsetMax = new Vector2(-8f, 26f);

        return slot;
    }

    public static void BindStorageSlot(GameObject slot, ItemStack stack)
    {
        if (slot == null)
            return;

        Transform iconTf = slot.transform.Find("Icon");
        Transform countTf = slot.transform.Find("Count");
        Image icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
        TextMeshProUGUI count = countTf != null ? countTf.GetComponent<TextMeshProUGUI>() : null;

        bool hasItem = stack != null && !stack.IsEmpty && stack.item != null;
        if (icon != null)
        {
            icon.sprite = hasItem ? stack.item.icon : null;
            icon.enabled = hasItem && stack.item.icon != null;
            icon.color = Color.white;
        }

        if (count != null)
            count.text = hasItem ? stack.amount.ToString() : "";
    }

    public static GameObject CreateHotbarSlot(Transform parent, int index)
    {
        GameObject slot = new GameObject("Slot_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slot.transform.SetParent(parent, false);
        UiTheme.StyleImage(slot.GetComponent<Image>(), UiTheme.Card);

        Image highlight = UiTheme.AddImage(slot.transform, "Highlight", Vector2.zero, UiTheme.AccentDim);
        highlight.gameObject.name = "Highlight";
        Stretch(highlight.rectTransform, 0f);
        highlight.enabled = false;

        Image icon = UiTheme.AddImage(slot.transform, "Icon", new Vector2(48f, 48f), Color.white);
        icon.sprite = null;
        icon.enabled = false;
        icon.preserveAspect = true;
        icon.type = Image.Type.Simple;

        TextMeshProUGUI key = UiTheme.AddText(slot.transform, "Key", (index + 1).ToString(), 12f, UiTheme.TextDim);
        key.alignment = TextAlignmentOptions.TopLeft;
        RectTransform keyRt = key.rectTransform;
        keyRt.anchorMin = new Vector2(0f, 1f);
        keyRt.anchorMax = new Vector2(0f, 1f);
        keyRt.pivot = new Vector2(0f, 1f);
        keyRt.anchoredPosition = new Vector2(6f, -4f);
        keyRt.sizeDelta = new Vector2(20f, 16f);

        return slot;
    }

    static GameObject CreateCard(Transform parent, string name, bool selected)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);
        Image bg = card.GetComponent<Image>();
        UiTheme.StyleImage(bg, selected ? UiTheme.AccentDim : UiTheme.Card);

        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        colors.colorMultiplier = 1f;
        card.GetComponent<Button>().colors = colors;
        card.GetComponent<Button>().targetGraphic = bg;
        return card;
    }

    static RectTransform CreateChipRow(Transform parent, string name, float left, float bottom, float right, float height)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, bottom + height);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 6f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 0, 0);
        return rt;
    }

    static void AppendStacks(Transform row, List<ItemStack> stacks)
    {
        if (stacks == null || stacks.Count == 0)
        {
            AppendLabel(row, "—");
            return;
        }

        int added = 0;
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null || stack.item == null)
                continue;
            CreateStackChip(row, stack.item.icon, stack.amount);
            added++;
        }

        if (added == 0)
            AppendLabel(row, "—");
    }

    static void AppendBuildings(Transform row, List<BuildingData> buildings)
    {
        if (buildings == null)
            return;

        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null)
                CreateStackChip(row, buildings[i].icon, 0);
        }
    }

    static void AppendRecipes(Transform row, List<RecipeData> recipes)
    {
        if (recipes == null)
            return;

        for (int i = 0; i < recipes.Count; i++)
        {
            ItemData output = FirstItem(recipes[i] != null ? recipes[i].outputs : null);
            if (output != null)
                CreateStackChip(row, output.icon, 0);
        }
    }

    static void AppendLabel(Transform row, string text)
    {
        TextMeshProUGUI label = UiTheme.AddText(row, "Label", text, 14f, UiTheme.TextDim);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform rt = label.rectTransform;
        rt.sizeDelta = new Vector2(58f, 36f);
    }

    static void CreateArrow(Transform row)
    {
        TextMeshProUGUI arrow = UiTheme.AddText(row, "Arrow", "→", 22f, UiTheme.Accent);
        arrow.alignment = TextAlignmentOptions.Center;
        arrow.fontStyle = FontStyles.Bold;
        arrow.rectTransform.sizeDelta = new Vector2(28f, 40f);
    }

    static void CreateStackChip(Transform parent, Sprite sprite, int amount)
    {
        GameObject chip = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        chip.transform.SetParent(parent, false);
        RectTransform rt = chip.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(48f, 48f);
        UiTheme.StyleImage(chip.GetComponent<Image>(), UiTheme.Chip);

        Image icon = CreateIcon(chip.transform, "Icon", Vector2.zero, new Vector2(36f, 36f));
        SetIcon(icon, sprite);

        if (amount > 0)
        {
            TextMeshProUGUI count = UiTheme.AddText(chip.transform, "Count", amount.ToString(), 13f, UiTheme.Text);
            count.alignment = TextAlignmentOptions.BottomRight;
            count.fontStyle = FontStyles.Bold;
            RectTransform countRt = count.rectTransform;
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(1f, 0f);
            countRt.pivot = new Vector2(1f, 0f);
            countRt.offsetMin = new Vector2(0f, 1f);
            countRt.offsetMax = new Vector2(-3f, 16f);
        }
    }

    static Image CreateIcon(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        Image image = UiTheme.AddImage(parent, name, size, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.sprite = null;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        return image;
    }

    static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static ItemData FirstItem(List<ItemStack> stacks)
    {
        if (stacks == null)
            return null;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i] != null && stacks[i].item != null)
                return stacks[i].item;
        }
        return null;
    }
}
