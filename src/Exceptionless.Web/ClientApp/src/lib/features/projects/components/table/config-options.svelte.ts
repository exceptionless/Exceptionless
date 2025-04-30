import type { ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import ProjectConfigActionsCell from '$features/projects/components/table/project-config-actions-cell.svelte';
import { type ClientConfiguration, ClientConfigurationSetting } from '$features/projects/models';
import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

export type ConfigurationSettingsColumnParameters = {
    projectId: string;
};

export function getColumns<TClientConfigurationSetting extends ClientConfigurationSetting>(
    params: ConfigurationSettingsColumnParameters
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

export function getTableOptions<TClientConfigurationSetting extends ClientConfigurationSetting>(
    columnParameters: ConfigurationSettingsColumnParameters,
    queryParameters: TableMemoryPagingParameters,
    queryResponse: CreateQueryResult<ClientConfiguration, ProblemDetails>
) {
    const knownSettingsToHide = ['@@log:*', '@@DataExclusions', '@@IncludePrivateInformation', '@@UserAgentBotPatterns', 'UserNamespaces', 'CommonMethods'];
    const queryData = $derived(
        Object.entries(queryResponse.data?.settings ?? {})
            .map(([key, value]) => {
                const config = new ClientConfigurationSetting() as TClientConfigurationSetting;
                config.key = key;
                config.value = value;
                return config;
            })
            .filter((setting) => !knownSettingsToHide.includes(setting.key))
    );

    return getSharedTableOptions<TClientConfigurationSetting>({
        columnPersistenceKey: 'projects-configuration-values-column-visibility',
        get columns() {
            return getColumns<TClientConfigurationSetting>(columnParameters);
        },
        paginationStrategy: 'memory',
        get queryData() {
            return queryData;
        },
        get queryParameters() {
            return queryParameters;
        }
    });
}
