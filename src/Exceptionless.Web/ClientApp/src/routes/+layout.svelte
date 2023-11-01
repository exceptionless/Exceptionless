<script lang="ts">
	import { Toasts } from 'svoast';
	import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
	import { accessToken } from '$api/auth';
	import '../app.css';

	setDefaultBaseUrl('api/v2');
	setAccessTokenStore(accessToken);
</script>

<slot />

<Toasts position="bottom-right" />
