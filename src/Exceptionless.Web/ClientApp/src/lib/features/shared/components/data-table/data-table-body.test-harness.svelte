<script lang="ts">
    import type { EventSummaryModel, StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$features/events/components/summary';

    import { buildEventDetailsHref, buildStackDetailsHref } from '$features/events/components/summary';
    import Summary from '$features/events/components/summary/summary.svelte';
    import { getSharedTableOptions } from '$features/shared/table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { createTable, renderComponent } from '@tanstack/svelte-table';

    import DataTableBody from './data-table-body.svelte';

    type TestSummary = EventSummaryModel<'event-error-summary'> | StackSummaryModel<'stack-error-summary'>;

    interface Props {
        allColumnsSized?: boolean;
        fullWidthSummary?: boolean;
        kind: 'event' | 'stack';
        onRowClick: (row: TestSummary) => void;
    }

    let { allColumnsSized = false, fullWidthSummary = false, kind, onRowClick }: Props = $props();

    const summaryData = {
        Message: 'Unexpected end of Stream, the content may have already been read by another component.',
        Method: 'MoveNext',
        Type: 'IOException'
    };
    const eventSummary: EventSummaryModel<'event-error-summary'> = {
        data: summaryData,
        date: '2026-07-28T00:00:00Z',
        id: 'event-id',
        project_id: 'project-id',
        tags: [],
        template_key: 'event-error-summary'
    };
    const stackSummary: StackSummaryModel<'stack-error-summary'> = {
        data: summaryData,
        first_occurrence: '2026-07-28T00:00:00Z',
        id: 'stack-id',
        last_occurrence: '2026-07-28T00:00:00Z',
        project_id: 'project-id',
        status: StackStatus.Open,
        tags: [],
        template_key: 'stack-error-summary',
        title: 'Unexpected end of Stream, the content may have already been read by another component.',
        total: 1,
        total_users: 1,
        users: 1
    };
    const summary: TestSummary = $derived(kind === 'event' ? eventSummary : stackSummary);
    const queryParameters = { limit: 20, page: 1 };
    const table = createTable(
        getSharedTableOptions<TestSummary, 'memory'>({
            columnPersistenceKey: 'row-navigation-test',
            columns: [
                {
                    cell: 'Select row',
                    enableResizing: false,
                    header: 'Select',
                    id: 'select'
                },
                {
                    cell: (props) => renderComponent(Summary, { showStatus: false, summary: props.row.original }),
                    header: 'Summary',
                    id: 'summary',
                    meta: {
                        get class() {
                            return fullWidthSummary ? 'w-full' : 'w-60 min-w-60 max-w-60';
                        }
                    },
                    minSize: 120,
                    size: 160
                },
                {
                    cell: (props) => props.row.original.id,
                    header: 'Date',
                    id: 'date'
                }
            ],
            get defaultColumnSizing() {
                return allColumnsSized ? { date: 130, summary: 140 } : undefined;
            },
            enableColumnResizing: true,
            paginationStrategy: 'memory',
            get queryData() {
                return [summary];
            },
            queryParameters
        })
    );
</script>

<DataTableBody
    rowClick={onRowClick}
    rowHref={(row: SummaryModel<SummaryTemplateKeys>) => (kind === 'event' ? buildEventDetailsHref(row.id) : buildStackDetailsHref(row.id))}
    {table}
/>
