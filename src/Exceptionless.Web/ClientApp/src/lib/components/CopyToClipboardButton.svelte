<script lang="ts">
    import type { VariantProps } from 'tailwind-variants';
    import IconContentCopy from '~icons/mdi/content-copy';
    import { toast } from 'svelte-sonner';
    import { Button, type ButtonProps, buttonVariants } from '$comp/ui/button';

    type Props = ButtonProps & {
        value?: string | null;
        size: VariantProps<typeof buttonVariants>['size'];
    };

    let { title = 'Copy to Clipboard', value, size = 'icon' }: Props = $props();

    // UPGRADE
    function handleCopyDone() {
        toast.success('Copy to clipboard succeeded');
    }

    function handleCopyError() {
        toast.error('Copy to clipboard failed');
    }
</script>

<div>
    <Button {title} {size}>
        <slot><IconContentCopy class="h-4 w-4" /></slot>
    </Button>
</div>
