<script lang="ts">
    import type { ParameterInfo } from '$features/events/models/event-data';

    interface Props {
        parameter: ParameterInfo;
    }

    let { parameter }: Props = $props();
</script>

{#if parameter.generic_arguments?.length}&lt;<span class="text-purple-400">{parameter.generic_arguments.join(', ')}</span>&gt;{/if}
