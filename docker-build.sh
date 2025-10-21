#!/usr/bin/env bash

docker build --no-cache \
    -t doris-storage-adapter \
    --build-arg VERSION="$(./resolve-version.sh)" \
    --build-arg CI=true \
    --build-arg SOURCE_DATE_EPOCH="$(git log -1 --pretty=%ct)" \
    .
    