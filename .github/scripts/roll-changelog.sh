#!/usr/bin/env bash
#
# Rolls the "[Unreleased]" section of CHANGELOG.md into a dated version heading and
# leaves a fresh, empty "[Unreleased]" section at the top.
#
#   ## [Unreleased]        ->   ## [Unreleased]
#
#   ### Fixed                    ## [1.1.0] - 2026-09-18
#   - ...
#                                ### Fixed
#                                - ...
#
# The headings follow Keep a Changelog, which CHANGELOG.md says it adheres to: the
# version in square brackets, without the "v" the release tag carries.
#
# Usage: roll-changelog.sh <version> [date] [changelog-path]
#
#   version         Version being released, with or without a leading "v".
#   date            Release date as YYYY-MM-DD. Defaults to today (UTC).
#   changelog-path  Defaults to CHANGELOG.md in the repository root.
#
# Exits non-zero if the changelog has no "[Unreleased]" heading, or if that section
# is empty — releasing with nothing written down is almost always a mistake.

set -euo pipefail

version=${1:?usage: roll-changelog.sh <version> [date] [changelog-path]}
date=${2:-$(date -u +%Y-%m-%d)}
changelog=${3:-"$(dirname "$0")/../../CHANGELOG.md"}

version=${version#v}
heading="## [${version}] - ${date}"

if [[ ! -f $changelog ]]; then
  echo "::error::Changelog not found: $changelog" >&2
  exit 2
fi

if ! grep -qE '^## \[Unreleased\][[:space:]]*$' "$changelog"; then
  echo "::error::No '## [Unreleased]' heading found in $changelog" >&2
  exit 3
fi

if grep -qE "^## \[${version//./\\.}\]" "$changelog"; then
  echo "::error::$changelog already contains a heading for ${version}" >&2
  exit 4
fi

# Everything between "## [Unreleased]" and the next "## " heading (or EOF).
unreleased_body=$(awk '
  /^## \[Unreleased\][[:space:]]*$/ { inside = 1; next }
  inside && /^## / { exit }
  inside { print }
' "$changelog")

if [[ -z ${unreleased_body//[[:space:]]/} ]]; then
  echo "::error::The [Unreleased] section of $changelog is empty — nothing to release" >&2
  exit 5
fi

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

awk -v heading="$heading" '
  { print }
  !done && /^## \[Unreleased\][[:space:]]*$/ {
    print ""
    print heading
    done = 1
  }
' "$changelog" >"$tmp"

mv "$tmp" "$changelog"
trap - EXIT

echo "Rolled [Unreleased] into '${heading}' in ${changelog}"
