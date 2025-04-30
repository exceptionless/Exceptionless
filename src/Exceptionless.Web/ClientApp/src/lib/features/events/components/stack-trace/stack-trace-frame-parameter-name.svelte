<script lang="ts">
    import type { ParameterInfo } from '$features/events/models/event-data';

    interface Props {
        parameter: ParameterInfo;
    }

    let { parameter }: Props = $props();
</script>

{#if parameter.name}&nbsp;{parameter.name}{/if}
