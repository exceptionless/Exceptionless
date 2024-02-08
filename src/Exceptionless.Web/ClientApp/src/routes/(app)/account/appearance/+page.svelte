<script lang="ts">
    import { Label } from '$comp/ui/label';
    import * as RadioGroup from '$comp/ui/radio-group';

    import { Separator } from '$comp/ui/separator';
    import { Button } from '$comp/ui/button';

    import H3 from '$comp/typography/H3.svelte';
    import Muted from '$comp/typography/Muted.svelte';
    import ThemePreview from './(components)/ThemePreview.svelte';

    async function onSave() {}
</script>

<div class="space-y-6">
    <div>
        <H3>Appearance</H3>
        <Muted>Customize the appearance of the app. Automatically switch between day and night themes.</Muted>
    </div>
    <Separator />

    <form on:submit|preventDefault={onSave}>
        <RadioGroup.Root value="light" class="grid max-w-xl grid-cols-3 gap-8 pt-2" orientation="horizontal">
            <Label for="light" class="[&:has([data-state=checked])>div]:border-primary">
                <RadioGroup.Item id="light" value="light" class="sr-only" />
                <ThemePreview mode="light" />
                <div class="pt-2 text-center">Light</div>
            </Label>
            <Label for="dark" class="[&:has([data-state=checked])>div]:border-primary">
                <RadioGroup.Item id="dark" value="dark" class="sr-only" />
                <ThemePreview mode="dark" />
                <div class="pt-2 text-center">Dark</div>
            </Label>
            <Label for="system" class="[&:has([data-state=checked])>div]:border-primary">
                <RadioGroup.Item id="system" value="system" class="sr-only" />
                <ThemePreview mode="system" />
                <div class="pt-2 text-center">System</div>
            </Label>
        </RadioGroup.Root>

        <div class="pt-2">
            <Button type="submit">Save</Button>
        </div>
    </form>
</div>
