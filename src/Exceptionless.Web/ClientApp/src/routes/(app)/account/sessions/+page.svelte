<script lang="ts">
    import DateTime from '$comp/formatters/date-time.svelte';
    import { H3, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import Laptop from '@lucide/svelte/icons/laptop';
    import Smartphone from '@lucide/svelte/icons/smartphone';
</script>

<div class="space-y-6">
    <div>
        <H3>Web sessions</H3>
        <Muted>This is a list of devices that have logged into your account. Revoke any sessions that you do not recognize.</Muted>
    </div>
    <Separator />

    <ul class="divide-border divide-y">
        <li class="pb-4">
            <div class="flex items-center space-x-4">
                <Laptop />
                <div class="min-w-0 flex-1">
                    <P class="font-semibold">Wisconsin</P>
                    <Muted>Signed in on <DateTime value={new Date()}></DateTime></Muted>
                </div>
                <div class="inline-flex items-center">
                    <Button aria-label="Revoke user session" variant="outline">Revoke</Button>
                </div>
            </div>
        </li>
        <li class="py-4">
            <div class="flex items-center space-x-4">
                <Smartphone />
                <div class="min-w-0 flex-1">
                    <P class="font-semibold">Texas</P>
                    <Muted>Signed in on <DateTime value={new Date()}></DateTime></Muted>
                </div>
                <div class="inline-flex items-center">
                    <Button aria-label="Revoke user session" variant="outline">Revoke</Button>
                </div>
            </div>
        </li>
    </ul>
</div>
