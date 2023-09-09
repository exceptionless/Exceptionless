<script lang="ts">
	import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
	import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
	import { accessToken } from '$api/auth';
	import '../app.css';

	setDefaultBaseUrl('api/v2');
	setAccessTokenStore(accessToken);

	// Create a client
	const queryClient = new QueryClient();
</script>

<QueryClientProvider client={queryClient}>
	<slot />
</QueryClientProvider>
