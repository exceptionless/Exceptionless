<script lang="ts">
    import Badge from '$comp/ui/badge/badge.svelte';
    import * as Tooltip from '$comp/ui/tooltip';
    import { getSuspensionDescription } from '$features/organizations/suspension-utils';

    interface Props {
        code?: null | string;
        notes?: null | string;
    }

    let { code, notes }: Props = $props();

    function getLabel(code: null | string | undefined): string {
        switch (code) {
            case 'Abuse':
                return 'Abuse';
            case 'Billing':
                return 'Billing';
            case 'Other':
                return 'Other';
            case 'Overage':
                return 'Overage';
            default:
                return 'Suspended';
        }
    }

    function getVariant(code: null | string | undefined): 'amber' | 'orange' | 'red' {
        switch (code) {
            case 'Billing':
                return 'amber';
            case 'Overage':
                return 'orange';
            default:
                return 'red';
        }
    }

    const variant = $derived(getVariant(code));
</script>

<Tooltip.Root>
    <Tooltip.Trigger>
        {#snippet child({ props })}
            <Badge {...props} {variant} class="cursor-help">
                {getLabel(code)}
            </Badge>
        {/snippet}
    </Tooltip.Trigger>
    <Tooltip.Content>
        {getSuspensionDescription(code, notes)}
    </Tooltip.Content>
</Tooltip.Root>
