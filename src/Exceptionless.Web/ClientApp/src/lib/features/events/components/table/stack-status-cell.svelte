<script lang="ts">
    import { stackStatuses } from '../options';

    interface Props {
        value: string;
    }

    let { value }: Props = $props();
    let status = $derived(stackStatuses.find((status) => status.value === value));
</script>

{#if status}
    <div class="flex w-[100px] items-center">
        <span>{status.label}</span>
    </div>
{/if}
