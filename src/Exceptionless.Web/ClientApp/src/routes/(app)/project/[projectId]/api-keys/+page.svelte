<script lang="ts">
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { organization } from '$features/organizations/context.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { getProjectTokensQuery, postProjectToken } from '$features/tokens/api.svelte';
    import { getTableContext } from '$features/tokens/components/table/options.svelte';
    import TokensDataTable from '$features/tokens/components/table/tokens-data-table.svelte';
    import { NewToken, ViewToken } from '$features/tokens/models';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import Plus from 'lucide-svelte/icons/plus';
    import { watch } from 'runed';
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

    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const context = getTableContext<ViewToken>({ limit: params.limit! });
    const table = createTable(context.options);

    const tokensQuery = getProjectTokensQuery({
        params: context.parameters,
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    watch(
        () => tokensQuery.dataUpdatedAt,
        () => {
            if (tokensQuery.isSuccess) {
                context.data = tokensQuery.data.data || [];
                context.meta = tokensQuery.data.meta;
            }
        }
    );

    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;
    });

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>API Keys</H3>
        <Muted>Create and manage API keys for authenticating your applications with Exceptionless.</Muted>
    </div>
    <Separator />

    <TokensDataTable bind:limit={params.limit!} isLoading={tokensQuery.isLoading} {table}>
        {#snippet footerChildren()}
            <div class="h-9 min-w-[140px]">
                <Button size="sm" onclick={addApiKey}>
                    <Plus class="mr-2 size-4" />
                    Add API Key</Button
                >
            </div>

            <DataTable.PageSize bind:value={params.limit!} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/snippet}
    </TokensDataTable>
</div>
