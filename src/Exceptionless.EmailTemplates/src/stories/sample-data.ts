/**
 * Sample data for Storybook email template previews.
 * Replaces Handlebars tokens with realistic dummy values so templates
 * render without a running backend.
 */

const BASE_URL = 'https://be.exceptionless.io';
const ORG_ID = 'org_123456';
const PROJECT_ID = 'proj_abcdef';

/**
 * Replaces {{Token}} and {{#if/each/unless}} blocks with sample values.
 * Block helpers are removed (we can't evaluate them here) so all conditional
 * content is visible in the preview.
 */
export function fillTokens(html: string, tokens: Record<string, string>): string {
    // Strip block helpers so the content inside them is always shown
    let result = html
        .replace(/\{\{#if[^}]*\}\}/g, '')
        .replace(/\{\{else if[^}]*\}\}/g, '')
        .replace(/\{\{else\}\}/g, '')
        .replace(/\{\{\/if\}\}/g, '')
        .replace(/\{\{#each[^}]*\}\}/g, '')
        .replace(/\{\{\/each\}\}/g, '')
        .replace(/\{\{#unless[^}]*\}\}/g, '')
        .replace(/\{\{\/unless\}\}/g, '');

    // Replace remaining tokens
    return result.replace(/\{\{([^}]+)\}\}/g, (_, key: string) => tokens[key.trim()] ?? `[${key.trim()}]`);
}

export const sharedTokens: Record<string, string> = {
    BaseUrl: BASE_URL,
    OrganizationId: ORG_ID,
    OrganizationName: 'Acme Corp',
    ProjectId: PROJECT_ID,
    ProjectName: 'My Web App',
    UserFullName: 'Blake Niemyjski',
    UserEmail: 'blake@example.com'
};

export const passwordResetTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'Reset your Exceptionless password',
    UserPasswordResetToken: 'abc123resettoken'
};

export const emailVerifyTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'Verify your Exceptionless email address',
    UserVerifyEmailAddressToken: 'xyz789verifytoken'
};

export const eventNoticeTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'New Critical Event: System.NullReferenceException',
    EventId: 'evt_9876543',
    StackId: 'stk_111222',
    IsNew: 'true',
    IsCritical: 'true',
    IsRegression: 'false',
    TotalOccurrences: '47',
    HasUserInfo: 'true',
    UserDisplayName: 'Jane Smith',
    UserDescription: 'User reported: app crashed on checkout',
    HasSubmittedEvents: 'true',
    Fields: 'true',
    '@key': 'Error.Message',
    this: 'Object reference not set to an instance of an object.'
};

export const dailySummaryTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'Daily Summary for My Web App — May 27, 2026',
    StartDate: 'May 27, 2026',
    HasSubmittedEvents: 'true',
    Count: '1,247',
    Unique: '38',
    New: '5',
    Blocked: '0',
    Fixed: '2',
    IsFreePlan: 'false',
    StackId: 'stk_111222',
    TypeName: 'NullReferenceException',
    Title: 'Object reference not set to an instance of an object.',
    IsRegressed: 'false',
    '../BaseUrl': BASE_URL
};

export const organizationAddedTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'You have been added to Acme Corp on Exceptionless'
};

export const organizationInvitedTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: "blake@example.com has invited you to join Acme Corp's Exceptionless organization!",
    InviteToken: 'inv_abc123xyz'
};

export const organizationNoticeTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'Exceptionless: Acme Corp has been throttled',
    IsOverMonthlyLimit: 'false',
    IsOverHourlyLimit: 'true',
    ThrottledUntil: '2026-05-27 15:00:00'
};

export const paymentFailedTokens: Record<string, string> = {
    ...sharedTokens,
    Subject: 'Exceptionless: Payment failed for Acme Corp'
};
