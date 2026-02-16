import type { ApiError } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

type LogLevel = 'debug' | 'info' | 'warning' | 'error' | 'fatal';

interface LogEntry {
  level: LogLevel;
  message: string;
  timestamp: string;
  properties?: Record<string, unknown>;
}

const buffer: LogEntry[] = [];
const MAX_BUFFER = 20;
const FLUSH_INTERVAL_MS = 10_000;
let flushTimer: ReturnType<typeof setInterval> | null = null;

function createEntry(level: LogLevel, message: string, properties?: Record<string, unknown>): LogEntry {
  return {
    level,
    message,
    timestamp: new Date().toISOString(),
    properties: {
      ...properties,
      url: window.location.pathname,
      userAgent: navigator.userAgent,
    },
  };
}

async function flush(): Promise<void> {
  if (buffer.length === 0) return;

  const entries = buffer.splice(0, buffer.length);

  try {
    await fetch(`${API_BASE_URL}/logs`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(entries),
    });
  } catch {
    // Si falla el envío, no perdemos los logs — los mandamos a consola
    entries.forEach((e) => console.warn('[logger] No se pudo enviar:', e.message));
  }
}

function enqueue(entry: LogEntry): void {
  buffer.push(entry);

  // También loguear en consola en desarrollo
  if (import.meta.env.DEV) {
    const consoleFn = entry.level === 'error' || entry.level === 'fatal'
      ? console.error
      : entry.level === 'warning'
        ? console.warn
        : console.log;
    consoleFn(`[${entry.level}]`, entry.message, entry.properties ?? '');
  }

  if (buffer.length >= MAX_BUFFER) {
    flush();
  }
}

/** Inicializar el logger (llamar una vez en main.tsx) */
export function initLogger(): void {
  if (flushTimer) return;

  // Flush periódico
  flushTimer = setInterval(flush, FLUSH_INTERVAL_MS);

  // Flush al cerrar la página
  window.addEventListener('beforeunload', () => flush());

  // Capturar errores globales no manejados
  window.addEventListener('error', (event) => {
    logger.error('Unhandled error', {
      error: event.message,
      filename: event.filename,
      line: event.lineno,
      col: event.colno,
    });
  });

  // Capturar promesas rechazadas sin manejar
  window.addEventListener('unhandledrejection', (event) => {
    logger.error('Unhandled promise rejection', {
      reason: String(event.reason),
    });
  });
}

export const logger = {
  debug: (message: string, properties?: Record<string, unknown>) =>
    enqueue(createEntry('debug', message, properties)),

  info: (message: string, properties?: Record<string, unknown>) =>
    enqueue(createEntry('info', message, properties)),

  warn: (message: string, properties?: Record<string, unknown>) =>
    enqueue(createEntry('warning', message, properties)),

  error: (message: string, properties?: Record<string, unknown>) =>
    enqueue(createEntry('error', message, properties)),

  /** Log de error a partir de un catch */
  captureError: (error: unknown, context?: string) => {
    const msg = error instanceof Error ? error.message : String(error);
    const props: Record<string, unknown> = { context };
    if (error instanceof Error && error.stack) {
      props.stack = error.stack;
    }
    enqueue(createEntry('error', msg, props));
  },

  /** Log de error de API */
  captureApiError: (apiError: ApiError, context?: string) => {
    enqueue(createEntry('error', apiError.message, {
      context,
      statusCode: apiError.status,
    }));
  },

  /** Forzar envío inmediato */
  flush,
};
