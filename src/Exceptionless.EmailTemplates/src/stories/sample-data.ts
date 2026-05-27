/**
 * Sample data for Storybook email template previews.
 * Replaces Handlebars tokens with realistic values so templates render correctly.
 *
 * Uses a proper Handlebars-like evaluator supporting:
 *   {{token}}, {{../token}} (parent scope)
 *   {{#if token}}...{{else if token2}}...{{else}}...{{/if}}
 *   {{#unless token}}...{{/unless}}
 *   {{#each array}}...{{@index}}...{{@key}}...{{this}}...{{/each}}
 */

/** A single item in a {{#each}} loop (all properties are strings for simplicity). */
export type EachItem = Record<string, string>;

/** Token value — either a scalar string or an array for {{#each}} loops. */
export type TokenValue = string | EachItem[];

/** The full token context passed to fillTokens. */
export type TokenData = Record<string, TokenValue>;

// ─── Evaluator ──────────────────────────────────────────────────────────────

function isTruthy(val: TokenValue | undefined): boolean {
    if (val === undefined || val === null) return false;
    if (Array.isArray(val)) return val.length > 0;
    // '0', '', 'false' are falsy; everything else is truthy
    return val !== '' && val !== 'false' && val !== '0';
}

/** Entry point — render a Handlebars template against the given context. */
export function fillTokens(html: string, tokens: TokenData): string {
    return evalTemplate(html, tokens);
}

function evalTemplate(tpl: string, ctx: TokenData): string {
    let i = 0;
    let out = '';

    while (i < tpl.length) {
        const next = tpl.indexOf('{{', i);
        if (next === -1) {
            out += tpl.slice(i);
            break;
        }

        out += tpl.slice(i, next);

        const closeIdx = tpl.indexOf('}}', next + 2);
        if (closeIdx === -1) {
            out += tpl.slice(next);
            break;
        }

        const tag = tpl.slice(next + 2, closeIdx).trim();
        const afterTag = closeIdx + 2;

        if (tag.startsWith('#if ') || tag.startsWith('#unless ')) {
            const isUnless = tag.startsWith('#unless ');
            const key = tag.slice(isUnless ? 8 : 4).trim();
            const condition = isUnless ? !isTruthy(ctx[key]) : isTruthy(ctx[key]);
            const { branches, elseConditions, nextIdx } = collectIfBranches(tpl, afterTag);

            let selected = condition ? (branches[0] ?? '') : '';
            if (!condition) {
                for (let b = 0; b < elseConditions.length; b++) {
                    const cond = elseConditions[b];
                    if (cond === null || isTruthy(ctx[cond])) {
                        selected = branches[b + 1] ?? '';
                        break;
                    }
                }
            }
            out += evalTemplate(selected, ctx);
            i = nextIdx;
        } else if (tag.startsWith('#each ')) {
            const key = tag.slice(6).trim();
            const { body, nextIdx } = collectBlock(tpl, afterTag, 'each');
            const items = ctx[key];
            if (Array.isArray(items)) {
                out += items
                    .map((item, index) => {
                        const itemCtx: TokenData = {
                            ...ctx,
                            ...item,
                            '@index': String(index)
                        };
                        return evalTemplate(body, itemCtx);
                    })
                    .join('');
            }
            i = nextIdx;
        } else if (tag.startsWith('/') || tag === 'else' || tag.startsWith('else ')) {
            // Should never reach here at top level — skip stray closing/else tags
            i = afterTag;
        } else {
            // Simple token — strip leading ../ for parent-scope access ({{../BaseUrl}} → BaseUrl)
            const key = tag.startsWith('../') ? tag.slice(3) : tag;
            const val = ctx[key];
            out += val !== undefined ? String(val) : `[${key}]`;
            i = afterTag;
        }
    }

    return out;
}

interface IfBranchResult {
    /** branches[0] = true branch; branches[1..n] = else/else-if branches */
    branches: string[];
    /** null = plain {{else}}, 'key' = {{else if key}}; length = branches.length - 1 */
    elseConditions: (string | null)[];
    /** index in the template immediately after the closing {{/if}} or {{/unless}} */
    nextIdx: number;
}

