<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import SuspensionIndicator from './suspension-indicator.svelte';

    const { Story } = defineMeta({
        argTypes: {
            code: {
                control: { type: 'select' },
                options: ['Billing', 'Overage', 'Abuse', 'Other', undefined]
            },
            notes: {
                control: { type: 'text' }
            }
        },
        component: SuspensionIndicator,
        tags: ['autodocs'],
        title: 'Components/Organizations/SuspensionIndicator'
    });
</script>

<Story name="Billing" args={{ code: 'Billing' }} />
<Story name="Overage" args={{ code: 'Overage' }} />
<Story name="Abuse" args={{ code: 'Abuse' }} />
<Story name="Other" args={{ code: 'Other' }} />
<Story name="Unknown" args={{ code: undefined }} />
<Story name="With Notes" args={{ code: 'Billing', notes: 'Account payment failed on 2024-01-15. Customer contacted.' }} />
