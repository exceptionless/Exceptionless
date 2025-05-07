<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { VariantProps } from 'tailwind-variants';

    import { Button, type ButtonProps, type buttonVariants } from '$comp/ui/button';
    import { UseClipboard } from '$lib/hooks/use-clipboard.svelte';
    import ClipboardCopy from '@lucide/svelte/icons/clipboard-copy';
    import { toast } from 'svelte-sonner';

    type Props = ButtonProps & {
        children?: Snippet;
        size?: VariantProps<typeof buttonVariants>['size'];
        value?: null | string;
        variant?: VariantProps<typeof buttonVariants>['variant'];
    };

    let { children, size = 'icon', title = 'Copy to Clipboard', value, variant = 'default' }: Props = $props();

    const clipboard = new UseClipboard();

    async function copyToClipboard() {
        await clipboard.copy(value ?? '');
        if (clipboard.copied) {
            toast.success('Copy to clipboard succeeded');
        } else {
            toast.error('Copy to clipboard failed');
        }
    }
</script>

<div>
    <Button onclick={copyToClipboard} {size} {title} {variant}>
        {#if children}
            {@render children()}
        {:else}
            <ClipboardCopy class="size-4" />
        {/if}
    </Button>
</div>
