<script lang="ts">
    import { createQueryParameters } from '#shared/query-params/index.ts';

    import { clearSavedViewQueryParams } from './use-saved-views.svelte';

    const queryParams = createQueryParameters({
        defaults: {
            filter: '(status:open OR status:regressed)'
        },
        history: 'push',
        schema: {
            filter: 'string',
            filters: 'string',
            limit: 'number',
            saved: 'string'
        }
    });
</script>

<button onclick={() => clearSavedViewQueryParams(queryParams)}>Clear view</button>
<button onclick={() => clearSavedViewQueryParams(queryParams, 'replace')}>Clear deleted view</button>
<output>{queryParams.saved ?? 'No saved view'}</output>
