---
title: Service Request & Quote Manager — Challenge Brief
type: requirements-input
source: interview take-home prompt + target role job description
created: 2026-07-28
---

# Service Request & Quote Manager — Challenge Brief

## Origin

Senior Full-Stack programming challenge issued as part of an interview for a
**Senior Full Stack Software Engineer** role (Technology dept, reports to DevOps Manager,
Remote-US). Deliverable is due in two days and must be demoable live as well as runnable
from a clone by the reviewers.

## Stated goal

Build a small internal tool that helps a team manage organizations, requests, and
quotes/offers. A request can have multiple quotes, and one quote may ultimately be
accepted. The exercise is explicitly about demonstrating how a candidate designs and
delivers a small "production-minded" system end-to-end: .NET API + React UI.

## Functional requirements (verbatim intent)

### FR-1 — Entity management

A user can create and manage Organizations, Requests for quotes, and Quotes.

### FR-2 — One request, many quotes

A request can have multiple quotes at different stages.

### FR-3 — Enforced lifecycle

Status transitions must be supported via the UI **and enforced by the API**. Quotes move
through a lifecycle. Only one quote per request can be accepted.

### FR-4 — Work visibility

"Users need a reliable way to see what's happening with the quotes, so they can focus
their time on the right work." This is a triage/prioritisation requirement, not a CRUD
grid requirement.

### FR-5 — Auditability (lightweight)

Track "what happened" in the usage of the app.

## UI requirements

Create whatever screens and interactions are necessary to support the purpose of the
application. The interface should make it easy for a user to understand what is happening
and take appropriate action. Design decisions should prioritise **clarity, usability, and
maintainability over visual polish**.

## Backend requirements

A clean API supporting the UI use cases above, plus persistence.

## Role-derived technical constraints

Drawn from the job description for the target role; these shape technology selection even
where the challenge prompt is silent.

Required by the role:

- .NET / C#, ASP.NET Core, Web APIs
- React + TypeScript, modern UI frameworks, reusable components, state management patterns
- RESTful API design, authentication, security best practices
- Relational databases and data modelling
- Authentication, authorisation, and **role-based access control patterns**
- Azure-based environments including cloud services, CI/CD pipelines, and monitoring tools
- Automated unit, integration, and end-to-end tests
- Observability and production readiness

Preferred by the role:

- Azure App Services, Azure Functions, **Service Bus**, Storage
- Microservices **and modular monolithic** architectures
- Automated testing frameworks and performance tuning

## Candidate-stated intent

The candidate wants the submission to demonstrate, specifically:

- Azure Service Bus used for queueing
- Logging for tracking client actions and for error capture
- RESTful APIs
- Basic login functionality

## Hard delivery constraints

- **Two calendar days** of elapsed time, including rehearsal.
- Must be demoable **on the fly** — no long start-up ritual, no cloud dependency on the
  critical path.
- Must also run for reviewers from a fresh clone on a machine that is not the candidate's.
- Azure subscription is **not currently active**; activating it is possible but costs
  setup time and carries verification risk. Azure integration must therefore be present
  in code and configuration-gated, not required at runtime.
