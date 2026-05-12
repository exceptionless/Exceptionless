# Exceptionless Design System (Source of Truth)

This document is the canonical design reference for the Svelte `ClientApp` implementation.

## Scope and ownership
- Applies to: `src/Exceptionless.Web/ClientApp`
- Runtime source of truth: `src/Exceptionless.Web/ClientApp/src/app.css`
- Implementation components consume theme tokens via shared Tailwind utility classes in `src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/ui/*`

If anything below changes, update `app.css` first, then this file.

## Design intent
- Clean, readable, low-noise UI for debugging and operational workflows.
- Neutral surfaces with blue-green accents for navigation and hierarchy.
- Strong contrast in both light and dark modes.
- Token-driven styling over hard-coded component colors (except explicit local UI preview examples).

## 1) Theme tokens (single source)

### Light mode (`:root`)

- `--background`: `hsl(0 0% 100%)`
- `--foreground`: `hsl(221 39% 11%)`
- `--muted`: `hsl(210 20% 98%)`
- `--muted-foreground`: `hsl(240 3.8% 46.1%)`
- `--popover`: `hsl(0 0% 100%)`
- `--popover-foreground`: `hsl(221 39% 11%)`
- `--card`: `hsl(0 0% 100%)`
- `--card-foreground`: `hsl(221 39% 11%)`
- `--border`: `hsl(220 13% 91%)`
- `--input`: `hsl(220 13% 91%)`
- `--primary`: `hsl(96 64% 46%)`
- `--primary-foreground`: `hsl(0 0% 10%)`
- `--secondary`: `hsl(210 20% 98%)`
- `--secondary-foreground`: `hsl(240 5.9% 10%)`
- `--accent`: `hsl(220 14.29% 95.88%)`
- `--accent-foreground`: `hsl(216.92 19.12% 26.67%)`
- `--destructive`: `hsl(0 72.2% 50.6%)`
- `--destructive-foreground`: `hsl(0 0% 100%)`
- `--ring`: `hsl(221 39% 11%)`
- `--radius`: `0.375rem`
- `--sidebar`: `var(--background)`
- `--sidebar-foreground`: `var(--foreground)`
- `--sidebar-primary`: `var(--primary)`
- `--sidebar-primary-foreground`: `var(--primary-foreground)`
- `--sidebar-accent`: `var(--accent)`
- `--sidebar-accent-foreground`: `var(--accent-foreground)`
- `--sidebar-border`: `var(--border)`
- `--sidebar-ring`: `var(--ring)`
- `--chart-1`: `#7bb662`
- `--chart-2`: `#56b4e9`
- `--chart-3`: `#d47a00`
- `--chart-4`: `#ffd64d`
- `--chart-5`: `#d9d9d9`
- `--chart-6`: `#c62828`

`@theme inline` maps these into Tailwind-compatible tokens:
- `--font-sans`: `'Inter Variable', ui-sans-serif, system-ui, sans-serif, ...`
- `--font-mono`: `'Source Code Pro Variable', ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace`
- `--radius-sm`: `calc(var(--radius) - 4px)`
- `--radius-md`: `calc(var(--radius) - 2px)`
- `--radius-lg`: `var(--radius)`
- `--radius-xl`: `calc(var(--radius) + 4px)`
- `--color-*` mappings for background/foreground/border/card/input/primary/secondary/accent/destructive/ring

