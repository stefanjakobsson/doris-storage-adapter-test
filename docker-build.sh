#!/usr/bin/env bash

set -euo pipefail

VERSION="$(bash resolve_version.sh)"

echo "Resolved version: $VERSION"

docker build \
    --build-arg MINVERVERSIONOVERRIDE=$VERSION \
    --build-arg CI=true \
    --build-arg SOURCE_DATE_EPOCH="$(git log -1 --pretty=%ct)" \
    .