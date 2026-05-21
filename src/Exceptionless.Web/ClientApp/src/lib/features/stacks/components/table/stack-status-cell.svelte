<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { stackStatuses } from '$features/stacks/options';

    interface Props {
        value: StackStatus;
    }

    let { value }: Props = $props();

    const label = $derived(stackStatuses.find((option) => option.value === value)?.label ?? value);
</script>

<span>{label}</span>
