# Forms & Inputs

## Contents

- Forms use Field.FieldGroup + Field.Field
- InputGroup requires InputGroup.Input/InputGroup.Textarea
- Buttons inside inputs use InputGroup.Root + InputGroup.Addon
- Option sets use installed form controls
- Field.FieldSet + Field.FieldLegend for grouping related fields
- Field validation and disabled states

---

## Forms use Field.FieldGroup + Field.Field

Always use `Field.FieldGroup` + `Field.Field` — never raw `div` with `space-y-*`:

```svelte
<script lang="ts">
  import * as Field from "$comp/ui/field";
  import { Input } from "$comp/ui/input";
</script>

<Field.FieldGroup>
  <Field.Field>
    <Field.Label for="email">Email</Field.Label>
    <Input id="email" type="email" />
  </Field.Field>
  <Field.Field>
    <Field.Label for="password">Password</Field.Label>
    <Input id="password" type="password" />
  </Field.Field>
</Field.FieldGroup>
```

Use `Field` with `orientation="horizontal"` for settings pages. Use `Field.Label` with `class="sr-only"` for visually hidden labels.

**Choosing form controls:**

- Simple text input → `Input`
- Dropdown with predefined options → `Select`
- Searchable dropdown → add `Combobox` intentionally before importing it, or compose installed `Command` + `Popover`
- Native HTML select (no JS) → add `native-select` intentionally before importing it
- Boolean toggle → `Switch` (for settings) or `Checkbox` (for forms)
- Single choice from few options → `RadioGroup`
- Toggle between 2–5 options → use `RadioGroup`, `Select`, or intentionally add `ToggleGroup`
- OTP/verification code → add `InputOTP` intentionally before importing it
- Multi-line text → `Textarea`

---

## InputGroup requires InputGroup.Input/InputGroup.Textarea

Never use raw `Input` or `Textarea` inside an `InputGroup.Root`.

**Incorrect:**

```svelte
<script lang="ts">
  import * as InputGroup from "$comp/ui/input-group";
  import { Input } from "$comp/ui/input";
</script>

<InputGroup.Root>
  <Input placeholder="Search..." />
</InputGroup.Root>
```

**Correct:**

```svelte
<script lang="ts">
  import * as InputGroup from "$comp/ui/input-group";
</script>

<InputGroup.Root>
  <InputGroup.Input placeholder="Search..." />
</InputGroup.Root>
```

---

## Buttons inside inputs use InputGroup.Root + InputGroup.Addon

Never place a `Button` directly inside or adjacent to an `Input` with custom positioning.

**Incorrect:**

```svelte
<script lang="ts">
  import { Input } from "$comp/ui/input";
  import { Button } from "$comp/ui/button";
  import SearchIcon from "@lucide/svelte/icons/search";
</script>

<div class="relative">
  <Input placeholder="Search..." class="pr-10" />
  <Button class="absolute top-0 right-0" size="icon">
    <SearchIcon />
  </Button>
</div>
```

**Correct:**

```svelte
<script lang="ts">
  import * as InputGroup from "$comp/ui/input-group";
  import { Button } from "$comp/ui/button";
  import SearchIcon from "@lucide/svelte/icons/search";
</script>

<InputGroup.Root>
  <InputGroup.Input placeholder="Search..." />
  <InputGroup.Addon>
    <Button size="icon">
      <SearchIcon data-icon="inline-start" />
    </Button>
  </InputGroup.Addon>
</InputGroup.Root>
```

---

## Option sets use installed form controls

Don't manually loop `Button` components with active state. Use `RadioGroup` or `Select` when they fit. If `ToggleGroup` is the right control, add it with the CLI before importing it.

**Incorrect:**

```svelte
<script lang="ts">
  import { Button } from "$comp/ui/button";
  let selected = $state("daily");
</script>

<div class="flex gap-2">
  {#each ["daily", "weekly", "monthly"] as option (option)}
    <Button
      variant={selected === option ? "default" : "outline"}
      onclick={() => (selected = option)}
    >
      {option}
    </Button>
  {/each}
</div>
```

**Correct after intentionally adding `toggle-group`:**

```svelte
<script lang="ts">
  import * as ToggleGroup from "$comp/ui/toggle-group";
  let selected = $state("daily");
</script>

<ToggleGroup.Root bind:value={selected} spacing={2}>
  <ToggleGroup.Item value="daily">Daily</ToggleGroup.Item>
  <ToggleGroup.Item value="weekly">Weekly</ToggleGroup.Item>
  <ToggleGroup.Item value="monthly">Monthly</ToggleGroup.Item>
</ToggleGroup.Root>
```

Combine with `Field` for labelled toggle groups:

```svelte
<script lang="ts">
  import * as Field from "$comp/ui/field";
  import * as ToggleGroup from "$comp/ui/toggle-group";
</script>

<Field.Field orientation="horizontal">
  <Field.FieldTitle id="theme-label">Theme</Field.FieldTitle>
  <ToggleGroup.Root aria-labelledby="theme-label" spacing={2}>
    <ToggleGroup.Item value="light">Light</ToggleGroup.Item>
    <ToggleGroup.Item value="dark">Dark</ToggleGroup.Item>
    <ToggleGroup.Item value="system">System</ToggleGroup.Item>
  </ToggleGroup.Root>
</Field.Field>
```

---

## Field.FieldSet + Field.FieldLegend for grouping related fields

Use `Field.FieldSet` + `Field.FieldLegend` for related checkboxes, radios, or switches — not `div` with a heading:

```svelte
<script lang="ts">
  import * as Field from "$comp/ui/field";
  import { Checkbox } from "$comp/ui/checkbox";
</script>

<Field.FieldSet>
  <Field.FieldLegend variant="label">Preferences</Field.FieldLegend>
  <Field.FieldDescription>Select all that apply.</Field.FieldDescription>
  <Field.FieldGroup class="gap-3">
    <Field.Field orientation="horizontal">
      <Checkbox id="dark" />
      <Field.Label for="dark" class="font-normal"
        >Dark mode</Field.Label
      >
    </Field.Field>
  </Field.FieldGroup>
</Field.FieldSet>
```

---

## Field validation and disabled states

Both attributes are needed — `data-invalid`/`data-disabled` styles the field (label, description), while `aria-invalid`/`disabled` styles the control.

```svelte
<script lang="ts">
  import * as Field from "$comp/ui/field";
  import { Input } from "$comp/ui/input";
</script>

<!-- Invalid. -->
<Field.Field data-invalid>
  <Field.Label for="email">Email</Field.Label>
  <Input id="email" aria-invalid />
  <Field.FieldDescription>Invalid email address.</Field.FieldDescription>
</Field.Field>

<!-- Disabled. -->
<Field.Field data-disabled>
  <Field.Label for="email">Email</Field.Label>
  <Input id="email" disabled />
</Field.Field>
```

Works for installed controls such as `Input`, `Textarea`, `Select`, `Checkbox`, `RadioGroupItem`, and `Switch`; for `Slider`, `NativeSelect`, or `InputOTP`, add the component before importing it.
