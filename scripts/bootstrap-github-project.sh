#!/usr/bin/env bash
# Complete the GitHub metadata bootstrap for the existing User Management API
# repository and project. Safe to rerun: creates missing milestones/fields,
# updates label metadata, and skips project items that already exist.
set -euo pipefail

OWNER="Onurb29"
REPO="Onurb29/user-management-api"
PROJECT_TITLE="User Management API"

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI is required. On macOS: brew install gh" >&2
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "Authenticate first: gh auth login" >&2
  exit 1
fi

echo "Looking for existing project: $PROJECT_TITLE"
if ! PROJECT_NUMBER=$(gh project list --owner "$OWNER" --limit 100 --format json --jq '.projects[] | select(.title == "User Management API") | .number'); then
  echo "Cannot read GitHub Projects. Try: gh auth refresh -s project" >&2
  exit 1
fi
PROJECT_NUMBER=$(printf '%s\n' "$PROJECT_NUMBER" | head -n 1)
if [[ -z "$PROJECT_NUMBER" ]]; then
  echo "Could not find a project named '$PROJECT_TITLE' owned by $OWNER." >&2
  exit 1
fi
echo "Found GitHub Project #$PROJECT_NUMBER."

echo "Creating/updating labels..."
create_label() {
  gh label create "$1" -R "$REPO" --color "$2" --description "$3" --force >/dev/null
}
create_label "type:feature"       "1D76DB" "New functionality"
create_label "type:documentation" "0075CA" "Documentation or specification"
create_label "type:test"          "BFDADC" "Automated testing and verification"
create_label "type:chore"         "E4E669" "Project maintenance and tooling"
create_label "priority:P0"        "B60205" "Must-have for v1.0.0"
create_label "priority:P1"        "D93F0B" "Important delivery improvement"
create_label "area:api"           "0052CC" "HTTP endpoints and API contract"
create_label "area:database"      "5319E7" "EF Core and persistence"
create_label "area:security"      "D73A4A" "Authentication, authorization, and secure errors"
create_label "area:observability" "FBCA04" "Logging and monitoring"
create_label "area:performance"   "F9D0C4" "Query and runtime efficiency"
create_label "area:ci"            "0E8A16" "Continuous integration and release"

echo "Creating missing repository milestones..."
EXISTING_MILESTONES=$(gh api "repos/$REPO/milestones?state=all&per_page=100" --jq '.[].title')
create_milestone() {
  local title="$1"
  local description="$2"
  if printf '%s\n' "$EXISTING_MILESTONES" | grep -Fxq "$title"; then
    echo "  Already exists: $title"
  else
    gh api --method POST "repos/$REPO/milestones" -f title="$title" -f description="$description" >/dev/null
    echo "  Created: $title"
  fi
}
create_milestone "M0 - Requirements & Architecture" "API contract and development workflow; issues 1-2"
create_milestone "M1 - Project Foundation & Persistence" "Solution scaffolding, EF Core SQLite and services; issues 3-5"
create_milestone "M2 - CRUD API & Validation" "CRUD endpoints and input validation; issues 6-9"
create_milestone "M3 - Security, Middleware & Error Handling" "JWT, policy authorization, errors, structured logging; issues 10-12"
create_milestone "M4 - Quality Assurance & Performance" "Integration tests, performance, and negative tests; issues 13-15"
create_milestone "M5 - Documentation, CI & Release" "Onboarding, Copilot record, GitHub Actions and v1.0.0; issues 16-18"

echo "Linking project to repository (may already be linked)..."
if ! gh project link "$PROJECT_NUMBER" --owner "$OWNER" --repo "$REPO" >/dev/null 2>&1; then
  echo "  Project may already be linked; check its Settings if not visible from the repo."
fi

echo "Ensuring GitHub Project fields..."
FIELDS=$(gh project field-list "$PROJECT_NUMBER" --owner "$OWNER" --format json --jq '.fields[].name')
if ! printf '%s\n' "$FIELDS" | grep -Fxq "Priority"; then
  gh project field-create "$PROJECT_NUMBER" --owner "$OWNER" --name "Priority" --data-type SINGLE_SELECT --single-select-options "P0,P1" >/dev/null
fi
if ! printf '%s\n' "$FIELDS" | grep -Fxq "Phase"; then
  gh project field-create "$PROJECT_NUMBER" --owner "$OWNER" --name "Phase" --data-type SINGLE_SELECT --single-select-options "M0,M1,M2,M3,M4,M5" >/dev/null
fi

echo "Assigning milestones/labels and adding issues to the existing project..."
EXISTING_ITEMS=$(gh project item-list "$PROJECT_NUMBER" --owner "$OWNER" --limit 200 --format json --jq '.items[].content.url')
while IFS='|' read -r number phase type priority area; do
  case "$phase" in
    M0) milestone="M0 - Requirements & Architecture" ;;
    M1) milestone="M1 - Project Foundation & Persistence" ;;
    M2) milestone="M2 - CRUD API & Validation" ;;
    M3) milestone="M3 - Security, Middleware & Error Handling" ;;
    M4) milestone="M4 - Quality Assurance & Performance" ;;
    M5) milestone="M5 - Documentation, CI & Release" ;;
    *) echo "Unexpected phase: $phase" >&2; exit 1 ;;
  esac

  labels="$type,$priority"
  if [[ -n "$area" ]]; then labels="$labels,$area"; fi
  gh issue edit "$number" -R "$REPO" --milestone "$milestone" --add-label "$labels" >/dev/null

  issue_url="https://github.com/$REPO/issues/$number"
  if ! printf '%s\n' "$EXISTING_ITEMS" | grep -Fxq "$issue_url"; then
    gh project item-add "$PROJECT_NUMBER" --owner "$OWNER" --url "$issue_url" >/dev/null
  fi

  gh project item-edit "$PROJECT_NUMBER" --owner "$OWNER" --url "$issue_url" --field "Priority" --value "$priority" >/dev/null
  gh project item-edit "$PROJECT_NUMBER" --owner "$OWNER" --url "$issue_url" --field "Phase" --value "$phase" >/dev/null

  echo "  Configured issue #$number ($phase, $priority)"
done <<'ISSUES'
1|M0|type:documentation|priority:P0|area:api
2|M0|type:chore|priority:P0|
3|M1|type:feature|priority:P0|area:api
4|M1|type:feature|priority:P0|area:database
5|M1|type:feature|priority:P0|area:api
6|M2|type:feature|priority:P0|area:api
7|M2|type:feature|priority:P0|area:api
8|M2|type:feature|priority:P0|area:api
9|M2|type:feature|priority:P0|area:api
10|M3|type:feature|priority:P0|area:security
11|M3|type:feature|priority:P0|area:security
12|M3|type:feature|priority:P0|area:observability
13|M4|type:test|priority:P0|
14|M4|type:feature|priority:P1|area:performance
15|M4|type:test|priority:P0|area:security
16|M5|type:documentation|priority:P1|
17|M5|type:documentation|priority:P1|
18|M5|type:chore|priority:P1|area:ci
ISSUES

echo
echo "Completed: 6 milestones, labels, issue assignments, and 18 project items."
echo "Open: https://github.com/users/$OWNER/projects/$PROJECT_NUMBER"
echo "In the GitHub Project UI, add Board and Roadmap views as desired."
echo "Roadmap dates are intentionally left unset; no fabricated course deadlines."
