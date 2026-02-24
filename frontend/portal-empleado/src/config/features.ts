export type FeatureFlags = {
  gestion: boolean;
  vacaciones: boolean;
};

const rawGestionFlag = (import.meta.env.VITE_FEATURE_GESTION ?? '').toString();
const rawVacacionesFlag = (import.meta.env.VITE_FEATURE_VACACIONES ?? '').toString();

function parseBooleanFlag(value: string): boolean {
  const normalized = value.trim().toLowerCase();
  if (normalized === 'true') return true;
  if (normalized === '1') return true;
  if (normalized === 'yes') return true;
  if (normalized === 'on') return true;
  return false;
}

const defaultFeatures: FeatureFlags = {
  gestion: parseBooleanFlag(rawGestionFlag),
  vacaciones: parseBooleanFlag(rawVacacionesFlag),
};

let cachedFeatures: FeatureFlags | null = null;

export function getDefaultFeatures(): FeatureFlags {
  return defaultFeatures;
}

export async function loadFeatures(): Promise<FeatureFlags> {
  if (cachedFeatures) return cachedFeatures;

  try {
    const response = await fetch('/api/features', { credentials: 'include' });
    if (!response.ok) {
      cachedFeatures = defaultFeatures;
      return cachedFeatures;
    }
    const data = await response.json() as Partial<FeatureFlags>;
    cachedFeatures = {
      gestion: typeof data.gestion === 'boolean' ? data.gestion : defaultFeatures.gestion,
      vacaciones: typeof data.vacaciones === 'boolean' ? data.vacaciones : defaultFeatures.vacaciones,
    };
    return cachedFeatures;
  } catch {
    cachedFeatures = defaultFeatures;
    return cachedFeatures;
  }
}
