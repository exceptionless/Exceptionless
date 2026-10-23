<script lang="ts">
    import { untrack } from 'svelte';

    import type { CreateQueryParametersOptions } from './types';

    import { createQueryParameters } from './query-params.svelte';

    type Schema = { filter: 'string' };

    let { history = 'push', parameter = 'filter' }: { history?: CreateQueryParametersOptions<Schema>['history']; parameter?: 'filter' | 'project' } = $props();
    // Each harness owns one key so several can share a page, as independent components do.
    const key = untrack(() => parameter) as keyof Schema;
    const queryParameters = createQueryParameters({
        debounceMilliseconds: 200,
        history: untrack(() => history),
        schema: {
            [key]: 'string'
        } as Schema
    });
</script>

<button onclick={() => (queryParameters[key] = 'first')}>First</button>
<button onclick={() => (queryParameters[key] = 'second')}>Second</button>
<button onclick={() => (queryParameters[key] = 'a')}>Alpha</button>
<button onclick={() => (queryParameters[key] = 'a b')}>Spaced</button>
<button onclick={() => (queryParameters[key] = null)}>Clear</button>
<button
    onclick={() =>
        queryParameters.update(
            {
                [key]: 'hydrated'
            },
            {
                history: 'replace'
            }
        )}>Hydrate</button
>
<output>{queryParameters[key]}</output>
