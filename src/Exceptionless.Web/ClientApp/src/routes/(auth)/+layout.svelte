<script lang="ts">
    import logo from '$lib/assets/logo.svg';
    import logoDark from '$lib/assets/logo-dark.svg';

    let { children } = $props();
</script>

<div class="flex h-screen">
    <div class="m-auto w-full rounded-md p-6 shadow-md lg:max-w-lg">
        <img alt="Exceptionless Logo" class="mx-auto h-[100px] dark:hidden" src={logo} />
        <img alt="Exceptionless Logo" class="mx-auto hidden h-[100px] dark:block" src={logoDark} />

        {@render children()}
    </div>
</div>
