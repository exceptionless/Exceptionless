<script lang="ts">
	import Time from 'svelte-time';

	export let value: Date | string | undefined;
</script>

<Time live={true} relative={true} timestamp={value}></Time>
