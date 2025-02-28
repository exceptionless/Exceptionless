<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import StackStatusBadge from "$features/stacks/components/stack-status-badge.svelte";

    interface Props {
        value: StackStatus;
    }

    let { value }: Props = $props();
</script>

{#if value}
    <div class="flex w-[100px] items-center">
        <StackStatusBadge status={value} />
    </div>
{/if}