### Dark mode (`.dark`)
- `--background`: `hsl(220 60% 1.96%)`
- `--foreground`: `hsl(0 0% 100%)`
- `--muted`: `hsl(210 16.13% 12.16%)`
- `--muted-foreground`: `hsl(207.69 35.14% 92.75%)`
- `--popover`: `hsl(216 27.78% 7.06%)`
- `--popover-foreground`: `hsl(207.69 35.14% 92.75%)`
- `--card`: `hsl(216 27.78% 7.06%)`
- `--card-foreground`: `hsl(210 40% 98%)`
- `--border`: `hsl(215 14.63% 16.08%)`
- `--input`: `hsl(215 12.24% 19.22%)`
- `--primary`: `hsl(96 64.1% 45.88%)`
- `--primary-foreground`: `hsl(0 0% 10%)`
- `--secondary`: `hsl(215 15.38% 15.29%)`
- `--secondary-foreground`: `hsl(0 0% 97.25%)`
- `--accent`: `hsl(210 16.13% 12.16%)`
- `--accent-foreground`: `hsl(207.69 35.14% 92.75%)`
- `--destructive`: `hsl(359.59 67.74% 42.55%)`
- `--destructive-foreground`: `hsl(0 0% 100%)`
- `--ring`: `hsl(96 64.1% 45.88%)`
- Sidebar variables aliasing to base dark values
- `--chart-1`: `#a4d56f`
- `--chart-2`: `#8fdbff`
- `--chart-3`: `#ff9e3d`
- `--chart-4`: `#ffea70`
- `--chart-5`: `#5a5a5a`
- `--chart-6`: `#ff5c5c`

## 2) Typography and global style behavior
- Global body: `bg-background text-foreground`.
- Font stacks are defined in `app.css` with `Inter Variable` (sans) and `Source Code Pro Variable` (mono stack).
- Spacing and sizing are not hardcoded globally; components define heights and spacing through shared primitives.

## 3) Baseline global components (from shared UI layer)

### Buttons (`lib/features/shared/components/ui/button/button.svelte`)
- Base: `rounded-lg`, `border`, transition, `focus-visible:ring-3`, `disabled:opacity-50`
- Default variant: `bg-primary text-primary-foreground [a]:hover:bg-primary/80`
- Outline variant: `border-border bg-background hover:bg-muted hover:text-foreground ...`
- Secondary variant: `bg-secondary text-secondary-foreground`
- Ghost variant: `hover:bg-muted hover:text-foreground`
- Destructive variant: destructive foreground/ring treatment with translucent background
- Link variant: `text-primary underline-offset-4 hover:underline`
- Size tokens:
  - `default`: height `h-8`, padding `px-2.5`
  - `xs`: `h-6`
  - `sm`: `h-7`
  - `lg`: `h-9`
  - `xl`: `h-10`

### Inputs (`ui/input/input.svelte`)
- Base: `h-8`, `rounded-lg`, `border-input`, `focus-visible:border-ring`
- `aria-invalid` states use `destructive` ring/border
- Disabled state includes `disabled:bg-input/50` and reduced pointer interactivity

### Textarea (`ui/textarea/textarea.svelte`)
- Base: `rounded-lg`, `border-input`, `min-h-16`, focus ring same as input

### Card (`ui/card/card.svelte`)
- Base container: `bg-card text-card-foreground rounded-xl` with ring-based border style
- Supports sm size variant and internal spacing via `gap` and padding

### Badge (`ui/badge/badge.svelte`)
- Base: `rounded-4xl` and semantic variants (`default`, `secondary`, `destructive`, `outline`, `ghost`, `link`)
- Additional semantic variants include `red`, `amber`, `orange` classes (currently utility-driven)

## 4) Behavior / structure notes
- Theme variants are attached by CSS class `dark` at root.
- Global helpers for `body`, border reset, and range selection brush styles are in `app.css`.
- `theme-preview.svelte` currently uses direct HSL swatch classes for visual preview blocks (this is a UI demo file, not the token definition).

## 5) App implementation contract
- The following files must stay aligned:
  - `src/Exceptionless.Web/ClientApp/src/app.css`
  - `src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/ui/*`
  - `src/Exceptionless.Web/ClientApp/src/routes/(app)/account/appearance/(components)/theme-preview.svelte`
- Do not treat `DESIGN.md` as a target to force-fit the implementation; it documents current implementation state.
- Any request to change actual look-and-feel starts in `app.css` and shared component classes; then update this doc in the same change.

## 6) Change log
- `2026-05-11` — Reworked to make root `DESIGN.md` authoritative description of current `ClientApp` tokenized implementation.
- `2026-05-11` — Restored `app.css` and `theme-preview.svelte` implementation values and documented as-is.
