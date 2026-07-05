import { test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

test('new user can sign up and configure a first project @signup', async ({ e2eApi, page }, testInfo) => {
    const journey = new ExceptionlessE2EJourney(page, e2eApi, testInfo);

    try {
        await test.step('sign up and create the organization in the UI', async () => {
            await journey.signUpAndCreateOrganization();
        });

        await test.step('create the first project and verify the configure token in the UI', async () => {
            await journey.createFirstProjectAndVerifyConfigureToken();
        });
    } finally {
        await journey.cleanup();
    }
});
