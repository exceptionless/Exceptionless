<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import { StackStatus } from '../models';
    import StackStatusBadge from './stack-status-badge.svelte';

    const { Story } = defineMeta({
        argTypes: {
            status: {
                control: { type: 'select' },
                options: [StackStatus.Open, StackStatus.Fixed, StackStatus.Regressed, StackStatus.Snoozed, StackStatus.Ignored, StackStatus.Discarded]
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
<Story name="Snoozed" args={{ status: StackStatus.Snoozed }} />
<Story name="Ignored" args={{ status: StackStatus.Ignored }} />
<Story name="Discarded" args={{ status: StackStatus.Discarded }} />
