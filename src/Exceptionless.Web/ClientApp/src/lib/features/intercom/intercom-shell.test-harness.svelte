<script lang="ts">
    import type { BootOptions } from 'svelte-intercom';

    import IntercomShell from './intercom-shell.svelte';
    import IntercomShellTestProbe from './intercom-shell.test-probe.svelte';

    interface Props {
        appId?: string;
        bootOptions?: BootOptions;
        onMountProbe?: () => void;
    }

    let { appId = undefined, bootOptions = undefined, onMountProbe = () => {} }: Props = $props();
</script>

<IntercomShell {appId} {bootOptions}>
    {#snippet children(openChat)}
        <IntercomShellTestProbe {onMountProbe} />
        <button data-testid="open-chat" onclick={openChat}>Open chat</button>
    {/snippet}
</IntercomShell>