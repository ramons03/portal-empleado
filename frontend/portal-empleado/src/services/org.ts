import { readJson, writeJson } from './storage';

export type OrgScope = 'school' | 'saed';

export type OrgEmployee = {
  id: string;
  fullName: string;
  role: string;
  unit: string;
  managerId?: string | null;
  level: 'staff' | 'coordinator' | 'director' | 'saed';
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';
const ORG_SCOPE_KEY = 'saed.org.scope';

const mockEmployees: OrgEmployee[] = [
  {
    id: 'e-01',
    fullName: 'Lucia Perez',
    role: 'Docente',
    unit: 'Colegio San Martin',
    managerId: 'e-02',
    level: 'staff',
  },
  {
    id: 'e-02',
    fullName: 'Carlos Gomez',
    role: 'Director',
    unit: 'Colegio San Martin',
    managerId: 'e-07',
    level: 'director',
  },
  {
    id: 'e-03',
    fullName: 'Marina Lopez',
    role: 'Preceptora',
    unit: 'Colegio Belgrano',
    managerId: 'e-04',
    level: 'staff',
  },
  {
    id: 'e-04',
    fullName: 'Hernan Ruiz',
    role: 'Director',
    unit: 'Colegio Belgrano',
    managerId: 'e-07',
    level: 'director',
  },
  {
    id: 'e-05',
    fullName: 'Julieta Diaz',
    role: 'Coordinadora Academica',
    unit: 'Colegio San Martin',
    managerId: 'e-02',
    level: 'coordinator',
  },
  {
    id: 'e-06',
    fullName: 'Santiago Mendez',
    role: 'Analista RRHH',
    unit: 'SAED',
    managerId: 'e-07',
    level: 'saed',
  },
  {
    id: 'e-07',
    fullName: 'Valeria Ortega',
    role: 'Direccion General',
    unit: 'SAED',
    managerId: null,
    level: 'saed',
  },
];

export function getOrgScope(): OrgScope {
  const stored = readJson<OrgScope | null>(ORG_SCOPE_KEY, null);
  if (stored === 'school' || stored === 'saed') return stored;

  const envValue = (import.meta.env.VITE_ORG_SCOPE ?? '').toString().toLowerCase();
  if (envValue === 'saed') return 'saed';
  return 'school';
}

export function setOrgScope(scope: OrgScope): void {
  writeJson(ORG_SCOPE_KEY, scope);
}

export async function getOrgEmployees(): Promise<OrgEmployee[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/org/employees`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error(`Failed to load org employees: ${response.statusText}`);
    }

    const data = await response.json();
    if (!Array.isArray(data)) {
      return mockEmployees;
    }

    return data as OrgEmployee[];
  } catch {
    return mockEmployees;
  }
}

export function buildReportingChain(
  employees: OrgEmployee[],
  employeeId: string,
  scope: OrgScope
): OrgEmployee[] {
  const map = new Map(employees.map((emp) => [emp.id, emp]));
  const chain: OrgEmployee[] = [];

  let current = map.get(employeeId);
  while (current) {
    chain.push(current);

    if (scope === 'school' && current.level === 'director') {
      break;
    }

    const nextId = current.managerId ?? undefined;
    current = nextId ? map.get(nextId) : undefined;
  }

  return chain;
}
