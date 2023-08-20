<script lang="ts">
	import EmailInput from '$comp/form/EmailInput.svelte';
	import PasswordInput from '$comp/form/PasswordInput.svelte';

	import logo from '$lib/assets/exceptionless-350.png';
	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';
	import IconGoogle from '~icons/mdi/google';
	import IconMicrosoft from '~icons/mdi/microsoft';

	import { goto } from '$app/navigation';
	import { page } from '$app/stores';
	import { googleLogin, accessToken } from '$api/Auth';
	import { FetchClient, ProblemDetails } from '$lib/api/FetchClient';
	import type { TokenResult } from '$lib/models/api.generated';
	import { Login } from '$lib/models/api';

	const data = new Login();
	data.invite_token = $page.url.searchParams.get('token') as string;

	const api = new FetchClient();
	const loading = api.loading;
	let problem = new ProblemDetails();
	let url = ($page.url.searchParams.get('url') as string) ?? '/';

	async function login() {
		if ($loading) {
			return;
		}

		const response = await api.postJSON<TokenResult>('auth/login', data, {
			expectedStatusCodes: [401]
		});
		if (response.success && response.data?.token) {
			accessToken.set(response.data.token);
			await goto(url);
		} else if (response.status === 401) {
			problem = problem.setErrorMessage('Invalid email or password.');
		} else if (response.problem) {
			problem = response.problem;
		}
	}
</script>

<svelte:head>
	<title>Log in</title>
</svelte:head>

<div class="flex h-screen">
	<div class="m-auto w-full rounded-md bg-white p-6 shadow-md lg:max-w-lg">
		<img class="mx-auto" src={logo} alt="Exceptionless" />
		<h2 class="mt-5 text-center text-2xl font-bold leading-9 tracking-tight">
			Log in to your account
		</h2>
		<form on:submit|preventDefault={login}>
			{#if problem.errors.general}<p class="text-error">{problem.errors.general}</p>{/if}
			<EmailInput
				name="email"
				bind:value={data.email}
				required
				{problem}
				placeholder="Enter email address"
			></EmailInput>
			<PasswordInput
				name="password"
				bind:value={data.password}
				required
				{problem}
				placeholder="Enter password"
			>
				<span slot="label" class="label-text-alt text-sm">
					<a href="/forgot-password" class="link link-secondary" tabindex="-1"
						>Forgot password?</a
					>
				</span>
			</PasswordInput>
			<div class="my-4">
				<button type="submit" class="btn btn-primary btn-block">
					{#if $loading}
						<span class="loading loading-spinner"></span> Logging in...
					{:else}
						Login
					{/if}
				</button>
			</div>
		</form>

		<div class="my-4 flex w-full items-center">
			<hr class="w-full" />
			<p class="px-3">OR</p>
			<hr class="w-full" />
		</div>
		<div class="auto-cols-2 grid grid-flow-col grid-rows-2 gap-4">
			<button class="btn" aria-label="Login with Microsoft">
				<IconMicrosoft /> Microsoft
			</button>
			<button class="btn" aria-label="Login with Google" on:click={googleLogin}>
				<IconGoogle /> Google
			</button>
			<button class="btn" aria-label="Login with Facebook">
				<IconFacebook /> Facebook
			</button>
			<button class="btn" aria-label="Login with GitHub">
				<IconGitHub /> GitHub
			</button>
		</div>

		<p class="mt-5 text-center text-sm">
			Not a member?
			<a href="/signup" class="link-primary link">Start a free trial</a>
		</p>

		<p class="mt-5 text-center text-sm">
			By signing up, you agree to our <a
				href="https://exceptionless.com/privacy"
				target="_blank"
				class="link">Privacy Policy</a
			>
			and
			<a href="https://exceptionless.com/terms" target="_blank" class="link"
				>Terms of Service</a
			>.
		</p>
	</div>
</div>
