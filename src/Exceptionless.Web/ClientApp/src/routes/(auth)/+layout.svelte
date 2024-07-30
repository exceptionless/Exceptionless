<script lang="ts">
    import logo from '$lib/assets/logo.svg';
    import logoDark from '$lib/assets/logo-dark.svg';

    let { children } = $props();
</script>

<div class="flex h-screen">
    <div class="m-auto w-full rounded-md p-6 shadow-md lg:max-w-lg">
        <img src={logo} class="mx-auto h-[100px] dark:hidden" alt="Exceptionless Logo" />
        <img src={logoDark} class="mx-auto hidden h-[100px] dark:block" alt="Exceptionless Logo" />

        {@render children()}
    </div>
</div>
