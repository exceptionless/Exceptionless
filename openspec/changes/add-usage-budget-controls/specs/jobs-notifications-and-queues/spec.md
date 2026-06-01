# Spec: Jobs, Notifications & Queues

## ADDED Requirements

### Requirement: Organizations may configure budget alert thresholds

Organizations may configure event budget alert thresholds as percentages of their effective monthly event allowance. Budget alerts are disabled by default.

#### Scenario: Existing organization has no budget alerts

Given an organization existed before budget alerts were introduced
When the organization is loaded
Then budget alerts must be treated as disabled.

#### Scenario: Enable budget alerts with thresholds

Given an authorized user can update an organization
When the user enables budget alerts with thresholds 50 and 80
Then the organization must store budget alert settings as enabled with thresholds 50 and 80.

#### Scenario: Disable budget alerts

Given an organization has enabled budget alerts
When an authorized user disables budget alerts
Then budget alert emails must no longer be sent for that organization.

### Requirement: Budget alert thresholds validate percentage values

Budget alert threshold percentages must be valid pre-overage warning thresholds.

#### Scenario: Reject zero threshold

Given an authorized user can update budget alert settings
When the user configures threshold 0
Then the API must reject the request with a validation error.

#### Scenario: Reject threshold at or above one hundred percent

Given an authorized user can update budget alert settings
When the user configures threshold 100 or above
Then the API must reject the request with a validation error.

#### Scenario: Enabled alerts require at least one threshold

Given an authorized user can update budget alert settings
When the user enables budget alerts with no thresholds
Then the API must reject the request with a validation error.

### Requirement: Budget alerts use effective organization allowance

Budget alert thresholds must be evaluated against the same effective organization event allowance used by existing usage enforcement.

#### Scenario: Threshold uses plan allowance

Given an organization has a monthly event allowance of 100000
And budget alerts are enabled for threshold 50
When the threshold event count is calculated
Then the threshold event count must be 50000.

#### Scenario: Unlimited organization has no percentage budget alert

Given an organization has unlimited event allowance
When budget alert thresholds are evaluated
Then percentage budget alerts must be inactive and no budget alert email must be sent.

### Requirement: Budget alerts send when accepted usage crosses thresholds

Budget alert emails must be triggered when accepted organization event usage crosses a configured threshold for the first time in a monthly usage period.

#### Scenario: Usage below threshold does not send alert

Given an organization has a monthly event allowance of 100000
And budget alerts are enabled for threshold 50 and accepted event usage is 49998
When one event is accepted
Then no budget alert email must be sent.

#### Scenario: Usage crossing threshold sends alert

Given an organization has a monthly event allowance of 100000
And budget alerts are enabled for threshold 50 and accepted event usage is 49999
When one event is accepted
Then a 50 percent budget alert email must be queued.

#### Scenario: Usage already above threshold does not resend alert

Given the 50 percent alert has already been sent in the current monthly usage period
When another event is accepted
Then another 50 percent budget alert email must not be queued.

#### Scenario: Large batch crosses multiple thresholds

Given budget alerts are enabled for thresholds 50 and 80 and accepted event usage is 45000
When a batch of 40000 events is accepted
Then both the 50 percent and 80 percent budget alert emails must be queued.

### Requirement: Budget alert emails are sent once per threshold per monthly usage period

Each configured threshold may generate at most one budget alert email per organization per monthly usage period.

#### Scenario: Threshold already sent in period

Given the 80 percent alert was sent earlier in the current monthly usage period
When usage remains above 80 percent
Then no additional 80 percent budget alert email must be sent.

#### Scenario: New monthly period resets sent threshold state

Given an organization received an 80 percent alert in the previous monthly usage period
When usage crosses 80 percent in the new period
Then a new 80 percent budget alert email may be sent.

### Requirement: Budget alert emails use organization email notification eligibility

Budget alert emails must be sent only to organization users who are eligible for existing organization email notices.

#### Scenario: Verified user with email notifications enabled receives alert

Given a user belongs to an organization with a verified email and email notifications enabled
When a budget alert is sent for the organization
Then the user must receive the budget alert email.

#### Scenario: Unverified user does not receive alert

Given a user belongs to an organization and does not have a verified email address
When a budget alert is sent for the organization
Then the user must not receive the budget alert email.

### Requirement: Budget alerts do not block ingestion

Budget alert processing must not block accepted event ingestion.

#### Scenario: Alert email enqueue fails

Given budget alerts are enabled and usage crosses a configured threshold
When the budget alert email cannot be queued
Then the accepted event must not be rejected solely because the alert email failed.

### Requirement: Budget alerts preserve existing overage notifications

Budget alerts must not replace or suppress existing monthly/hourly overage notifications.

#### Scenario: Existing monthly overage still sends

Given an organization reaches or exceeds its monthly event allowance
When existing monthly overage notification behavior is triggered
Then the existing monthly overage email must still be sent.

### Requirement: Budget alert emails are separate from smart throttling emails

Organization budget alerts and project smart throttling notifications must be distinct notifications.

#### Scenario: Project throttling below budget threshold

Given organization budget alerts are enabled and accepted usage has not crossed a threshold
When a project enters smart-throttled state
Then no organization budget threshold email must be sent solely because smart throttling was applied.

## ADDED Requirements

### Requirement: Smart throttling MUST send project throttling notification email

When smart throttling is applied to a project, Exceptionless MUST send an email notification to eligible organization users.

#### Scenario: Project enters smart-throttled state

Given a project was not previously smart-throttled and smart throttling is applied
When notification processing runs
Then eligible organization users must receive a project throttling notification email.

#### Scenario: Project remains smart-throttled

Given a project is already smart-throttled
When additional event posts are processed
Then Exceptionless must not send duplicate throttling emails for every post.

#### Scenario: User is not email-eligible

Given a user belongs to the organization and does not have a verified email or has notifications disabled
When a project throttling notification is sent
Then that user must not receive the email.
