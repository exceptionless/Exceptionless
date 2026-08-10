<script lang="ts">
    import { createQueryParameters } from './query-params.svelte';

    const queryParameters = createQueryParameters({
        debounceMilliseconds: 200,
        history: 'replace',
        schema: { filter: 'string' }
    });
</script>

<button onclick={() => (queryParameters.filter = 'first')}>First</button>
<button onclick={() => (queryParameters.filter = 'second')}>Second</button>
<output>{queryParameters.filter}</output>
