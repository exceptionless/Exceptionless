---
title: "Exclude and Protect Sensitive Data within Exceptionless"
---

# Exclude and Protect Sensitive Data within Exceptionless

![Exceptionless Data Exclusions for Security](/assets/img/news/data-exclusions.png)We realize you may have sensitive data that could potentially be transmitted within an Exceptionless error, event, log message, etc.

In order to help make sure that information is not compromised, we have included a [simple comma delimited field for data exclusions](/docs/security) on the Exceptionless Project Settings page where you can add field names that you would like to be excluded from any error or event that is reported.

Once set, the excluded field data is discarded at the client level and never hits our servers.

## Types of Data You Can Exclude

You can exclude data from any of the following:

* Extended Data Properties
* Form Fields
* Cookies
* Query Parameters

## Data Exclusion Wildcards

To be extra careful with your data, using * allows you to specify wildcards that can be used to dictate "starts with," "ends with," or "contains."

* `string*`
    Following the string with a wildcard removes any field that starts with the string from the event report.
* `*string`
    If you prefix the field name with a wildcard, it will remove any field that ends with the string.
* `*string*`
    Using a wildcard before and after the string means that the system will remove any field that contains the string.

### Example

![Exceptionless Security](/assets/img/news/data-exclusion-examples.png)

One potential example is, let’s say, user addresses. Perhaps you have multiple user addresses that may get transmitted, and you want to exclude some or all of them. Maybe you have "HomeAddress" and "WorkAddress".

To exclude only "HomeAddress" data, you would just add `HomeAddress` as an exclusion. The same goes for "WorkAddress."

To exclude both, you could either add `HomeAddress` and `WorkAddress`, separated by a comma, or you could use `*Address` if those were the only two fields that ended with "Address." If those were the only fields that contained "Address" at all, you could use `*Address*`.

It's easy stuff, but powerful enough to be aware of and use where possible to ensure security.

## What Do You Exclude?

We are sure you can come up with some creative exclusions for various types of sensitive data. At Exceptionless, we exclude any and all relatively sensitive data to protect our users as much as possible.

Obviously, things like passwords, credit card info, etc are encrypted, but other fields such as addresses, etc, are also relatively sensitive information that typically doesn’t need to be displayed.

What do you exclude? Do you have any feedback about this Exceptionless feature? Let us know!
