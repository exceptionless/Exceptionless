---
name: Storybook
description: |
  Component stories using Storybook with Svelte CSF. Story patterns, defineMeta, argTypes,
  snippet-based customization, and visual testing.
  Keywords: storybook, stories.svelte, defineMeta, Story, args, argTypes, autodocs
---

# Storybook

> **Documentation:** [storybook.js.org](https://storybook.js.org/docs/svelte)

## Running Storybook

```bash
npm run storybook
```

## File Location

Co-locate stories with components as `*.stories.svelte`.

## Basic Story Pattern

From [stack-status-badge.stories.svelte](src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-status-badge.stories.svelte):

```svelte
<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import { StackStatus } from '../models';
    import StackStatusBadge from './stack-status-badge.svelte';

    const { Story } = defineMeta({
        argTypes: {
            status: {
                control: { type: 'select' },
                options: [StackStatus.Open, StackStatus.Fixed, StackStatus.Regressed]
            }
        },
        component: StackStatusBadge,
        tags: ['autodocs'],
        title: 'Components/Stacks/StackStatusBadge'
    });
</script>

<Story name="Open" args={{ status: StackStatus.Open }} />
<Story name="Fixed" args={{ status: StackStatus.Fixed }} />
<Story name="Regressed" args={{ status: StackStatus.Regressed }} />
```

## Story with Snippets

From [notification.stories.svelte](src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/notification/notification.stories.svelte):

```svelte
<script module lang="ts">
    import { Button } from '$comp/ui/button';
    import CheckCircle from '@lucide/svelte/icons/check-circle';
    import Ban from '@lucide/svelte/icons/ban';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import NotificationDescription from './notification-description.svelte';
    import NotificationTitle from './notification-title.svelte';
    import Notification from './notification.svelte';

    const { Story } = defineMeta({
        component: Notification,
        tags: ['autodocs'],
        title: 'Components/Shared/Notification'
    });
</script>

<Story name="Success">
    <Notification variant="success">
        {#snippet icon()}<CheckCircle />{/snippet}
        <NotificationTitle>Operation completed successfully!</NotificationTitle>
        <NotificationDescription>Your changes have been saved.</NotificationDescription>
    </Notification>
</Story>

<Story name="Destructive">
    <Notification variant="destructive">
        {#snippet icon()}<Ban />{/snippet}
        <NotificationTitle>Something went wrong</NotificationTitle>
        <NotificationDescription>An error occurred. Please try again.</NotificationDescription>
    </Notification>
</Story>

<Story name="With Action">
    <Notification variant="information">
        {#snippet action()}
            <Button variant="outline" size="sm">Take Action</Button>
        {/snippet}
        <NotificationTitle>Action required</NotificationTitle>
        <NotificationDescription>Click the button to proceed.</NotificationDescription>
    </Notification>
</Story>
```

## Key Patterns

- **`defineMeta`**: Returns `Story` component, configure `component`, `title`, `tags`
- **`tags: ['autodocs']`**: Auto-generate documentation page
- **`argTypes`**: Configure controls for props (select, text, boolean, etc.)
- **Simple args**: `<Story name="Open" args={{ status: 'open' }} />`
- **Custom content**: Use children with snippets for complex compositions
- **Title hierarchy**: Use `/` for organization (e.g., `Components/Shared/Notification`)
