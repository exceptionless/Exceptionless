<script lang="ts">
    import { List } from '#comp/typography/index.ts';

    interface Props {
        logs?: string[];
    }

    let { logs }: Props = $props();
</script>

{#if logs}
    <List items={logs} />
{/if}
