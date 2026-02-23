#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.prod.yml}"
DEFAULT_ENV_FILE="$SCRIPT_DIR/.env"
ENV_FILE="${ENV_FILE:-$DEFAULT_ENV_FILE}"

ACTION="${1:-up}"
API_ARG="${2:-}"

COMPOSE_ARGS=(-f "$COMPOSE_FILE")
if [[ -f "$ENV_FILE" ]]; then
  COMPOSE_ARGS+=(--env-file "$ENV_FILE")
fi

compose() {
  docker compose "${COMPOSE_ARGS[@]}" "$@"
}

print_usage() {
  cat << USAGE
Usage:
  ./run-prod.sh [action] [api_upstream]

Actions:
  up        Build and start production stack (default)
  down      Stop and remove production stack
  restart   Restart api and web services
  logs      Follow logs for api and web
  ps        Show container status
  help      Show this help

Environment:
  COMPOSE_FILE  Override compose file path (default: docker-compose.prod.yml)
  ENV_FILE      Override env file path (default: .env)
  API_UPSTREAM  Backend URL for frontend Nginx proxy (used if arg is omitted)

Examples:
  ./run-prod.sh
  ./run-prod.sh up
  ./run-prod.sh up https://api.mi.saed.digital
  API_UPSTREAM=https://api.staging.mi.saed.digital ./run-prod.sh up
  ENV_FILE=.env.prod ./run-prod.sh up
  ./run-prod.sh logs
USAGE
}

if ! command -v docker >/dev/null 2>&1; then
  echo "Error: docker is not installed or not in PATH." >&2
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Error: docker compose plugin is not available." >&2
  exit 1
fi

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Error: compose file not found: $COMPOSE_FILE" >&2
  exit 1
fi

if [[ -f "$ENV_FILE" ]]; then
  echo "[prod] Using env file: $ENV_FILE"
else
  echo "[prod] Env file not found ($ENV_FILE), using compose defaults + shell env"
fi

case "$ACTION" in
  up)
    if [[ -n "$API_ARG" ]]; then
      echo "[prod] Using API_UPSTREAM from argument: $API_ARG"
      API_UPSTREAM="$API_ARG" compose up -d --build
    elif [[ -n "${API_UPSTREAM:-}" ]]; then
      echo "[prod] Using API_UPSTREAM from environment: $API_UPSTREAM"
      compose up -d --build
    else
      echo "[prod] Using API_UPSTREAM from env file or compose default"
      compose up -d --build
    fi
    compose ps
    ;;
  down)
    compose down
    ;;
  restart)
    compose restart api web
    compose ps
    ;;
  logs)
    compose logs -f api web
    ;;
  ps)
    compose ps
    ;;
  help|-h|--help)
    print_usage
    ;;
  *)
    echo "Error: unknown action '$ACTION'" >&2
    print_usage
    exit 1
    ;;
esac
