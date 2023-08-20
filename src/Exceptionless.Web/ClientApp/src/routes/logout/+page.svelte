<script lang="ts">
	import logo from '$lib/assets/exceptionless-350.png';

	import { logout } from '$lib/api/Auth';
	import { goto } from '$app/navigation';

	async function onLogout() {
		logout();
		goto('/login');
	}
</script>

<svelte:head>
	<title>Log out</title>
</svelte:head>

<div class="flex h-screen">
	<div class="m-auto w-full rounded-md bg-white p-6 shadow-md lg:max-w-lg">
		<img class="mx-auto" src={logo} alt="Exceptionless" />
		<h2 class="mt-5 text-center text-2xl font-bold leading-9 tracking-tight">Log out?</h2>
		<form on:submit|preventDefault={onLogout}>
			<div class="my-4">
				<button type="submit" class="btn btn-primary btn-block"> Logout </button>
			</div>
		</form>
	</div>
</div>
