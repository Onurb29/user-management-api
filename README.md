# User Management API

> **Status: Planning** | **Framework: ASP.NET Core / .NET 10** | **Target release: v1.0.0**

A course capstone built with a professional issue-driven software development workflow. The goal is to deliver a secure REST API that lets authorized **IT** and **HR** personnel manage user records.

## Project management

- **[Development roadmap](docs/ROADMAP.md)**: architecture, phases, test expectations, and Definition of Done.
- **[GitHub Issues](https://github.com/Onurb29/user-management-api/issues)**: 18 actionable tasks with acceptance criteria.
- **GitHub Project**: `User Management API` (Board/Roadmap). Use `scripts/bootstrap-github-project.sh` to associate issues, milestones, labels, and project fields using the GitHub CLI.

## Planned API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/users` | List users with pagination |
| GET | `/api/users/{id}` | Retrieve user by ID |
| POST | `/api/users` | Create and validate a user |
| PUT | `/api/users/{id}` | Update existing user |
| DELETE | `/api/users/{id}` | Delete user |

## Planned stack

ASP.NET Core Controllers, C#, EF Core, SQLite, JWT Bearer authentication, IT/HR authorization policies, Serilog, ProblemDetails, OpenAPI, xUnit integration tests, and GitHub Actions.

**Architecture:** HTTP request → ASP.NET Core pipeline → Controller → Service → EF Core → SQLite.

## Working agreement

- Take one GitHub Issue at a time.
- Create a small feature branch (e.g. `feat/06-get-users`).
- Implement and test the requirement; update documentation when relevant.
- Open a PR, review the change, and merge only after the acceptance criteria pass.
- Record **actual** Microsoft Copilot contributions for the lab.
- Never commit secrets, development JWT signing keys, bearer tokens, or personal user data.

## Initial setup

The API has **not been scaffolded yet**. Work begins with [issue #1: API contract](https://github.com/Onurb29/user-management-api/issues/1) and [issue #2: workflow](https://github.com/Onurb29/user-management-api/issues/2). Implementation starts with [issue #3: scaffold ASP.NET Core](https://github.com/Onurb29/user-management-api/issues/3).
