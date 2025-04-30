<script lang="ts">
    import { H3, Muted } from '$comp/typography';
    import { Label } from '$comp/ui/label';
    import * as RadioGroup from '$comp/ui/radio-group';
    import { Separator } from '$comp/ui/separator';
    import { setMode, userPrefersMode } from 'mode-watcher';

    import ThemePreview from './(components)/theme-preview.svelte';

    function onUserThemePreferenceChange(mode?: string) {
        setMode(mode as 'dark' | 'light' | 'system');
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Appearance</H3>
        <Muted>Customize the appearance of the app. Automatically switch between day and night themes.</Muted>
    </div>
    <Separator />

    <form>
        <RadioGroup.Root
            class="grid max-w-xl grid-cols-3 gap-8 pt-2"
            orientation="horizontal"
            onValueChange={onUserThemePreferenceChange}
            value={userPrefersMode.current}
        >
            <Label class="[&:has([data-state=checked])>div]:border-primary" for="light">
                <RadioGroup.Item class="sr-only" id="light" value="light" />
                <ThemePreview mode="light" />
                <div class="pt-2 text-center">Light</div>
            </Label>
            <Label class="[&:has([data-state=checked])>div]:border-primary" for="dark">
                <RadioGroup.Item class="sr-only" id="dark" value="dark" />
                <ThemePreview mode="dark" />
                <div class="pt-2 text-center">Dark</div>
            </Label>
            <Label class="[&:has([data-state=checked])>div]:border-primary" for="system">
                <RadioGroup.Item class="sr-only" id="system" value="system" />
                <ThemePreview mode="system" />
                <div class="pt-2 text-center">System</div>
            </Label>
        </RadioGroup.Root>
    </form>
</div>
