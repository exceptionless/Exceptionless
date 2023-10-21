<script lang="ts">
	import IconContentCopy from '~icons/mdi/content-copy';
	import { clickToCopyAction } from 'svelte-legos';
	import { toast } from 'svoast';

	export let title: string = 'Copy to Clipboard';
	export let value: string | null | undefined;

	function handleCopyDone() {
		toast.success('Copy to clipboard succeeded');
	}

	function handleCopyError() {
		toast.error('Copy to clipboard failed');
	}
</script>

<button
	{title}
	use:clickToCopyAction={() => value || ''}
	on:copy-done={handleCopyDone}
	on:copy-error={handleCopyError}
>
	<slot><IconContentCopy /></slot>
</button>
