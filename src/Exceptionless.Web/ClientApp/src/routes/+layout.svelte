<script lang="ts">
	import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
	import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';

	import { Toasts } from 'svoast';
	import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
	import { accessToken } from '$api/auth';
	import '../app.css';

	setDefaultBaseUrl('api/v2');
	setAccessTokenStore(accessToken);

	const queryClient = new QueryClient();
</script>

<QueryClientProvider client={queryClient}>
	<div class="bg-background text-foreground">
		<slot />
	</div>
	<SvelteQueryDevtools />
</QueryClientProvider>

<Toasts position="bottom-right" />
