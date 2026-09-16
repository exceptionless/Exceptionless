# Spec: Organizations, Projects, Users & Auth

## ADDED Requirements

### Requirement: Budget alert settings belong to an organization

Budget alert settings belong to an organization and may be changed only by users authorized to update that organization.

#### Scenario: Authorized organization user updates budget alerts

Given a user is authorized to update an organization
When the user updates budget alert settings
Then the update must be allowed if the payload is valid.

#### Scenario: Unauthorized user cannot update budget alerts

Given a user is not authorized to update an organization
When the user attempts to update budget alert settings
Then the API must reject the operation according to existing organization authorization behavior.

## ADDED Requirements

### Requirement: Budget alert email recipients respect user preferences

Budget alert email recipients must respect existing user email verification and email notification preferences.

#### Scenario: User email notifications disabled

Given a user belongs to an organization and has email notifications disabled
When a budget alert is sent
Then the user must not receive the budget alert email.

#### Scenario: User email unverified

Given a user belongs to an organization and the user's email address is not verified
When a budget alert is sent
Then the user must not receive the budget alert email.

#### Scenario: Verified user with email notifications enabled

Given a user belongs to an organization with a verified email and notifications enabled
When a budget alert is sent
Then the user may receive the budget alert email.

## ADDED Requirements

### Requirement: Project event budget authorization follows project authorization

A project event budget belongs to a project and is scoped by the project's owning organization. Only users authorized to update the project may configure its event budget.

#### Scenario: Authorized project user updates project event budget

Given a user is authorized to update a project
When the user updates the project's event budget
Then the update must be allowed if the payload is valid.

#### Scenario: Unauthorized user cannot update project event budget

Given a user is not authorized to update a project
When the user attempts to update the project's event budget
Then the API must reject the operation according to existing project authorization behavior.

#### Scenario: Project event budget cannot grant organization capacity

Given a user configures a project event budget
When events are submitted for the project
Then the project event budget must not allow accepted event usage beyond the organization's effective allowance.

## ADDED Requirements

### Requirement: Budget controls preserve existing token and auth behavior

Budget alert settings, smart project throttling, and project event budget configuration must not change API key authentication, token scopes, or user authorization roles.

#### Scenario: Existing project token behavior is preserved

Given an existing project API key has client scope and the project has no configured event budget
When the key is used for event submission
Then token authentication and scope behavior must match existing behavior.

#### Scenario: Project smart throttling does not disable API key

Given a project is under smart throttling
When a project API key is used for a non-ingest API route it is authorized to access
Then the API key must not be considered disabled solely because the project is smart-throttled.

#### Scenario: Project budget does not disable API key

Given a project reaches its project event budget
When a project API key is used for a non-ingest API route it is authorized to access
Then the API key must not be considered disabled solely because the project reached its event budget.

#### Scenario: Disabled or suspended token remains rejected

Given a project has remaining project event budget and the API key is disabled or suspended
When an event is submitted with that API key
Then the event must still be rejected due to token authentication state.

## ADDED Requirements

### Requirement: Project event budget MUST NOT preclude future token-level caps

Project event budget and smart throttling behavior MUST NOT preclude future API-key/token-level ingest caps.

#### Scenario: Future token cap is absent

Given token-level ingest caps are not implemented
When a project event budget or smart throttling is evaluated
Then enforcement must consider organization and project scopes only.

#### Scenario: Project budget remains independent of token count

Given a project has multiple API keys and a configured project event budget
When events are submitted using different API keys for that project
Then all accepted events for the project must count against the same project event budget.
