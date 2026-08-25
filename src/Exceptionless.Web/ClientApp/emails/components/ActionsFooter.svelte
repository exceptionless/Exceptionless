<script module lang="ts">
    import { Heading, Section } from '@better-svelte-email/components';

    export const actionsFooterStyles = `
[data-email-actions]>tbody>tr>td{padding:0 16px 16px}
[data-email-actions] li,[data-email-actions] a{line-height:1.3}
`;
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    let { actions, summaryBlocked = '' }: { actions: Snippet; summaryBlocked?: string } = $props();
</script>

<Section data-email-actions data-summary-blocked={summaryBlocked} class="px-4 py-2">
    <Heading as="h5" class="text-muted mt-0 mb-[5px] text-[20px] leading-[1.3] font-normal">Other Actions</Heading>
    <ul style="margin-top:0">
        {@render actions()}
    </ul>
</Section>
