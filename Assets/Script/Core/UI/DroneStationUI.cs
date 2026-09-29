using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Окно станций дронов (E по станции). Загрузка: куда летать, дроны и покупка за рубины, фильтр.
/// Выгрузка: заполненность склада, что лежит, сколько станций привязано и дронов в пути.
/// </summary>
public class DroneStationUI : MonoBehaviour
{
    public static DroneStationUI Instance { get; private set; }
    public bool IsOpen { get; private set; }

    VisualElement overlay;
    VisualElement body;
    Label status;
    Label droneLine;
    Button buyButton;
    VisualElement targetList;
    VisualElement filterGrid;
    VisualElement storageList;
    TextField filterSearch;

    DroneLoadStation load;
    DroneUnloadStation unload;
    float nextRefresh;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (overlay != null)
            return;
        Build();
        IndustryUi.Show(overlay, false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void OpenFor(BuildingBase station)
    {
        if (Instance == null || station == null)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        Instance.Open(station);
    }

    void Open(BuildingBase station)
    {
        if (overlay == null)
            Build();
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            MachineUI.Instance.Close();
        load = station as DroneLoadStation;
        unload = station as DroneUnloadStation;
        IndustryUi.SetHeader(overlay, station.data != null ? station.data.displayName : "Drones", station.data != null ? station.data.icon : null);
        IsOpen = true;
        IndustryUi.Show(overlay, true);
        UiAudio.PlayOpen();
        Fill();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        load = null;
        unload = null;
        IndustryUi.Show(overlay, false);
        KeybindStore.SuppressGameplay();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Update()
    {
        if (!IsOpen)
            return;
        BuildingBase b = load != null ? load : unload;
        if (b == null || !b.IsPlaced)
        {
            Close();
            return;
        }

        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.3f;
        RefreshLive();
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 87);
        overlay = IndustryUi.OverlayPanel("Drones", null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        if (panel != null)
        {
            panel.RemoveFromClassList("panel-wide");
            panel.style.width = 820;
            panel.style.maxWidth = Length.Percent(94);
            panel.style.height = Length.Percent(80);
            panel.style.alignSelf = Align.Center;
            panel.style.marginTop = 40;
        }

        VisualElement host = overlay.Q("Body") ?? panel;
        status = IndustryUi.Text("Status", "", "body-text");
        status.style.whiteSpace = WhiteSpace.Normal;
        host.Add(status);
        var scroll = IndustryUi.Scroll("DroneScroll");
        scroll.AddToClassList("grow");
        body = IndustryUi.El("DroneBody", "col");
        scroll.Add(body);
        host.Add(scroll);
        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);
    }

    void Fill()
    {
        body.Clear();
        droneLine = null;
        buyButton = null;
        targetList = null;
        filterGrid = null;
        storageList = null;
        if (load != null)
            FillLoad();
        else if (unload != null)
            FillUnload();
        RefreshLive();
    }

    static Label Section(VisualElement parent, string key)
    {
        Label l = IndustryUi.Text("S", UiLocale.T(key), "settings-group");
        l.style.marginTop = 14;
        parent.Add(l);
        return l;
    }

    // ---------- Загрузка ----------

    void FillLoad()
    {
        Section(body, "drone.sec_target");
        targetList = IndustryUi.El("Targets", "col");
        body.Add(targetList);
        RebuildTargets();

        Section(body, "drone.sec_drones");
        var row = IndustryUi.El("DroneRow", "row");
        row.style.alignItems = Align.Center;
        droneLine = IndustryUi.Text("DroneLine", "", "body-text", "grow");
        droneLine.style.whiteSpace = WhiteSpace.Normal;
        buyButton = IndustryUi.Btn("", () =>
        {
            if (load != null && load.TryBuyDrone())
                UiAudio.PlayConfirm();
            else
                UiAudio.PlayError();
            RefreshLive();
        }, "btn-small", "btn-primary");
        row.Add(droneLine);
        row.Add(buyButton);
        body.Add(row);

        Section(body, "drone.sec_filter");
        filterSearch = new TextField { name = "DroneFilterSearch" };
        filterSearch.AddToClassList("field");
        filterSearch.AddToClassList("search-field");
        if (filterSearch.textEdition != null)
            filterSearch.textEdition.placeholder = UiLocale.T("machine.search_filter");
        filterSearch.RegisterValueChangedCallback(_ => RebuildFilter());
        body.Add(filterSearch);
        filterGrid = IndustryUi.El("FilterGrid", "col");
        body.Add(filterGrid);
        RebuildFilter();
    }

    void RebuildTargets()
    {
        if (targetList == null || load == null)
            return;
        targetList.Clear();
        var stations = new List<DroneUnloadStation>();
        DroneNetwork.CollectUnloadStations(stations);
        if (stations.Count == 0)
        {
            targetList.Add(IndustryUi.Text("None", UiLocale.T("drone.no_unload"), "muted"));
            return;
        }

        stations.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - load.transform.position)
                .CompareTo(Vector3.SqrMagnitude(b.transform.position - load.transform.position)));
        DroneUnloadStation current = load.Target;
        for (int i = 0; i < stations.Count; i++)
        {
            DroneUnloadStation st = stations[i];
            float dist = Vector3.Distance(st.transform.position, load.transform.position);
            string sub = UiLocale.T("drone.target_sub", Mathf.RoundToInt(dist), DroneNetwork.CellText(st), st.Total, DroneNetwork.UnloadCapacity);
            bool on = st == current;
            targetList.Add(IndustryUi.FilterCard(
                st.data != null ? st.data.icon : null,
                (on ? "✓ " : "") + (st.data != null ? st.data.displayName : "Unload"),
                on ? UiLocale.T("drone.target_on") + "  ·  " + sub : sub,
                on,
                () =>
                {
                    if (load == null)
                        return;
                    load.SetTarget(on ? null : st);
                    RebuildTargets();
                    RefreshLive();
                }));
        }
    }

    void RebuildFilter()
    {
        if (filterGrid == null || load == null)
            return;
        filterGrid.Clear();
        string q = filterSearch != null ? (filterSearch.value ?? "").Trim().ToLowerInvariant() : "";
        filterGrid.Add(IndustryUi.FilterCard(
            null,
            UiLocale.T("drone.filter_any"),
            UiLocale.T("drone.filter_any_sub"),
            load.filter == null,
            () =>
            {
                load.SetFilter(null);
                RebuildFilter();
            }));

        var seen = new HashSet<string>();
        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null || item.isFluid || string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                continue;
            if (q.Length > 0
                && (item.displayName ?? "").ToLowerInvariant().IndexOf(q) < 0
                && item.id.ToLowerInvariant().IndexOf(q) < 0)
                continue;
            ItemData captured = item;
            bool selected = load.filter == item;
            filterGrid.Add(IndustryUi.FilterCard(
                item.icon,
                item.displayName,
                selected ? UiLocale.T("machine.filter_set") : UiLocale.T("machine.filter_only"),
                selected,
                () =>
                {
                    load.SetFilter(captured);
                    RebuildFilter();
                }));
        }
    }

    // ---------- Выгрузка ----------

    void FillUnload()
    {
        Section(body, "drone.sec_storage");
        storageList = IndustryUi.El("Storage", "col");
        body.Add(storageList);
    }

    void RebuildStorage()
    {
        if (storageList == null || unload == null)
            return;
        storageList.Clear();
        if (unload.Total <= 0)
        {
            storageList.Add(IndustryUi.Text("Empty", UiLocale.T("drone.storage_empty"), "muted"));
            return;
        }

        foreach (var pair in unload.Storage)
        {
            if (pair.Key == null || pair.Value <= 0)
                continue;
            storageList.Add(IndustryUi.StatRow(pair.Key.icon, pair.Key.displayName, pair.Value.ToString()));
        }
    }

    // ---------- Живые цифры ----------

    void RefreshLive()
    {
        if (load != null)
        {
            string crate = load.CrateItem != null
                ? UiLocale.T("drone.crate", load.CrateItem.displayName, load.CrateCount, DroneNetwork.Capacity())
                : UiLocale.T("drone.crate_empty", DroneNetwork.Capacity());
            DroneUnloadStation t = load.Target;
            string to = t != null ? UiLocale.T("drone.to", DroneNetwork.CellText(t)) : UiLocale.T("drone.to_none");
            string broken = load.IsBroken ? "\n" + UiLocale.T("drone.broken") : "";
            status.text = crate + "\n" + UiLocale.T("drone.ready", load.ReadyCount, DroneNetwork.ReadyCrates)
                + "  ·  " + to + broken;

            if (droneLine != null)
            {
                int slots = DroneNetwork.Slots();
                droneLine.text = UiLocale.T("drone.drones", load.OwnedDrones, slots, load.FlyingCount(),
                    Mathf.RoundToInt(DroneNetwork.Speed()));
                if (load.OwnedDrones >= DroneNetwork.MaxDrones)
                {
                    IndustryUi.SetButtonLabel(buyButton, UiLocale.T("drone.buy_max"));
                    buyButton.SetEnabled(false);
                }
                else if (load.OwnedDrones >= slots)
                {
                    IndustryUi.SetButtonLabel(buyButton, UiLocale.T("drone.buy_locked"));
                    buyButton.SetEnabled(false);
                }
                else
                {
                    int price = load.NextDronePrice;
                    int have = PlayerWallet.Instance != null ? PlayerWallet.Instance.Rubies : 0;
                    IndustryUi.SetButtonLabel(buyButton, UiLocale.T("drone.buy", price));
                    buyButton.SetEnabled(have >= price);
                }
            }
        }
        else if (unload != null)
        {
            string broken = unload.IsBroken ? "\n" + UiLocale.T("drone.broken") : "";
            status.text = UiLocale.T("drone.unload_status", unload.Total, DroneNetwork.UnloadCapacity,
                unload.LinkedStations(), unload.IncomingDrones()) + broken;
            RebuildStorage();
        }
    }
}
