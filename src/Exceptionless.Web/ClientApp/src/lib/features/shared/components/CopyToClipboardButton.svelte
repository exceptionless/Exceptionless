<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { VariantProps } from 'tailwind-variants';

    import { Button, type ButtonProps, type buttonVariants } from '$comp/ui/button';
    import IconContentCopy from '~icons/mdi/content-copy';
    import { toast } from 'svelte-sonner';

    type Props = {
        children?: Snippet;
        size?: VariantProps<typeof buttonVariants>['size'];
        value?: null | string;
    } & ButtonProps;

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
            <IconContentCopy class="h-4 w-4" />
        {/if}
    </Button>
</div>
