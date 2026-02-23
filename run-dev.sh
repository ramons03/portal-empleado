#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.dev.yml}"
DEFAULT_ENV_FILE="$SCRIPT_DIR/.env.dev"
ENV_FILE="${ENV_FILE:-$DEFAULT_ENV_FILE}"

# If .env.dev doesn't exist, fallback to .env
if [[ ! -f "$ENV_FILE" && "$ENV_FILE" == "$DEFAULT_ENV_FILE" && -f "$SCRIPT_DIR/.env" ]]; then
  ENV_FILE="$SCRIPT_DIR/.env"
fi

ACTION="${1:-up}"

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
  ./run-dev.sh [action]

Actions:
  up        Build and start development stack (default)
  down      Stop and remove development stack
  restart   Restart api and frontend services
  logs      Follow logs for api and frontend
  ps        Show container status
  help      Show this help

Environment:
  COMPOSE_FILE  Override compose file path (default: docker-compose.dev.yml)
  ENV_FILE      Override env file path (default: .env.dev, fallback to .env)

Examples:
  ./run-dev.sh
  ./run-dev.sh up
  ENV_FILE=.env ./run-dev.sh up
  ./run-dev.sh logs
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
  echo "[dev] Using env file: $ENV_FILE"
else
  echo "[dev] No env file found, using compose defaults + shell env"
fi

case "$ACTION" in
  up)
    compose up -d --build
    compose ps
    ;;
  down)
    compose down
    ;;
  restart)
    compose restart api frontend
    compose ps
    ;;
  logs)
    compose logs -f api frontend
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
