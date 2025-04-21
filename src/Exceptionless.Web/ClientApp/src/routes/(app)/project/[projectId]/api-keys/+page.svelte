<script lang="ts">
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { organization } from '$features/organizations/context.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { type GetProjectTokensParams, getProjectTokensQuery, postProjectToken } from '$features/tokens/api.svelte';
    import { getTableOptions } from '$features/tokens/components/table/options.svelte';
    import TokensDataTable from '$features/tokens/components/table/tokens-data-table.svelte';
    import { NewToken, ViewToken } from '$features/tokens/models';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';

    const projectId = page.params.projectId || '';

    const newToken = postProjectToken({
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    async function addApiKey() {
        const token = new NewToken();
        token.organization_id = organization.current!;
        token.project_id = projectId;
        token.scopes = ['client'];

        await newToken.mutateAsync(token);
        toast.success('API Key added successfully');
    }

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const tokensQueryParameters: GetProjectTokensParams = $state({
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        }
    });

    const tokensQuery = getProjectTokensQuery({
        get params() {
            return tokensQueryParameters;
        },
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const table = createTable(getTableOptions<ViewToken>(tokensQueryParameters, tokensQuery));

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>API Keys</H3>
        <Muted>Create and manage API keys for authenticating your applications with Exceptionless.</Muted>
    </div>
    <Separator />

    <TokensDataTable bind:limit={tokensQueryParameters.limit!} isLoading={tokensQuery.isLoading} {table}>
        {#snippet footerChildren()}
            <div class="h-9 min-w-[140px]">
                <Button size="sm" onclick={addApiKey}>
                    <Plus class="mr-2 size-4" />
                    Add API Key</Button
                >
            </div>

            <DataTable.PageSize bind:value={tokensQueryParameters.limit!} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/snippet}
    </TokensDataTable>
</div>
