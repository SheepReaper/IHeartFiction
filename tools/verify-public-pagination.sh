#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

export ALLOW_INSECURE_TLS=true

node verify-pagination-load-more.cjs
node verify-social-metadata.cjs