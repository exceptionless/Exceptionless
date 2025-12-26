<script lang="ts">
    import type { NewToken, ViewToken } from '$features/tokens/models';

    import { page } from '$app/state';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { organization } from '$features/organizations/context.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { type GetProjectTokensParams, getProjectTokensQuery, postProjectToken } from '$features/tokens/api.svelte';
    import { getTableOptions } from '$features/tokens/components/table/options.svelte';
    import TokensDataTable from '$features/tokens/components/table/tokens-data-table.svelte';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';

    const projectId = $derived(page.params.projectId || '');

    const newToken = postProjectToken({
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    async function addApiKey() {
        const token: NewToken = {
            organization_id: organization.current!,
            project_id: projectId,
            scopes: ['client']
        };

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
    <div class="flex items-start justify-between">
        <div>
            <H3>API Keys</H3>
            <Muted>Create and manage API keys for authenticating your applications with Exceptionless.</Muted>
        </div>
        <Button size="icon" onclick={addApiKey} title="Add API Key" class="shrink-0">
            <Plus class="size-4" aria-hidden="true" />
            <span class="sr-only">Add API Key</span>
        </Button>
    </div>
    <Separator />

    <TokensDataTable bind:limit={tokensQueryParameters.limit!} isLoading={tokensQuery.isLoading} {table} />
</div>
