<script lang="ts">
    import type { SummaryModel, SummaryTemplateKeys, StackSummaryModel } from '$lib/models/api';

    export let summary: SummaryModel<SummaryTemplateKeys>;
    const source = summary as StackSummaryModel<SummaryTemplateKeys>;

    const percentage = source.total_users > 0 ? (source.users / source.total_users) * 100.0 : 0;
</script>

<abbr title={`${source.users} of ${source.total_users} users`}>
    {percentage.toFixed()}%
</abbr>
