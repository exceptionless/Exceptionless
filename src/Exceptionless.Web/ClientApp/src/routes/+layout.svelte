<script lang="ts">
	import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
	import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';
	import { ModeWatcher } from 'mode-watcher';

	import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
	import { accessToken } from '$api/auth';
	import { Toaster } from '$comp/ui/sonner';
	import '../app.css';

	setDefaultBaseUrl('api/v2');
	setAccessTokenStore(accessToken);

	const queryClient = new QueryClient();
</script>

<div class="bg-background text-foreground">
	<ModeWatcher defaultMode={'dark'} />

	<QueryClientProvider client={queryClient}>
		<slot />

		<SvelteQueryDevtools />
	</QueryClientProvider>

	<Toaster position="bottom-right" />
</div>
