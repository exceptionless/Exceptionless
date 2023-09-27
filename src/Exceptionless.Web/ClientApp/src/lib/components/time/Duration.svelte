<script lang="ts">
	import {
		getRelativeTimeFormatUnit,
		getDifferenceInSeconds,
		getSetIntervalTime
	} from '$lib/helpers/dates';
	import { onMount } from 'svelte';

	export let value: Date | string | undefined;
	let durationText = '';
	let durationInSecondsText = '';

	onMount(() => {
		function setDurationText() {
			if (value) {
				const differenceInSeconds = getDifferenceInSeconds(value);
				const rtf = new Intl.RelativeTimeFormat(navigator.language, { numeric: 'auto' });
				durationText = rtf.format(
					-differenceInSeconds,
					getRelativeTimeFormatUnit(differenceInSeconds)
				);
				durationInSecondsText = `${differenceInSeconds} seconds`;
			} else {
				durationText = 'never';
				durationInSecondsText = '';
			}
		}

		setDurationText();
		if (!value) {
			return;
		}

		const interval = setInterval(setDurationText, getSetIntervalTime(value));
		return () => clearInterval(interval);
	});
</script>

<abbr title={durationInSecondsText}>
	{durationText}
</abbr>
