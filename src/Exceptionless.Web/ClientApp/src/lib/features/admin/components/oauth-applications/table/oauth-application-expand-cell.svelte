<script lang="ts">
    import type { Row, StockFeatures } from '@tanstack/svelte-table';

    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    import type { OAuthApplication } from '#features/admin/models.ts';

    import { Button } from '#comp/ui/button/index.ts';

    let { row }: { row: Row<StockFeatures, OAuthApplication> } = $props();
</script>

<Button
    variant="ghost"
    size="icon-sm"
    aria-label={`${row.getIsExpanded() ? 'Hide' : 'Show'} details for ${row.original.name}`}
    aria-expanded={row.getIsExpanded()}
    aria-controls={`oauth-application-details-${row.original.id}`}
    onclick={() => row.toggleExpanded()}
>
    {#if row.getIsExpanded()}
        <ChevronDown aria-hidden="true" />
    {:else}
        <ChevronRight aria-hidden="true" />
    {/if}
</Button>
