<script lang="ts">
    import { untrack } from 'svelte';

    import type { CreateQueryParametersOptions } from './types';

    import { createQueryParameters } from './query-params.svelte';

    let { history = 'push' }: { history?: CreateQueryParametersOptions<{ filter: 'string' }>['history'] } = $props();
    const queryParameters = createQueryParameters({
        debounceMilliseconds: 200,
        history: untrack(() => history),
        schema: {
            filter: 'string'
        }
    });
</script>

<button onclick={() => (queryParameters.filter = 'first')}>First</button>
<button onclick={() => (queryParameters.filter = 'second')}>Second</button>
<button onclick={() => (queryParameters.filter = 'a')}>Alpha</button>
<button onclick={() => (queryParameters.filter = 'a b')}>Spaced</button>
<button onclick={() => (queryParameters.filter = null)}>Clear</button>
<button
    onclick={() =>
        queryParameters.update(
            {
                filter: 'hydrated'
            },
            {
                history: 'replace'
            }
        )}>Hydrate</button
>
<output>{queryParameters.filter}</output>
