import type { ClientConfigurationSetting } from '$features/projects/models';

import ProjectConfigActionsCell from '$features/projects/components/table/project-config-actions-cell.svelte';
import {
    type ColumnDef,
    getCoreRowModel,
    getPaginationRowModel,
    renderComponent,
    type TableOptions,
    type Updater,
    type VisibilityState
} from '@tanstack/svelte-table';
import { PersistedState } from 'runed';

export type ProjectClientConfigurationSettingsParameters = {
    limit: number;
    projectId: string;
};

export function getColumns<TClientConfigurationSetting extends ClientConfigurationSetting>(
    params: ProjectClientConfigurationSettingsParameters
): ColumnDef<TClientConfigurationSetting>[] {
    const columns: ColumnDef<TClientConfigurationSetting>[] = [
        {
            accessorKey: 'key',
            cell: (info) => info.getValue(),
            enableHiding: false,
            header: 'Key',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            accessorKey: 'value',
            cell: (info) => info.getValue(),
            enableHiding: false,
            header: 'Value',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            cell: (info) => renderComponent(ProjectConfigActionsCell, { projectId: params.projectId, setting: info.row.original }),
            enableHiding: false,
            enableSorting: false,
            header: 'Actions',
            id: 'actions',
            meta: {
                class: 'w-16'
            }
        }
    ];

    return columns;
}

export function getTableContext<TClientConfigurationSetting extends ClientConfigurationSetting>(
    params: ProjectClientConfigurationSettingsParameters,
    configureOptions: (options: TableOptions<TClientConfigurationSetting>) => TableOptions<TClientConfigurationSetting> = (options) => options
) {
    let _columns = $state(getColumns<TClientConfigurationSetting>(params));
    let _data = $state([] as TClientConfigurationSetting[]);

    const [columnVisibility, setColumnVisibility] = createPersistedTableState('project-config-column-visibility', <VisibilityState>{});
    const options = configureOptions({
        get columns() {
            return _columns;
        },
        set columns(value) {
            _columns = value;
        },
        get data() {
            return _data;
        },
        set data(value) {
            _data = value;
        },
        enableMultiRowSelection: true,
        enableRowSelection: true,
        enableSortingRemoval: false,
        getCoreRowModel: getCoreRowModel(),
        getPaginationRowModel: getPaginationRowModel(),
        getRowId: (originalRow) => originalRow.key,
        onColumnVisibilityChange: setColumnVisibility,
        state: {
            get columnVisibility() {
                return columnVisibility();
            }
        }
    });

    return {
        get data() {
            return _data;
        },
        set data(value) {
            _data = value;
        },
        options
    };
}

function createPersistedTableState<T>(key: string, initialValue: T): [() => T, (updater: Updater<T>) => void] {
    const persistedValue = new PersistedState<T>(key, initialValue);

    return [
        () => persistedValue.current,
        (updater: Updater<T>) => {
            if (updater instanceof Function) {
                persistedValue.current = updater(persistedValue.current);
            } else {
                persistedValue.current = updater;
            }
        }
    ];
}
