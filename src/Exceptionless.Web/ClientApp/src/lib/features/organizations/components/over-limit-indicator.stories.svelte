<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import OverLimitIndicator from './over-limit-indicator.svelte';

    const { Story } = defineMeta({
        argTypes: {
            isOverLimit: {
                control: { type: 'boolean' },
                defaultValue: true
            }
        },
        component: OverLimitIndicator,
        tags: ['autodocs'],
        title: 'Components/Organizations/OverLimitIndicator'
    });
</script>

<Story name="Over Limit" args={{ isOverLimit: true }} />
<Story name="Not Over Limit" args={{ isOverLimit: false }} />
