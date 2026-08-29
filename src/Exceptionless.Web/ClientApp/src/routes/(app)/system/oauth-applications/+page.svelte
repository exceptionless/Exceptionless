<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import { type GetOAuthApplicationsParams, getOAuthApplicationsQuery } from '$features/admin/api.svelte';
    import OAuthApplicationsDataTable from '$features/admin/components/oauth-applications/table/oauth-applications-data-table.svelte';
    import { getTableOptions } from '$features/admin/components/oauth-applications/table/options.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { createQueryParameters } from '$shared/query-params';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';

    const DEFAULT_PARAMS = {
        criteria: '',
        limit: DEFAULT_LIMIT,
        organization: '',
        page: 1
    };

    const queryParams = createQueryParameters({
        defaults: DEFAULT_PARAMS,
        history: 'push',
        schema: {
            criteria: 'string',
            limit: 'number',
            organization: 'string',
            page: 'number'
        }
    });

    const applicationQueryParameters: GetOAuthApplicationsParams = $state({
        get criteria() {
            return queryParams.criteria!;
        },
        set criteria(value) {
            queryParams.criteria = value;
        },
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        },
        get organization() {
            return queryParams.organization!;
        },
        set organization(value) {
            queryParams.organization = value;
        },
        get page() {
            return queryParams.page!;
        },
        set page(value) {
            queryParams.page = value;
        }
    });

    const applicationsQuery = getOAuthApplicationsQuery({
        get params() {
            return applicationQueryParameters;
        }
    });
    const table = createTable(getTableOptions(applicationQueryParameters, applicationsQuery));
    const newApplicationHref = resolve('/(app)/system/oauth-applications/new');

    $effect(() => {
        queryParams.limit ??= DEFAULT_LIMIT;
        queryParams.page ??= 1;
    });

    function setCriteria(value: string) {
        applicationQueryParameters.page = 1;
        applicationQueryParameters.criteria = value;
    }

    function setOrganization(value: string) {
        applicationQueryParameters.page = 1;
        applicationQueryParameters.organization = value;
    }

    function rowHref(application: OAuthApplication) {
        return resolve('/(app)/system/oauth-applications/[id=objectid]', {
            id: application.id
        });
    }

    async function rowClick(application: OAuthApplication) {
        await goto(rowHref(application));
    }
</script>

<div class="space-y-4">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div class="space-y-1">
            <Muted>Manage public OAuth clients that can request access to the Exceptionless API and MCP tools.</Muted>
            <P class="text-muted-foreground text-xs">
                Dynamic clients are registered before consent. Organizations appear after a user authorizes access, and the historical associations are retained
                for administration. One client may be authorized for several organizations.
            </P>
        </div>
        <Button href={newApplicationHref} variant="outline">
            <Plus class="size-4" aria-hidden="true" />
            New OAuth App
        </Button>
    </div>

    {#if applicationsQuery.isError}
        <P class="text-destructive py-8 text-sm">Failed to load OAuth applications.</P>
    {:else}
        <OAuthApplicationsDataTable bind:limit={applicationQueryParameters.limit!} isLoading={applicationsQuery.isPending} {rowClick} {rowHref} {table}>
            {#snippet toolbarChildren()}
                <Input
                    type="search"
                    aria-label="Filter OAuth applications"
                    placeholder="Filter by application name or exact client ID..."
                    class="min-w-56 flex-1"
                    value={applicationQueryParameters.criteria}
                    oninput={(event) => setCriteria(event.currentTarget.value)}
                />
                <Input
                    type="search"
                    aria-label="Filter OAuth applications by organization"
                    placeholder="Filter by organization name or ID..."
                    class="min-w-56 flex-1"
                    value={applicationQueryParameters.organization}
                    oninput={(event) => setOrganization(event.currentTarget.value)}
                />
            {/snippet}
        </OAuthApplicationsDataTable>
    {/if}
</div>
