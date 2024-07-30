<script lang="ts">
    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    interface Props {
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<SummaryTemplateKeys>);
    let percentage = $derived(source.total_users > 0 ? (source.users / source.total_users) * 100.0 : 0);
</script>

<abbr title={`${source.users} of ${source.total_users} users`}>
    {percentage.toFixed()}%
</abbr>
