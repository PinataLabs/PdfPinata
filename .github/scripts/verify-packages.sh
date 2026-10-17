#!/usr/bin/env bash
#
# Checks what the release workflows packed before anything is pushed: one package per
# project in PACKAGES, nothing else, every one the same version, and none carrying the
# Six Labors licence key.
#
# Usage: verify-packages.sh <package-dir> <version> <project>...
#
#   package-dir  Where `dotnet pack --output` wrote the .nupkg files.
#   version      The version every package must have, with or without a leading "v".
#                Empty to take it from the first project's package, which is what the
#                pre-release workflow does: MinVer decides it there, not a tag.
#   project      Project directory name, e.g. PdfPinata.Skia. Pass PACKAGES unquoted.
#
# All the packages are versioned in lockstep, and a backend depends on the PdfPinata
# of its own version, so one package on another version is a set that cannot be
# installed together. A project in PACKAGES that is not packable packs nothing and
# `dotnet pack` still succeeds, so a missing package is caught here or not at all.
#
# The key (src/PdfPinata.ImageSharp4/sixlabors.lic) is excluded from packing in
# Directory.Build.targets. It is for building this repository; no consumer needs it.
#
# Exits non-zero, after reporting every problem it finds, if any check fails.

set -euo pipefail

dir=${1:?usage: verify-packages.sh <package-dir> <version> <project>...}
version=${2-}
shift 2 || { echo "::error::usage: verify-packages.sh <package-dir> <version> <project>..." >&2; exit 1; }

if [[ "$#" -eq 0 ]]; then
  echo "::error::No projects given - nothing to verify" >&2
  exit 1
fi

version=${version#v}
shopt -s nullglob

if [[ -z "$version" ]]; then
  # A digit right after the name tells PdfPinata.1.0.0 from PdfPinata.Skia.1.0.0.
  first=("$dir/$1".[0-9]*.nupkg)
  if [[ "${#first[@]}" -ne 1 ]]; then
    echo "::error::Expected one package for $1 in $dir, found ${#first[@]}" >&2
    exit 1
  fi
  version=$(basename "${first[0]}" .nupkg)
  version=${version#"$1".}
fi

status=0

for project in "$@"; do
  package="$dir/$project.$version.nupkg"
  if [[ ! -f "$package" ]]; then
    echo "::error::$project.$version.nupkg was not packed" >&2
    status=1
  fi
done

packages=("$dir"/*.nupkg)
if [[ "${#packages[@]}" -ne "$#" ]]; then
  echo "::error::Packed ${#packages[@]} packages for $# projects" >&2
  status=1
fi

for package in "${packages[@]}"; do
  if unzip -Z1 "$package" | grep -qi 'sixlabors\.lic$'; then
    echo "::error::$(basename "$package") contains the Six Labors licence key" >&2
    status=1
  fi
done

if [[ "$status" -ne 0 ]]; then
  echo "Packed:" >&2
  for package in "${packages[@]}"; do echo "  $(basename "$package")" >&2; done
  echo "::error::The packages are not the $# expected at version $version - refusing to continue" >&2
  exit 1
fi

echo "$# packages, all version $version, none carrying the licence key:"
for package in "${packages[@]}"; do echo "  $(basename "$package")"; done
