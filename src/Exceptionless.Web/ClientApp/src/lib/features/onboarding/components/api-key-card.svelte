<script lang="ts">
    import KeyRound from '@lucide/svelte/icons/key-round';

    import CopyToClipboardButton from '#comp/copy-to-clipboard-button.svelte';
    import * as Alert from '#comp/ui/alert/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import { Skeleton } from '#comp/ui/skeleton/index.ts';

    interface Props {
        apiKey?: string;
        isDisabled: boolean;
        isError: boolean;
        isLoading: boolean;
        isSuspended: boolean;
        onContactSupport: () => void;
        onEnable: () => void;
        onRetry: () => void;
    }

    let { apiKey, isDisabled, isError, isLoading, isSuspended, onContactSupport, onEnable, onRetry }: Props = $props();
</script>

<Card.Root data-api-key-card>
    <Card.Header>
        <Card.Title class="flex items-center gap-2"><KeyRound aria-hidden="true" class="text-muted-foreground size-4" /> Your API key</Card.Title>
        <Card.Description>The snippets below already include it. Keep it handy for config files and environment variables.</Card.Description>
    </Card.Header>
    <Card.Content class="flex flex-col gap-3">
        {#if isLoading}
            <Skeleton aria-label="Loading API key" class="h-9 w-full" />
        {:else if isError || !apiKey}
            <Alert.Root variant="destructive">
                <Alert.Title>We couldn't load your API key</Alert.Title>
                <Alert.Description>
                    <p>Check your connection, then try again.</p>
                    <Button class="mt-2" onclick={onRetry} size="sm" variant="outline">Retry</Button>
                </Alert.Description>
            </Alert.Root>
        {:else}
            <div class="bg-muted flex items-center gap-2 rounded-lg py-1 pr-1 pl-3">
                <code class="min-w-0 flex-1 truncate font-mono text-sm" data-api-key>{apiKey}</code>
                <CopyToClipboardButton size="icon-sm" title="Copy API key" value={apiKey} variant="ghost" />
            </div>
        {/if}

        {#if isDisabled}
            <Alert.Root variant="destructive">
                <Alert.Title>API key disabled</Alert.Title>
                <Alert.Description>
                    <p>Events sent with this key are rejected until it is enabled.</p>
                    <Button class="mt-2" onclick={onEnable} size="sm" variant="outline">Enable API key</Button>
                </Alert.Description>
            </Alert.Root>
        {:else if isSuspended}
            <Alert.Root variant="destructive">
                <Alert.Title>API key suspended</Alert.Title>
                <Alert.Description>
                    <p>Events sent with this key are rejected while it is suspended.</p>
                    <Button class="mt-2" onclick={onContactSupport} size="sm" variant="outline">Contact support</Button>
                </Alert.Description>
            </Alert.Root>
        {/if}
    </Card.Content>
</Card.Root>
