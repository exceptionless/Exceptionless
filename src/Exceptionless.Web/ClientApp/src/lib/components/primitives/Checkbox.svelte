<script lang="ts">
	import { Checkbox } from '$comp/ui/checkbox';
	import Label from '$comp/ui/label/label.svelte';

	export let checked: boolean;
	export let disabled: boolean = false;
	export let id: string;
</script>

<div class="flex items-center space-x-2">
	<Checkbox {id} bind:checked on:click {disabled} />
	<Label
		for={id}
		class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 peer-data-[disabled=true]:cursor-not-allowed peer-data-[disabled=true]:opacity-70"
	>
		<slot />
	</Label>
</div>
