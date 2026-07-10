import { test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

test('new user can mark an open stack fixed from event details @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('mark the stack fixed through the UI', async () => {
        await journey.markStackFixed();
    });
});