function collectIfBranches(tpl: string, start: number): IfBranchResult {
    let i = start;
    let depth = 0;
    let current = '';
    const branches: string[] = [];
    const elseConditions: (string | null)[] = [];

    while (i < tpl.length) {
        const next = tpl.indexOf('{{', i);
        if (next === -1) {
            current += tpl.slice(i);
            i = tpl.length;
            break;
        }

        const closeIdx = tpl.indexOf('}}', next + 2);
        if (closeIdx === -1) {
            current += tpl.slice(i);
            i = tpl.length;
            break;
        }

        const tag = tpl.slice(next + 2, closeIdx).trim();

        if (tag.startsWith('#if ') || tag.startsWith('#unless ') || tag.startsWith('#each ')) {
            depth++;
            current += tpl.slice(i, closeIdx + 2);
            i = closeIdx + 2;
        } else if ((tag === '/if' || tag === '/unless') && depth === 0) {
            current += tpl.slice(i, next); // literal content before {{/if}}
            branches.push(current);
            return { branches, elseConditions, nextIdx: closeIdx + 2 };
        } else if ((tag === '/if' || tag === '/unless' || tag === '/each') && depth > 0) {
            depth--;
            current += tpl.slice(i, closeIdx + 2);
            i = closeIdx + 2;
        } else if (tag === 'else' && depth === 0) {
            current += tpl.slice(i, next); // literal before {{else}}
            branches.push(current);
            elseConditions.push(null);
            current = '';
            i = closeIdx + 2;
        } else if (tag.startsWith('else if ') && depth === 0) {
            current += tpl.slice(i, next); // literal before {{else if}}
            branches.push(current);
            elseConditions.push(tag.slice(8).trim());
            current = '';
            i = closeIdx + 2;
        } else {
            current += tpl.slice(i, closeIdx + 2);
            i = closeIdx + 2;
        }
    }

    branches.push(current);
    return { branches, elseConditions, nextIdx: i };
}

interface BlockResult {
    body: string;
    nextIdx: number;
}

function collectBlock(tpl: string, start: number, blockType: string): BlockResult {
    let i = start;
    let depth = 0;
    let body = '';

    while (i < tpl.length) {
        const next = tpl.indexOf('{{', i);
        if (next === -1) {
            body += tpl.slice(i);
            i = tpl.length;
            break;
        }

        const closeIdx = tpl.indexOf('}}', next + 2);
        if (closeIdx === -1) {
            body += tpl.slice(i);
            i = tpl.length;
            break;
        }

        const tag = tpl.slice(next + 2, closeIdx).trim();

        if (tag.startsWith('#if ') || tag.startsWith('#unless ') || tag.startsWith('#each ')) {
            depth++;
            body += tpl.slice(i, closeIdx + 2);
            i = closeIdx + 2;
        } else if (tag === `/${blockType}` && depth === 0) {
            body += tpl.slice(i, next); // literal before closing tag
            return { body, nextIdx: closeIdx + 2 };
        } else if ((tag === '/if' || tag === '/unless' || tag === '/each') && depth > 0) {
            depth--;
            body += tpl.slice(i, closeIdx + 2);
            i = closeIdx + 2;
        } else {
            body += tpl.slice(i, closeIdx + 2);
            i = closeIdx + 2;
        }
    }

    return { body, nextIdx: i };
}

// ─── Sample Data ─────────────────────────────────────────────────────────────

const BASE_URL = 'https://be.exceptionless.io';
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
    UserDescription: 'User reported: app crashed on checkout',
    HasSubmittedEvents: 'true',
    // Fields is a dictionary iterated with {{#each Fields}} — each item provides @key and this
    Fields: [
        { '@key': 'Error.Message', this: 'Object reference not set to an instance of an object.' },
        { '@key': 'Error.Type', this: 'System.NullReferenceException' },
        {
            '@key': 'Error.StackTrace',
            this: 'at MyApp.Controllers.CheckoutController.ProcessOrder() in CheckoutController.cs:line 42'
        }
    ]
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

export const paymentFailedTokens: TokenData = {
    ...sharedTokens,
    Subject: 'Exceptionless: Payment failed for Acme Corp'
};
