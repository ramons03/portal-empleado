type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

const memoryStore = new Map<string, string>();

function getStorage(): Storage | null {
  try {
    if (typeof window === 'undefined') return null;
    return window.localStorage;
  } catch {
    return null;
  }
}

function getItem(key: string): string | null {
  const storage = getStorage();
  if (storage) return storage.getItem(key);
  return memoryStore.get(key) ?? null;
}

function setItem(key: string, value: string): void {
  const storage = getStorage();
  if (storage) {
    storage.setItem(key, value);
    return;
  }
  memoryStore.set(key, value);
}

function removeItem(key: string): void {
  const storage = getStorage();
  if (storage) {
    storage.removeItem(key);
    return;
  }
  memoryStore.delete(key);
}

export function readJson<T>(key: string, fallback: T): T {
  const raw = getItem(key);
  if (!raw) return fallback;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

export function writeJson(key: string, value: JsonValue): void {
  setItem(key, JSON.stringify(value));
}

export function clearKey(key: string): void {
  removeItem(key);
}
