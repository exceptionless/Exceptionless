/**
 * Sample data for Storybook email template previews.
 * Uses the `handlebars` npm package to evaluate templates, which matches
 * HandlebarsDotNet's behaviour for the constructs used in these templates:
 *   {{token}}, {{../token}} (parent scope in each loops)
 *   {{#if}}...{{else}}...{{/if}}, {{#unless}}...{{/unless}}
 *   {{#each array}}...{{@index}}...{{@key}}...{{this}}...{{/each}}
 */
import Handlebars from 'handlebars';

export type EachItem = Record<string, unknown>;
export type TokenValue = string | number | boolean | EachItem[] | Record<string, unknown>;
export type TokenData = Record<string, TokenValue>;

export function fillTokens(html: string, tokens: TokenData): string {
    const template = Handlebars.compile(html);
    return template(tokens);
}

// ─── Sample Data ─────────────────────────────────────────────────────────────

// Use the local dev URL so reviewers never accidentally hit production from email previews.
const BASE_URL = 'http://localhost:7110';
const ORG_ID = 'org_123456';
const PROJECT_ID = 'proj_abcdef';

const sharedTokens: TokenData = {
    BaseUrl: BASE_URL,
    OrganizationId: ORG_ID,
    OrganizationName: 'Acme Corp',
    ProjectId: PROJECT_ID,
    ProjectName: 'My Web App',
    UserFullName: 'Blake Niemyjski',
    UserEmail: 'blake@example.com'
};

export const passwordResetTokens: TokenData = {
    ...sharedTokens,
    Subject: 'Reset your Exceptionless password',
    UserPasswordResetToken: 'abc123resettoken'
};

export const emailVerifyTokens: TokenData = {
    ...sharedTokens,
    Subject: 'Verify your Exceptionless email address',
    UserVerifyEmailAddressToken: 'xyz789verifytoken'
};

export const eventNoticeTokens: TokenData = {
    ...sharedTokens,
    Subject: 'New Critical Event: System.NullReferenceException',
    EventId: 'evt_9876543',
    StackId: 'stk_111222',
    IsNew: 'true',
    IsCritical: 'true',
    IsRegression: '',
    TotalOccurrences: '47',
    HasUserInfo: 'true',
    UserDisplayName: 'Jane Smith',
    UserEmail: 'jane@example.com',
    UserEmailHref: 'mailto:jane%40example.com?body=User%20reported%3A%20app%20crashed%20on%20checkout',
    UserDescription: 'User reported: app crashed on checkout',
    HasSubmittedEvents: 'true',
    // Fields is a plain object — {{#each Fields}} iterates with @key = property name, this = value
    Fields: {
        'Error.Message': 'Object reference not set to an instance of an object.',
        'Error.Type': 'System.NullReferenceException',
        'Error.StackTrace': 'at MyApp.Controllers.CheckoutController.ProcessOrder() in CheckoutController.cs:line 42'
    }
};

export const regressedEventNoticeTokens: TokenData = {
    ...eventNoticeTokens,
    Subject: 'Critical Event Regression: System.NullReferenceException',
    IsNew: '',
    IsRegression: true,
    TotalOccurrences: 48
};

export const reoccurredEventNoticeTokens: TokenData = {
    ...eventNoticeTokens,
    Subject: 'Event Reoccurred: System.NullReferenceException',
    IsNew: '',
    IsCritical: '',
    IsRegression: '',
    HasUserInfo: '',
    Fields: [],
    UserDisplayName: '',
    UserEmail: '',
    UserEmailHref: '',
    UserDescription: '',
    TotalOccurrences: 48
};

export const dailySummaryTokens: TokenData = {
    ...sharedTokens,
    Subject: 'Daily Summary for My Web App — May 27, 2026',
    StartDate: 'May 27, 2026',
    HasSubmittedEvents: 'true',
    Count: '1,247',
    Unique: '38',
    New: '5',
    Blocked: '', // falsy = no discarded events → 3-column layout
    Fixed: '2',
    IsFreePlan: '',
    MostFrequent: [
        {
            StackId: 'stk_111',
            TypeName: 'NullReferenceException',
            Title: 'Object reference not set to an instance of an object.',
            IsRegressed: ''
        },
        {
            StackId: 'stk_222',
            TypeName: 'HttpRequestException',
            Title: 'Connection refused (localhost:5432)',
            IsRegressed: ''
        },
        {
            StackId: 'stk_333',
            TypeName: 'ArgumentNullException',
            Title: "Value cannot be null (Parameter 'userId')",
            IsRegressed: 'true'
        }
    ],
    Newest: [
        {
            StackId: 'stk_444',
            TypeName: 'InvalidOperationException',
            Title: 'Sequence contains no elements',
            IsRegressed: ''
        },
        {
            StackId: 'stk_555',
            TypeName: '',
            Title: 'Unhandled exception in background task processor',
            IsRegressed: ''
        }
    ]
};

export const blockedDailySummaryTokens: TokenData = {
    ...dailySummaryTokens,
    Subject: 'Daily Summary with discarded events',
    Blocked: 123,
    IsFreePlan: true
};

export const unconfiguredDailySummaryTokens: TokenData = {
    ...dailySummaryTokens,
    Subject: 'Project setup reminder',
    HasSubmittedEvents: '',
    Count: 0,
    Unique: 0,
    New: 0,
    Blocked: 0,
    Fixed: 0,
    MostFrequent: [],
    Newest: []
};

export const organizationAddedTokens: TokenData = {
    ...sharedTokens,
    Subject: 'You have been added to Acme Corp on Exceptionless'
};

export const organizationInvitedTokens: TokenData = {
    ...sharedTokens,
    Subject: "blake@example.com has invited you to join Acme Corp's Exceptionless organization!",
    InviteToken: 'inv_abc123xyz'
};

export const organizationNoticeTokens: TokenData = {
    ...sharedTokens,
    Subject: 'Exceptionless: Acme Corp has been throttled',
    IsOverMonthlyLimit: '',
    IsOverHourlyLimit: 'true',
    ThrottledUntil: '2026-05-27 15:00:00'
};

export const monthlyOrganizationNoticeTokens: TokenData = {
    ...organizationNoticeTokens,
    Subject: 'Exceptionless: Acme Corp reached its monthly plan limit',
    IsOverMonthlyLimit: true,
    IsOverHourlyLimit: ''
};

export const paymentFailedTokens: TokenData = {
    ...sharedTokens,
    Subject: 'Exceptionless: Payment failed for Acme Corp'
};
