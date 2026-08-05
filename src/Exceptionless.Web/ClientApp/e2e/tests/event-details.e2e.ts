import { test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

test('new user can inspect event details and exception context @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('inspect the event details tabs', async () => {
        const projectUserCountResponse = page.waitForResponse((response) => {
            const url = new URL(response.url());
            return (
                url.pathname.includes(`/api/v2/projects/${e2eScenario.projectId}/events/count`) && url.searchParams.get('aggregations') === 'cardinality:user'
            );
        });

        await journey.expectEventDetails();

        const countResponse = await projectUserCountResponse;
        const countResponseUrl = new URL(countResponse.url());
        test.expect(countResponse.ok()).toBe(true);
        test.expect(countResponseUrl.searchParams.get('time')).toBe('[now-7d TO now]');
    });
});
