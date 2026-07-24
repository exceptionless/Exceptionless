<script lang="ts">
    import type { ViewToken } from '$features/tokens/models';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import { A, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { organization } from '$features/organizations/context.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { type GetProjectTokensParams, getProjectTokensQuery, postProjectToken } from '$features/tokens/api.svelte';
    import { getTableOptions } from '$features/tokens/components/table/options.svelte';
    import TokensDataTable from '$features/tokens/components/table/tokens-data-table.svelte';
    import { createProjectToken, type ProjectTokenScope } from '$features/tokens/project-token';
    import FileCode from '@lucide/svelte/icons/file-code-2';
    import KeyRound from '@lucide/svelte/icons/key-round';
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

    async function addToken(scope: ProjectTokenScope) {
        const isSourceMapToken = scope === 'source-maps:write';
        const token = createProjectToken(organization.current!, projectId, scope);

        await newToken.mutateAsync(token);
        toast.success(isSourceMapToken ? 'Source map upload token added successfully' : 'Client API key added successfully');
    }

    async function addClientApiKey() {
        await addToken('client');
    }

    async function addSourceMapToken() {
        await addToken('source-maps:write');
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
    <div class="space-y-1">
        <Muted>Create and manage client API keys and project-scoped deployment tokens.</Muted>
        <Muted>
            To install an SDK and start sending events, open
            <A href={resolve('/(app)/project/[projectId]/configure', { projectId })}>Client setup</A>.
        </Muted>
    </div>

    <TokensDataTable bind:limit={tokensQueryParameters.limit!} isLoading={tokensQuery.isLoading} {table}>
        {#snippet toolbarChildren()}
            <div class="flex-1"></div>
            <DataTableViewOptions size="icon-lg" {table} />
            <DropdownMenu.Root>
                <DropdownMenu.Trigger>
                    {#snippet child({ props })}
                        <Button {...props} size="lg" disabled={newToken.isPending}>
                            <Plus class="size-4" aria-hidden="true" />
                            Add token
                        </Button>
                    {/snippet}
                </DropdownMenu.Trigger>
                <DropdownMenu.Content align="end" class="w-72">
                    <DropdownMenu.Group>
                        <DropdownMenu.GroupHeading>Add token</DropdownMenu.GroupHeading>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Item class="items-start" onclick={addClientApiKey} disabled={newToken.isPending}>
                            <KeyRound class="mt-0.5 size-4" aria-hidden="true" />
                            <span>
                                <span class="block font-medium">Client API key</span>
                                <span class="text-muted-foreground block text-xs">Send events from an application or service.</span>
                            </span>
                        </DropdownMenu.Item>
                        <DropdownMenu.Item class="items-start" onclick={addSourceMapToken} disabled={newToken.isPending}>
                            <FileCode class="mt-0.5 size-4" aria-hidden="true" />
                            <span>
                                <span class="block font-medium">Source map upload token</span>
                                <span class="text-muted-foreground block text-xs">Upload source maps from CI/CD for this project only.</span>
                            </span>
                        </DropdownMenu.Item>
                    </DropdownMenu.Group>
                </DropdownMenu.Content>
            </DropdownMenu.Root>
        {/snippet}
    </TokensDataTable>
</div>
