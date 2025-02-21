<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { stackStatuses } from '$features/stacks/options';

    interface Props {
        value: StackStatus;
    }

    let { value }: Props = $props();
    let label = $derived(stackStatuses.find((option) => option.value === value)?.label ?? value);
</script>

{#if value}
    <div class="flex w-[100px] items-center">
        <span>{label}</span>
    </div>
{/if}
