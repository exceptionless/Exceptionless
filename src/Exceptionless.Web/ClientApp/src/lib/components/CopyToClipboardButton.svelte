<script lang="ts">
	import type { VariantProps } from 'tailwind-variants';
	import IconContentCopy from '~icons/mdi/content-copy';
	import { clickToCopyAction } from 'svelte-legos';
	import { toast } from 'svelte-sonner';
	import { Button, buttonVariants } from '$comp/ui/button';

	export let title: string = 'Copy to Clipboard';
	export let value: string | null | undefined;
	export let size: VariantProps<typeof buttonVariants>['size'] = 'icon';

	function handleCopyDone() {
		toast.success('Copy to clipboard succeeded');
	}

	function handleCopyError() {
		toast.error('Copy to clipboard failed');
	}
</script>

<div
	use:clickToCopyAction={() => value || ''}
	on:copy-done={handleCopyDone}
	on:copy-error={handleCopyError}
>
	<Button {title} {size}>
		<slot><IconContentCopy class="w-4 h-4" /></slot>
	</Button>
</div>
