<script lang="ts">
	import logo from '$lib/assets/exceptionless-350.png';
	import IconMicrosoft from '~icons/mdi/microsoft';
	import IconGoogle from '~icons/mdi/google';
	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';

	import { useMutation } from '@sveltestack/svelte-query';
	import { bearerToken, client } from '$lib/api/ApiClient';

	import { goto } from '$app/navigation';
	import { page } from '$app/stores';

	import { Login } from '$lib/models/api';
	import {
		getResponseValidationErrors,
		validate,
		type ValidationErrors
	} from '$lib/validation/validation';
	import type { TokenResult } from '$lib/models/api.generated';

	const data = new Login();
	data.invite_token = $page.url.searchParams.get('token') as string;

	let errors: ValidationErrors<Login> = {};
	const mutation = useMutation(
		(model: Login) =>
			client.postJSON<TokenResult>('http://localhost:5200/api/v2/auth/login', model),
		{
			async onSuccess(data) {
				// TODO: Fix up after nullable reference types.
				bearerToken.set(data.token as string);

				// TODO: Referrer
				await goto('/');
			},
			async onError(error) {
				errors = await getResponseValidationErrors(error);
			}
		}
	);

	const handleSubmit = async () => {
		if ($mutation.isLoading) {
			return;
		}

		errors = await validate(data);
		if (Object.keys(errors).length) {
			return;
		}

		await $mutation.mutateAsync(data);
	};
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
		<form method="post" on:submit|preventDefault={handleSubmit}>
			{#if errors?.general}<p class="text-error">{errors.general}</p>{/if}
			<div class="form-control">
				<label for="email" class="label">
					<span class="label-text">Email</span>
				</label>
				<input
					id="email"
					type="email"
					placeholder="Email Address"
					class="input input-bordered input-primary w-full"
					class:input-error={errors.email}
					on:change={() => {
						errors.email = undefined;
					}}
					bind:value={data.email}
					required
				/>
				{#if errors.email}
					<label for="email" class="label">
						<span class="label-text text-error">{errors.email.join(' ')}</span>
					</label>
				{/if}
			</div>
			<div class="form-control">
				<label for="password" class="label">
					<span class="label-text">Password</span>
					<span class="label-text-alt text-sm">
						<a href="/forgot-password" class="link-secondary link">Forgot password?</a>
					</span>
				</label>
				<input
					id="password"
					type="password"
					placeholder="Enter Password"
					class="input input-bordered input-primary w-full"
					class:input-error={errors.password}
					on:change={() => {
						errors.password = undefined;
					}}
					bind:value={data.password}
					required
				/>
				{#if errors.password}
					<label for="password" class="label">
						<span class="label-text text-error">{errors.password.join(' ')}</span>
					</label>
				{/if}
			</div>
			<div class="my-4">
				<button type="submit" class="btn btn-primary btn-block">
					{#if $mutation.isLoading}
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
			<button class="btn" aria-label="Login with Google">
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
