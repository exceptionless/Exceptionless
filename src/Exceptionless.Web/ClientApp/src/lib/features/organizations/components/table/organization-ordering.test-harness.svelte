<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { FetchClientResponse, ProblemDetails } from '@foundatiofx/fetchclient';
    import type { CreateQueryResult } from '@tanstack/svelte-query';

    import { createTable } from '@tanstack/svelte-table';

    import { getTableOptions } from './options.svelte';

    let { organizations }: { organizations: ViewOrganization[] } = $props();
    const parameters = $state({
        limit: 20,
        mode: 'stats' as const,
        page: 1
    });
    const response = {
        get data() {
            return {
                data: organizations
            };
        }
    } as CreateQueryResult<FetchClientResponse<ViewOrganization[]>, ProblemDetails>;
    const table = createTable(getTableOptions(parameters, response));
</script>

<button onclick={() => table.getRowModel().rows[0]?.toggleSelected()}>Select row</button>
<span aria-label="Selected rows">{Object.keys(table.options.state?.rowSelection ?? {}).length}</span>
<button onclick={() => table.setPageIndex(1)}>Next page</button>
<button
    onclick={() =>
        table.setSorting([
            {
                desc: true,
                id: 'name'
            }
        ])}>Sort descending</button
>
<button
    onclick={() =>
        table.setSorting([
            {
                desc: false,
                id: 'name'
            }
        ])}>Sort ascending</button
>
<button onclick={() => table.setPageSize(5)}>Five rows</button>
<span aria-label="Page">{table.options.state?.pagination?.pageIndex}</span>
<span aria-label="Page count">{table.getPageCount()}</span>
<span aria-label="Parameter page">{parameters.page}</span>
<ol>
    {#each table.options.data as organization (organization.id)}
        <li>{organization.name}</li>
    {/each}
</ol>
