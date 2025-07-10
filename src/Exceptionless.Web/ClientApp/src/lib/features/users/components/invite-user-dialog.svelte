<script lang="ts">
    import { Button } from '$comp/ui/button';
    import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { addOrganizationUser } from '$features/organizations/api.svelte';
    import LoaderCircle from '@lucide/svelte/icons/loader-circle';
    import { toast } from 'svelte-sonner';
    import { z } from 'zod';

    interface InviteUserDialogProps {
        onOpenChange: (open: boolean) => void;
        open: boolean;
        organizationId: string;
    }

    const { onOpenChange, open, organizationId }: InviteUserDialogProps = $props();

    let email = $state('');
    let emailError = $state('');

    const emailSchema = z.string().email('Please enter a valid email address');

    const inviteUserMutation = addOrganizationUser({
        route: {
            get email() {
                return email;
            },
            get organizationId() {
                return organizationId;
            }
        }
    });

    function validateEmail() {
        const result = emailSchema.safeParse(email);
        if (!result.success) {
            emailError = result.error.errors[0]?.message || 'Invalid email';
            return false;
        }
        emailError = '';
        return true;
    }

    async function handleInviteUser() {
        if (!validateEmail()) {
            return;
        }

        try {
            await inviteUserMutation.mutateAsync();
            toast.success('User invited successfully');
            onOpenChange(false);
            email = '';
            emailError = '';
        } catch (error) {
            console.error('Error inviting user:', error);
            toast.error('Failed to invite user. Please try again.');
        }
    }

    function handleClose() {
        onOpenChange(false);
        email = '';
        emailError = '';
    }
</script>

<Dialog {open} onOpenChange={handleClose}>
    <DialogContent class="sm:max-w-md">
        <DialogHeader>
            <DialogTitle>Invite User</DialogTitle>
            <DialogDescription>Enter the email address of the user you want to invite to this organization.</DialogDescription>
        </DialogHeader>
        <div class="space-y-4">
            <div class="space-y-2">
                <Label for="email">Email Address</Label>
                <Input
                    id="email"
                    type="email"
                    placeholder="user@example.com"
                    bind:value={email}
                    onblur={validateEmail}
                    class={emailError ? 'border-destructive' : ''}
                />
                {#if emailError}
                    <p class="text-destructive text-sm" role="alert">{emailError}</p>
                {/if}
            </div>
        </div>
        <DialogFooter>
            <Button variant="outline" onclick={handleClose}>Cancel</Button>
            <Button onclick={handleInviteUser} disabled={!email || !!emailError || inviteUserMutation.isPending}>
                {#if inviteUserMutation.isPending}
                    <LoaderCircle class="mr-2 size-4 animate-spin" />
                    Inviting...
                {:else}
                    Invite User
                {/if}
            </Button>
        </DialogFooter>
    </DialogContent>
</Dialog>
