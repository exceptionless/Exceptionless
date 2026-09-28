<script lang="ts">
    import { resolve } from '$app/paths';
    import { Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { type GetOAuthApplicationsParams, getOAuthApplicationsQuery } from '$features/admin/api.svelte';
    import OAuthApplicationsDataTable from '$features/admin/components/oauth-applications/table/oauth-applications-data-table.svelte';
    import { getTableOptions } from '$features/admin/components/oauth-applications/table/options.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { createQueryParameters } from '$shared/query-params';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';

    const DEFAULT_PARAMS = {
        authorization: 'authorized',
        criteria: '',
        limit: DEFAULT_LIMIT,
        organization: '',
        page: 1
    };

    const queryParams = createQueryParameters({
        defaults: DEFAULT_PARAMS,
        history: 'push',
        schema: {
            authorization: 'string',
            criteria: 'string',
            limit: 'number',
            organization: 'string',
            page: 'number'
        }
    });

    const applicationQueryParameters: GetOAuthApplicationsParams = $state({
        get authorized() {
            return queryParams.authorization === 'all' ? undefined : queryParams.authorization !== 'unauthorized';
        },
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
        },
        sort: '-updated_utc'
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

    const authorizationOptions = [
        {
            label: 'Authorized',
            value: 'authorized'
        },
        {
            label: 'Not authorized',
            value: 'unauthorized'
        },
        {
            label: 'All applications',
            value: 'all'
        }
    ];

    function setAuthorization(value: string) {
        if (!value) {
            return;
        }

        queryParams.page = 1;
        queryParams.authorization = value;
    }

    function setCriteria(value: string) {
        applicationQueryParameters.page = 1;
        applicationQueryParameters.criteria = value;
    }

    function setOrganization(value: string) {
        applicationQueryParameters.page = 1;
        applicationQueryParameters.organization = value;
    }
</script>

<div class="space-y-4">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Muted>Manage public OAuth clients that can request access to the Exceptionless API and MCP tools.</Muted>
        <Button href={newApplicationHref} variant="outline">
            <Plus class="size-4" aria-hidden="true" />
            New OAuth App
        </Button>
    </div>

    {#if applicationsQuery.isError}
        <P class="text-destructive py-8 text-sm">Failed to load OAuth applications.</P>
    {:else}
        <OAuthApplicationsDataTable bind:limit={applicationQueryParameters.limit!} isLoading={applicationsQuery.isPending} {table}>
            {#snippet toolbarChildren()}
                <Select.Root type="single" value={queryParams.authorization ?? 'authorized'} onValueChange={setAuthorization}>
                    <Select.Trigger aria-label="Filter by authorization" class="w-44">
                        {authorizationOptions.find((option) => option.value === queryParams.authorization)?.label ?? 'Authorized'}
                    </Select.Trigger>
                    <Select.Content>
                        <Select.Group>
                            {#each authorizationOptions as option (option.value)}
                                <Select.Item value={option.value} label={option.label}>{option.label}</Select.Item>
                            {/each}
                        </Select.Group>
                    </Select.Content>
                </Select.Root>
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
