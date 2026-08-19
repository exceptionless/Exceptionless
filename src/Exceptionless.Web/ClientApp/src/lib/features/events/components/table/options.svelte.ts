import type { StackStatus } from '$features/stacks/models';

import NumberFormatter from '$comp/formatters/number.svelte';
import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { nameof } from '$lib/utils';
import { type ColumnDef, type ColumnVisibilityState, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import type { GetEventsMode } from '../../api.svelte';
import type { EventSummaryModel, StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '../summary/index';

import LogLevel from '../log-level.svelte';
import Summary from '../summary/summary.svelte';
import EventTagsSummaryCell from './event-tags-summary-cell.svelte';
import EventsUserIdentitySummaryCell from './events-user-identity-summary-cell.svelte';
import StackSortHeader from './stack-sort-header.svelte';
import StackStatusCell from './stack-status-cell.svelte';
import StackUsersSummaryCell from './stack-users-summary-cell.svelte';

export const defaultEventColumnVisibility: ColumnVisibilityState = {
    exception_type: false,
    level: false,
    message: false,
    name: false,
    project: false,
    source: false,
    tags: false,
    type: false,
    version: false
};

export const defaultStackColumnVisibility: ColumnVisibilityState = {
    project: false,
    tags: false
};

export type StackSortMode = Extract<GetEventsMode, 'stack_frequent' | 'stack_recent'>;

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
    mode: GetEventsMode = 'summary',
    options?: {
        onStackSort?: (mode: StackSortMode) => void;
        onTagClick?: (tag: string) => Promise<void> | void;
        showType?: boolean;
    }
): ColumnDef<StockFeatures, TSummaryModel, unknown>[] {
    const showType = options?.showType ?? true;
    const columns: ColumnDef<StockFeatures, TSummaryModel, unknown>[] = [
        {
            cell: (props) =>
                renderComponent(Checkbox, {
                    'aria-label': 'Select row',
                    checked: props.row.getIsSelected(),
                    class: 'translate-y-[2px]',
                    disabled: !props.row.getCanSelect(),
                    indeterminate: props.row.getIsSomeSelected(),
                    onCheckedChange: (checked: 'indeterminate' | boolean) =>
                        props.row.getToggleSelectedHandler()({
                            target: {
                                checked
                            }
                        })
                }),
            enableHiding: false,
            enableResizing: false,
            enableSorting: false,
            header: ({ table }) =>
                renderComponent(Checkbox, {
                    checked: table.getIsAllRowsSelected(),
                    indeterminate: table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected(),
                    onCheckedChange: (checked: 'indeterminate' | boolean) =>
                        table.getToggleAllRowsSelectedHandler()({
                            target: {
                                checked
                            }
                        })
                }),
            id: 'select',
            meta: {
                class: 'w-6'
            }
        },
        {
            cell: (prop) =>
                renderComponent(Summary, {
                    showStatus: false,
                    showType,
                    summary: prop.row.original
                }),
            enableResizing: true,
            header: 'Summary',
            id: 'summary',
            maxSize: 1200,
            meta: {
                class: 'w-full'
            },
            minSize: 240,
            size: 480
        },
        {
            accessorFn: (row) => getProject(row),
            cell: (prop) => formatTextColumn(prop.getValue()),
            enableSorting: false,
            header: 'Project',
            id: 'project',
            maxSize: 800,
            meta: {
                class: 'w-60 min-w-60 max-w-60'
            },
            minSize: 160,
            size: 240
        }
    ];

    const isEventSummary = mode === 'summary';
    if (isEventSummary) {
        columns.push(
            {
                cell: (prop) =>
                    renderComponent(EventsUserIdentitySummaryCell, {
                        summary: prop.row.original
                    }),
                header: 'User',
                id: 'user',
                maxSize: 480,
                meta: {
                    class: 'w-28'
                },
                minSize: 80,
                size: 112
            },
            {
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('date'),
                cell: (prop) =>
                    renderComponent(TimeAgo, {
                        value: prop.getValue<string>()
                    }),
                header: 'Date',
                id: 'date',
                maxSize: 480,
                meta: {
                    class: 'w-36'
                },
                minSize: 96,
                size: 144
            },
            {
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('tags'),
                cell: (prop) =>
                    renderComponent(EventTagsSummaryCell, {
                        onTagClick: options?.onTagClick,
                        tags: prop.getValue<string[]>()
                    }),
                enableSorting: false,
                header: 'Tags',
                id: 'tags',
                maxSize: 800,
                meta: {
                    class: 'w-52 min-w-52 max-w-52'
                },
                minSize: 120,
                size: 208
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Message'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                enableSorting: false,
                header: 'Message',
                id: 'message',
                maxSize: 800,
                meta: {
                    class: 'w-full'
                },
                minSize: 160,
                size: 320
            },
            {
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('type'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                header: 'Type',
                id: 'type',
                maxSize: 640,
                meta: {
                    class: 'w-28'
                },
                minSize: 80,
                size: 112
            },
            {
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('version'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                enableSorting: false,
                header: 'Version',
                id: 'version',
                maxSize: 640,
                meta: {
                    class: 'w-32'
                },
                minSize: 80,
                size: 128
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Type'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                header: 'Exception Type',
                id: 'exception_type',
                maxSize: 800,
                meta: {
                    class: 'w-36'
                },
                minSize: 112,
                size: 144
            },
            {
                accessorFn: (row) => getSource(row),
                cell: (prop) => formatTextColumn(prop.getValue()),
                header: 'Source',
                id: 'source',
                maxSize: 800,
                meta: {
                    class: 'w-40'
                },
                minSize: 112,
                size: 160
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Name'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                enableSorting: false,
                header: 'Name',
                id: 'name',
                maxSize: 800,
                meta: {
                    class: 'w-40'
                },
                minSize: 112,
                size: 160
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Level'),
                cell: (prop) =>
                    renderComponent(LogLevel, {
                        level: prop.getValue<string | undefined>()
                    }),
                header: 'Level',
                id: 'level',
                maxSize: 240,
                meta: {
                    class: 'w-[4.5rem] min-w-[4.5rem] max-w-[4.5rem] px-1 text-center'
                },
                minSize: 64,
                size: 72
            }
        );
    } else {
        columns.push(
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('tags'),
                cell: (prop) =>
                    renderComponent(EventTagsSummaryCell, {
                        onTagClick: options?.onTagClick,
                        tags: prop.getValue<string[]>()
                    }),
                enableSorting: false,
                header: 'Tags',
                id: 'tags',
                maxSize: 800,
                meta: {
                    class: 'w-52 min-w-52 max-w-52'
                },
                minSize: 120,
                size: 208
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('status'),
                cell: (prop) =>
                    renderComponent(StackStatusCell, {
                        value: prop.getValue<StackStatus>()
                    }),
                enableSorting: false,
                header: 'Status',
                id: 'status',
                maxSize: 480,
                meta: {
                    class: 'w-36'
                },
                minSize: 96,
                size: 144
            },
            {
                cell: (prop) =>
                    renderComponent(StackUsersSummaryCell, {
                        summary: prop.row.original
                    }),
                enableSorting: false,
                header: 'Users',
                id: 'users',
                maxSize: 320,
                meta: {
                    class: 'w-24'
                },
                minSize: 72,
                size: 96
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('total'),
                cell: (prop) =>
                    renderComponent(NumberFormatter, {
                        value: prop.getValue<number>()
                    }),
                enableSorting: false,
                header: () =>
                    renderComponent(StackSortHeader, {
                        active: mode === 'stack_frequent',
                        label: 'Events',
                        onclick: () => options?.onStackSort?.('stack_frequent')
                    }),
                id: 'events',
                maxSize: 320,
                meta: {
                    class: 'w-24'
                },
                minSize: 72,
                size: 96
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('first_occurrence'),
                cell: (prop) =>
                    renderComponent(TimeAgo, {
                        value: prop.getValue<string>()
                    }),
                enableSorting: false,
                header: 'First',
                id: 'first',
                maxSize: 480,
                meta: {
                    class: 'w-36'
                },
                minSize: 96,
                size: 144
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('last_occurrence'),
                cell: (prop) =>
                    renderComponent(TimeAgo, {
                        value: prop.getValue<string>()
                    }),
                enableSorting: false,
                header: () =>
                    renderComponent(StackSortHeader, {
                        active: mode === 'stack_recent',
                        label: 'Last',
                        onclick: () => options?.onStackSort?.('stack_recent')
                    }),
                id: 'last',
                maxSize: 480,
                meta: {
                    class: 'w-36'
                },
                minSize: 96,
                size: 144
            }
        );
    }

    return columns;
}

export function getStackSortMode(value: null | string | undefined): StackSortMode | undefined {
    if (value === 'stack_frequent' || value === '-events') {
        return 'stack_frequent';
    }

    if (value === 'stack_recent' || value === '-last') {
        return 'stack_recent';
    }

    return undefined;
}

function formatTextColumn(value: unknown): string {
    return typeof value === 'string' && value.length > 0 ? value : '—';
}

function getProject<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(summary: TSummaryModel): string | undefined {
    const eventSummary = summary as Partial<Pick<EventSummaryModel<SummaryTemplateKeys>, 'project_id' | 'project_name'>> & TSummaryModel;
    return eventSummary.project_name ?? eventSummary.project_id;
}

function getSource<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(summary: TSummaryModel): string | undefined {
    return getSummaryDataValue(summary, 'SourceShortName') ?? getSummaryDataValue(summary, 'Source');
}

function getSummaryDataValue<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(summary: TSummaryModel, key: string): string | undefined {
    const value = (summary.data as Record<string, unknown>)[key];
    return typeof value === 'string' && value.length > 0 ? value : undefined;
}
