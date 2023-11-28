<script lang="ts">
	import logo from '$lib/assets/exceptionless-350.png';
</script>

<div class="flex h-screen">
	<div class="m-auto w-full rounded-md p-6 shadow-md lg:max-w-lg">
		<img class="mx-auto" src={logo} alt="Exceptionless" />
		<slot />
	</div>
</div>
