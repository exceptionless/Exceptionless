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
        // Act
        const name = suggestOrganizationName(email, 'Jane Doe');

        // Assert
        expect(name).toBe(expected);
    });

    it.each(['jane@gmail.com', 'jane@outlook.com', 'jane@yahoo.co.jp', 'jane@icloud.com', 'jane@proton.me', 'jane@hotmail.fr'])(
        'uses the first name for the personal mailbox %s',
        (email) => {
            // Act
            const name = suggestOrganizationName(email, '  Jane   Doe ');

            // Assert
            expect(name).toBe("Jane's Team");
        }
    );

    it.each([
        ['not-an-email', 'Jane', "Jane's Team"],
        ['jane@localhost', 'Jane', "Jane's Team"],
        [undefined, undefined, 'My Team'],
        ['jane@gmail.com', '   ', 'My Team']
    ])('falls back for address %s and name %j', (email, fullName, expected) => {
        // Act
        const name = suggestOrganizationName(email, fullName);

        // Assert
        expect(name).toBe(expected);
    });
});

describe('getFirstName', () => {
    it.each([
        [' Ada Lovelace ', 'Ada'],
        ['', undefined],
        [null, undefined]
    ])('returns the first word of %j', (fullName, expected) => {
        // Act
        const firstName = getFirstName(fullName);

        // Assert
        expect(firstName).toBe(expected);
    });
});
