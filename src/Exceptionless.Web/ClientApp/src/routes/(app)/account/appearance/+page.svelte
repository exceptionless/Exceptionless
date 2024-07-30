<script lang="ts">
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Label } from '$comp/ui/label';
    import * as RadioGroup from '$comp/ui/radio-group';
    import { Separator } from '$comp/ui/separator';

    import ThemePreview from './(components)/ThemePreview.svelte';

    async function onSave() {}
</script>

<div class="space-y-6">
    <div>
        <H3>Appearance</H3>
        <Muted>Customize the appearance of the app. Automatically switch between day and night themes.</Muted>
    </div>
    <Separator />

    <form onsubmit={onSave}>
        <RadioGroup.Root class="grid max-w-xl grid-cols-3 gap-8 pt-2" orientation="horizontal" value="light">
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

        <div class="pt-2">
            <Button type="submit">Save</Button>
        </div>
    </form>
</div>
