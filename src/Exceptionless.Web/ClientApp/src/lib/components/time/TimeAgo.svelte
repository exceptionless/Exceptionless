<script lang="ts">
	import Time from 'svelte-time';

	export let date: Date | string | undefined;
</script>

<Time live={true} relative={true} timestamp={date}></Time>
