# Walk of Industry — UI system

UI Toolkit first. Gameplay systems stay the source of data; these files only present it.

## Design system

- Colors, spacing, type: `IndustryTokens.uss` (`--bg-0`, `--accent`, `--space-4`, …)
- Components and screens: `Industry.uss`
- Theme: `IndustryTheme.tss`
- Runtime copies live in `Assets/Resources/UI/` (same files). Edit both or copy after a USS change.

Palette is **dark industrial**: cold graphite surfaces, warm amber accent. Green / yellow / red / cyan / purple are semantic, never the only status cue.

Spacing scale: 4 8 12 16 20 24 32 40 48 64.

Type: Display 44, H1 32, H2 24, H3 20, Body 16, Small 14, Caption 13.

## Surfaces

Use `.panel-root` / `.panel-header` / `.panel-body`. Cards (`.card`) only for standalone objects (building, world save, research node). Stats use `.stat-row`.

States: `.is-selected` `.is-active` `.is-locked` `.is-error` `.is-complete` `.is-empty` `.is-drop-ok`.

## C# core

| Type | Role |
| --- | --- |
| `IndustryUi` | Mount, factory, money format, building category |
| `UiTheme` | Legacy uGUI colors only |
| `UiFactory` | (legacy helpers still on `IndustryUi`) |
| `UiMotion` | Panel fade |
| `UiTooltip` | Delayed hover tooltip, same panel as the control |
| `UiNotification` | Toasts (top overlay) |
| `UiModal` | Confirm destructive actions |
| `UiAudio` | Empty hooks: hover/click/open/close |
| `UiStatus` / `UiStatusUtil` | RUNNING / READY / LOCKED / … |
| `UiRuntime` | Overlay host, F8 debug flag |

## New screen

1. `IndustryUi.Mount(this, sortingOrder)`
2. Build with USS classes, not inline colors
3. Bind data from game systems
4. `IndustryUi.Show` for HUD, keep Open/Close/Refresh
5. Subscribe to existing events; do not refresh every `Update`

## Tooltip

```csharp
UiTooltip.Bind(element, "Title", "Description", "R");
```

## Notification

```csharp
UiNotification.Push("Research completed", node.displayName, UiStatus.Completed);
```

## Modal

```csharp
UiModal.Confirm("DELETE WORLD?", "Cannot be undone.", "DELETE", () => Delete());
```

## Animation

Prefer USS `transition-duration: 0.12s`. `UiMotion.ShowPanel` / `HidePanel` for full-screen menus.

## Debug

`F8` toggles `UiRuntime.DebugUi`.
