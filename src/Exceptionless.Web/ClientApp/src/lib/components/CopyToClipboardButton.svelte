<script lang="ts">
    import type { Snippet } from 'svelte';
    import { toast } from 'svelte-sonner';
    import type { VariantProps } from 'tailwind-variants';
    import IconContentCopy from '~icons/mdi/content-copy';

    import { Button, type ButtonProps, type buttonVariants } from '$comp/ui/button';

    type Props = ButtonProps & {
        children?: Snippet;
        value?: string | null;
        size?: VariantProps<typeof buttonVariants>['size'];
    };

    let { children, title = 'Copy to Clipboard', value, size = 'icon' }: Props = $props();

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
    <Button {title} {size} on:click={copyToClipboard}>
        {#if children}
            {@render children()}
        {:else}
            <IconContentCopy class="h-4 w-4" />
        {/if}
    </Button>
</div>
