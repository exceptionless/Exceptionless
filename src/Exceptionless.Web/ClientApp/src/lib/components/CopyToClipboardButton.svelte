<script lang="ts">
	import IconContentCopy from '~icons/mdi/content-copy';
	import { clickToCopyAction } from 'svelte-legos';

	export let title: string = 'Copy to Clipboard';
	export let value: string | null | undefined;

	function handleCopyDone() {
		console.log('Copy to clipboard succeeded');
	}

	function handleCopyError() {
		console.error('Copy to clipboard failed');
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
