<script lang="ts">
    import * as Tooltip from '$comp/ui/tooltip';

    import TimeAgo from './time-ago.svelte';

    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();
</script>

<Tooltip.Provider>
    <TimeAgo {value} />
</Tooltip.Provider>
