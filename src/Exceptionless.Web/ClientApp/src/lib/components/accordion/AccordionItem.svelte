<script lang="ts">
	export let title: string;
	export let checked: boolean = false;
	export let name: string = 'accordion';
</script>

<div class="collapse collapse-arrow join-item border border-base-300">
	<input type="radio" {name} {checked} />
	<div class="collapse-title text-xl font-medium">
		<slot name="title" {title}>{title}</slot>
	</div>
	<div class="collapse-content">
		<slot />
	</div>
</div>
