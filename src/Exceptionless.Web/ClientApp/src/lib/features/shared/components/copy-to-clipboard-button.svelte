<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { VariantProps } from 'tailwind-variants';

    import { Button, type ButtonProps, type buttonVariants } from '$comp/ui/button';
    import ClipboardCopy from 'lucide-svelte/icons/clipboard-copy';
    import { toast } from 'svelte-sonner';

    type Props = ButtonProps & {
        children?: Snippet;
        size?: VariantProps<typeof buttonVariants>['size'];
        value?: null | string;
    };

    let { children, size = 'icon', title = 'Copy to Clipboard', value }: Props = $props();

    async function copyToClipboard() {
        try {
            await navigator.clipboard.writeText(value ?? '');
            toast.success('Copy to clipboard succeeded');
        } catch {
            toast.error('Copy to clipboard failed');
        }
    }
</script>

<div>
    <Button onclick={copyToClipboard} {size} {title}>
        {#if children}
            {@render children()}
        {:else}
            <ClipboardCopy class="size-4" />
        {/if}
    </Button>
</div>
