/** Mailbox providers whose domain says nothing about the user's company. */
const PERSONAL_EMAIL_DOMAINS = new Set([
    '163.com',
    'aol.com',
    'fastmail.com',
    'gmail.com',
    'gmx.com',
    'gmx.de',
    'gmx.net',
    'googlemail.com',
    'hey.com',
    'hotmail.co.uk',
    'hotmail.com',
    'icloud.com',
    'live.com',
    'mac.com',
    'mail.com',
    'mail.ru',
    'me.com',
    'msn.com',
    'outlook.com',
    'pm.me',
    'proton.me',
    'protonmail.com',
    'qq.com',
    'tutanota.com',
    'web.de',
    'yahoo.co.uk',
    'yahoo.com',
    'yandex.com',
    'yandex.ru',
    'ymail.com',
    'zoho.com'
]);

/** Second-level labels used under country domains, such as acme.co.uk. */
const SECOND_LEVEL_LABELS = new Set(['ac', 'co', 'com', 'edu', 'gov', 'net', 'or', 'org']);

export function getFirstName(fullName: null | string | undefined): string | undefined {
    return fullName?.trim().split(/\s+/)[0] || undefined;
}

/**
 * Suggests an organization name: "Acme" for someone@acme.com, or "{FirstName}'s Team" for
 * personal mailboxes and unrecognizable addresses. The suggestion stays editable.
 */
export function suggestOrganizationName(emailAddress: null | string | undefined, fullName: null | string | undefined): string {
    const domain = emailAddress?.trim().toLowerCase().split('@')[1];
    const companyName = domain && !isPersonalEmailDomain(domain) ? getCompanyNameFromDomain(domain) : undefined;
    if (companyName) {
        return companyName;
    }

    const firstName = getFirstName(fullName);
    return firstName ? `${firstName}'s Team` : 'My Team';
}

function getCompanyNameFromDomain(domain: string): string | undefined {
    const labels = domain.split('.').filter(Boolean);
    if (labels.length < 2) {
        return undefined;
    }

    // Drop the top-level domain, then a second-level label such as "co" in acme.co.uk.
    labels.pop();
    const last = labels.at(-1);
    if (labels.length > 1 && last && SECOND_LEVEL_LABELS.has(last)) {
        labels.pop();
    }

    const name = labels.at(-1);
    if (!name || /^\d+$/.test(name)) {
        return undefined;
    }

    return name
        .split(/[-_]+/)
        .filter(Boolean)
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');
}

function isPersonalEmailDomain(domain: string): boolean {
    return PERSONAL_EMAIL_DOMAINS.has(domain) || /^(yahoo|hotmail|outlook|live|gmx)\.[a-z.]+$/.test(domain);
}
