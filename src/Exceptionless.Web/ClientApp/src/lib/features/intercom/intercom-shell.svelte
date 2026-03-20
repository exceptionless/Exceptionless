<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { BootOptions } from 'svelte-intercom';

    import { IntercomProvider } from 'svelte-intercom';

    import { openSupportChat } from './chat';
    import { getIntercom } from './context.svelte';
    import IntercomInitializer from './intercom-initializer.svelte';

    interface Props {
        appId?: string;
        bootOptions?: BootOptions;
        children: Snippet<[() => void]>;
        onUnreadCountChange?: (unreadCount: number) => void;
        routeKey?: string;
    }

    let {
        appId = undefined,
        bootOptions = undefined,
        children,
        onUnreadCountChange = undefined,
        routeKey = undefined
    }: Props = $props();

    const shouldBootIntercom = $derived(!!appId && !!bootOptions);

    function openChatFallback() {
        openSupportChat(undefined);
    }
</script>

{#if appId}
    <IntercomProvider {appId} autoboot={shouldBootIntercom} {bootOptions} {onUnreadCountChange}>
        <IntercomInitializer {bootOptions} {routeKey}>
            {@const intercom = getIntercom()}
            {@const openChat = () => openSupportChat(shouldBootIntercom ? intercom : undefined)}
            {@render children(openChat)}
        </IntercomInitializer>
    </IntercomProvider>
{:else}
    {@render children(openChatFallback)}
{/if}