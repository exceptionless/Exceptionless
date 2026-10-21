import { describe, expect, it } from 'vitest';

import { getFirstName, suggestOrganizationName } from './organization-name';

describe('suggestOrganizationName', () => {
    it.each([
        ['jane@acme.com', 'Acme'],
        ['jane@ACME.io', 'Acme'],
        ['jane@mail.acme.co.uk', 'Acme'],
        ['jane@blue-sky-labs.dev', 'Blue Sky Labs'],
        ['jane@eng.contoso.com.au', 'Contoso']
    ])('uses the company domain for %s', (email, expected) => {
        expect(suggestOrganizationName(email, 'Jane Doe')).toBe(expected);
    });

    it.each(['jane@gmail.com', 'jane@outlook.com', 'jane@yahoo.co.jp', 'jane@icloud.com', 'jane@proton.me', 'jane@hotmail.fr'])(
        'uses the first name for the personal mailbox %s',
        (email) => {
            expect(suggestOrganizationName(email, '  Jane   Doe ')).toBe("Jane's Team");
        }
    );

    it('falls back when the address or name is missing', () => {
        expect(suggestOrganizationName('not-an-email', 'Jane')).toBe("Jane's Team");
        expect(suggestOrganizationName('jane@localhost', 'Jane')).toBe("Jane's Team");
        expect(suggestOrganizationName(undefined, undefined)).toBe('My Team');
        expect(suggestOrganizationName('jane@gmail.com', '   ')).toBe('My Team');
    });
});

describe('getFirstName', () => {
    it('returns the first word of the name', () => {
        expect(getFirstName(' Ada Lovelace ')).toBe('Ada');
        expect(getFirstName('')).toBeUndefined();
        expect(getFirstName(null)).toBeUndefined();
    });
});
