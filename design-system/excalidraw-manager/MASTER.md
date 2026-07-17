# Excalidraw Manager UI System

## Visual thesis

A quiet native workspace for people who think in drawings: Finder-like structure,
Linear-like restraint, and the product's graphite-card / teal-dot icon as its memorable mark.
The interface should feel like a tool already built into the computer, not a dashboard
or a marketing page.

The automated design database correctly classified the product as a developer/productivity
tool, but its generated newsletter layout and exaggerated typography were rejected as an
irrelevant match. This file is the reviewed source of truth.

## Content plan

1. Sidebar: product identity, workspace tree, live services, settings.
2. Toolbar: create/open utilities only; no marketing copy.
3. Working surface: directory contents or one selected board's essential actions.
4. Status bar: platform, current operation, version.

```text
┌─ 244px sidebar ─────────┬─ compact toolbar ─────────────────────────────┐
│ ◇ Excalidraw Manager   │ location                         utilities     │
│                         ├────────────────────────────────────────────────┤
│ WORKSPACES              │ name                                           │
│ ▾ Product               │ /local/path                                    │
│   ├ Board A             │ ────────────────────────────────────────────── │
│   └ Research            │ file rows OR selected-board actions            │
│                         │                                                │
│ RUNNING                 │                                                │
│ ● Formula        :17840 │                                                │
│ ● Board A         :6417 │                                                │
│                         ├────────────────────────────────────────────────┤
│ ⚙ Settings              │ macOS arm64       Ready              v0.5.0    │
└─────────────────────────┴────────────────────────────────────────────────┘
```

## Interaction thesis

- Selection changes use only a 140–180 ms color transition; nothing floats or bounces.
- Async actions show an inline spinner and explicit verb such as “正在启动”.
- Hover clarifies click targets; keyboard focus uses a visible two-pixel accent ring.
- Respect `prefers-reduced-motion`; the product remains fully usable without animation.

## Tokens

### Light

| Role | Value |
| --- | --- |
| Canvas | `#f7f7f5` |
| Sidebar | `#efefec` |
| Working surface | `#fbfbfa` |
| Raised control | `#ffffff` |
| Primary text | `#1b1b19` |
| Secondary text | `#6f6f69` |
| Hairline | `#ddddD7` |
| Accent | `#0c8277` |
| Accent wash | `#ddf4f0` |
| Running | `#23856d` |
| Destructive | `#b83d49` |

### Dark

| Role | Value |
| --- | --- |
| Canvas | `#181817` |
| Sidebar | `#1e1e1c` |
| Working surface | `#222220` |
| Raised control | `#292927` |
| Primary text | `#f1f1ed` |
| Secondary text | `#a2a29b` |
| Hairline | `#393936` |
| Accent | `#43cbbb` |
| Accent wash | `#143c37` |
| Running | `#5bc4a7` |
| Destructive | `#ff8b94` |

## Type and spacing

- UI: `-apple-system`, `BlinkMacSystemFont`, `Segoe UI`, sans-serif. Native system type is
  intentional for a cross-platform desktop utility.
- Paths, ports, versions: `SFMono-Regular`, `Cascadia Code`, monospace.
- Base 14px; labels 11px; page title 24–28px. No display-sized typography.
- Four-pixel spacing grid. Sidebar rows 32px, toolbar 48px, controls at least 36px.
- Radius 6px for rows, 8px for controls, 12px only for dialogs. No pill-shaped buttons.

## Component rules

- Use Lucide SVG icons from one set; never use emoji as interface icons.
- Cards are not a default container. Prefer whitespace, hairlines, lists, and definition rows.
- Buttons use flat fills or one-pixel borders. No gradients and no decorative shadows.
- Keep one primary action per context. Destructive actions remain secondary until requested.
- Empty states explain the next action in one sentence and provide that action immediately.
- All click targets expose a tooltip or visible label and remain keyboard accessible.

## Signature

The existing stacked-card application icon is the brand anchor. Its teal dot supplies the
selection, focus, running, and primary-action color. The icon's small purple marks are reserved
for the formula tool only; purple is never used as the main interface color.

## Do not use

- Dashboard card mosaics, hero art, glassmorphism, gradients, or oversized headings.
- Purple everywhere; it is an action/selection color only.
- Multiple accent colors competing for attention.
- Layout-shifting hover transforms, hidden focus rings, or body text below 12px.
