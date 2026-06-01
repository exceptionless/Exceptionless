# Spec: API Compatibility

## MODIFIED Requirements

### Requirement: Event submission MUST preserve existing organization overage behavior

Exceptionless event submission MUST preserve existing organization overage and API authentication behavior while adding automatic smart project throttling and optional project event budgets.

#### Scenario: Organization overage status remains unchanged

Given an organization has no remaining event allowance
When an event is submitted for a project in that organization
Then the response status code must remain the existing organization overage status code.

#### Scenario: Project smart throttling preserves queued event post compatibility

Given an organization has remaining event allowance and a project is under smart throttling
When an event post is submitted through the normal queued event submission path
Then the HTTP response should not be changed solely to report project throttling.

#### Scenario: Generic API throttle behavior is unchanged

Given API request throttling is enabled
When non-event API requests are made
Then existing generic API request throttle behavior must remain unchanged.

#### Scenario: Existing client API keys continue to authenticate

Given an existing project API key is used for event submission and the project has no configured event budget
When an event is submitted
Then authentication and authorization behavior must match existing behavior.

### Requirement: Rate-limit headers MUST NOT be repurposed for project monthly controls

Existing generic API throttle response headers MUST NOT be redefined to mean project monthly event budgets or smart project throttling.

#### Scenario: Generic API throttle headers remain request-rate headers

Given generic API throttling emits request-rate headers
When usage budget controls are added
Then those headers must continue to represent generic API request throttling behavior.

## ADDED Requirements

### Requirement: Organization API exposes budget alert settings

The organization API may expose and update budget alert settings as an additive organization field.

#### Scenario: Organization response includes budget alert settings

Given an organization has budget alert settings
When the organization is returned from the API
Then the response must include budget_alert_settings.

#### Scenario: Existing organization response remains compatible

Given an existing organization has no budget alert settings
When the organization is returned from the API
Then budget_alert_settings may be null and existing fields must remain unchanged.

#### Scenario: Authorized user updates budget alert settings

Given a user is authorized to update an organization
When the user updates budget alert settings with valid thresholds
Then the API must persist the settings and include them in the response.

#### Scenario: Invalid budget alert settings rejected

Given a user is authorized to update an organization
When the user submits invalid budget alert settings
Then the API must reject the request with a validation error.

## ADDED Requirements

### Requirement: Budget alert emails are asynchronous side effects

Budget alert emails are side effects of accepted usage threshold crossing and must not change event submission success responses.

#### Scenario: Event crosses budget alert threshold

Given budget alerts are enabled and an accepted event causes usage to cross a threshold
When the event submission completes
Then the event submission response must remain the normal accepted response.

#### Scenario: Budget alert failure does not reject event

Given budget alerts are enabled and budget alert queuing fails
When an event is submitted
Then the event must not be rejected solely because budget alert queuing failed.

## ADDED Requirements

### Requirement: Project API accepts event budget configuration

The project update API may accept an optional project event budget configuration as an additive project field.

#### Scenario: Clear project event budget

Given an authorized user can update a project
When the user patches the project with ingest_limit set to null
Then the project event budget must be cleared.

#### Scenario: Set fixed project event budget

Given an authorized user can update a project
When the user patches the project with a fixed positive ingest_limit
Then the project must persist the fixed event budget.

#### Scenario: Set percentage project event budget

Given an authorized user can update a project and the organization has finite allowance
When the user patches the project with a valid percentage event budget
Then the project must persist the percentage event budget.

#### Scenario: Invalid fixed limit is rejected

Given an authorized user can update a project
When the user patches the project with a fixed event budget less than or equal to 0
Then the API must reject the request with a validation error.

#### Scenario: Invalid percentage limit is rejected

Given an authorized user can update a project
When the user patches the project with a percentage event budget less than or equal to 0 or greater than 100
Then the API must reject the request with a validation error.

## ADDED Requirements

### Requirement: Project response MUST include budget and throttling state

Project responses used by the UI MUST expose the configured project event budget, currently effective cap, and smart throttling state where available.

#### Scenario: Project has no event budget

Given a project has no configured event budget
When the project is returned from the API
Then ingest_limit must be null and effective_ingest_limit must be null.

#### Scenario: Project has fixed event budget

Given a project has a fixed event budget
When the project is returned from the API
Then effective_ingest_limit must contain the currently effective cap.

#### Scenario: Project is smart-throttled

Given a project is currently under smart throttling
When the project is returned from the API
Then the response should indicate smart throttling state where feasible.
