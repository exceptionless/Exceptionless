<script lang="ts">
	import { logout } from '$lib/api/Auth';
	import { goto } from '$app/navigation';

	async function onLogout() {
		await logout();
		await goto('/login');
	}
</script>

<svelte:head>
	<title>Log out</title>
</svelte:head>

<h2 class="mt-5 text-center text-2xl font-bold leading-9 tracking-tight">Log out?</h2>
<form on:submit|preventDefault={onLogout}>
	<div class="my-4">
		<button type="submit" class="btn btn-primary btn-block"> Logout</button>
	</div>
</form>
