import { isEmailAddressTaken } from './api.svelte';

/**
 * Validates that an email address is not already in use.
 * Returns an error message if the email is taken, undefined if available.
 * Network errors are handled gracefully (returns undefined to not block the user).
 *
 * @param email The email address to validate
 * @returns Error message if email is taken, undefined if available or on error
 */
export async function validateEmailAvailability(email: string): Promise<string | undefined> {
    // Skip validation for empty or obviously invalid emails
    // Let the Zod schema handle basic format validation
    if (!email || !email.includes('@')) {
        return;
    }

    try {
        const isTaken = await isEmailAddressTaken(email);
        if (isTaken) {
            return 'A user with this email address already exists.';
        }

        return;
    } catch {
        // Network error - don't block the user, let server-side validation handle it
        return;
    }
}
