<script lang="ts">
    import type { ParameterInfo } from '$features/events/models/event-data';

    interface Props {
        parameter: ParameterInfo;
    }

    let { parameter }: Props = $props();
</script>

{#if parameter.type_namespace}<span class="text-purple-400">{parameter.type_namespace}.</span>{/if}{#if parameter.type}<span class="text-purple-400"
        >{parameter.type.replace('+', '')}</span
    >{/if}
