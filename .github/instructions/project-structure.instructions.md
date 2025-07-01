---
description: "Project structure reference for understanding the codebase organization"
applyTo: "**"
---

# Project Structure

```plaintext
project-root/
├── build                                           # Build files
├── docker                                          # Docker files
├── k8s                                             # Kubernetes files
├── samples
├── src
│   ├── Exceptionless.AppHost                       # Aspire
│   ├── Exceptionless.Core                          # Domain
│   ├── Exceptionless.EmailTemplates                # Email Templates
│   ├── Exceptionless.Insulation                    # Concrete Implementations
│   ├── Exceptionless.Job                           # ASP.NET Core Jobs
│   ├── Exceptionless.Web                           # ASP.NET Core Web Application
│   │   ├── ClientApp                               # Frontend SvelteKit Spa Application
│   │   │   ├── api-templates                       # API templates for generated code using OpenApi
│   │   │   ├── e2e
│   │   │   ├── src                                 # JavaScript SvelteKit application
│   │   │   │   ├── lib
│   │   │   │   │   ├── assets                      # Static assets
│   │   │   │   │   ├── features                    # Vertical Sliced Features, each folder corresponds to an api controller
│   │   │   │   │   │   ├── events                  # Event features (related to Events Controller)
│   │   │   │   │   │   │   ├── components
│   │   │   │   │   │   │   └── models
│   │   │   │   │   │   ├── organizations
│   │   │   │   │   │   ├── projects
│   │   │   │   │   │   │   └── components
│   │   │   │   │   │   ├── shared                  # Shared components used by all other features
│   │   │   │   │   │   │   ├── api
│   │   │   │   │   │   │   ├── components
│   │   │   │   │   │   │   └── models
│   │   │   │   │   │   ├── stacks
│   │   │   │   │   │   │   └── components
│   │   │   │   │   ├── generated                   # Generated code
│   │   │   │   │   ├── hooks                       # Client hooks
│   │   │   │   │   └── utils                       # Utility functions
│   │   │   │   └── routes                          # Application routes
│   │   │   │       ├── (app)
│   │   │   │       │   ├── account
│   │   │   │       │   └── stream
│   │   │   │       ├── (auth)
│   │   │   │       │   ├── login
│   │   │   │       │   └── logout
│   │   │   │       └── status
│   │   │   └── static                              # Static assets
│   │   ├── ClientApp.angular                       # Legacy Angular Client Application (Ignore)
│   │   ├── Controllers                             # ASP.NET Core Web API Controllers
│   └── tests                                       # ASP.NET Core Unit and Integration Tests
```
