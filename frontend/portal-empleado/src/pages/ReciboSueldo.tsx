import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  getReciboDetalle,
  getReciboPdfUrl,
  getReciboSueldo,
  logReciboSueldoView,
  type ReciboDetalleResponse,
  type ReciboItem,
} from '../services/recibo-sueldo';
import { logger } from '../services/logger';
import type { FeatureFlags } from '../config/features';
import './ReciboSueldo.css';

type ReciboSueldoProps = {
  features: FeatureFlags;
};

type HumanReceiptStatus = 'Disponible' | 'Firmado' | 'Pendiente de firma';

type PeriodGroup = {
  key: string;
  year: number;
  month: number;
  monthLabel: string;
  periodLabel: string;
  order: number;
  items: ReciboItem[];
};

export default function ReciboSueldo({ features }: ReciboSueldoProps) {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<ReciboItem[]>([]);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [detailById, setDetailById] = useState<Record<string, ReciboDetalleResponse>>({});
  const [detailLoadingId, setDetailLoadingId] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [selectedYear, setSelectedYear] = useState<number | null>(null);
  const [selectedMonthKey, setSelectedMonthKey] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        await logReciboSueldoView('page');
        const response = await getReciboSueldo();
        setItems(response.items);
      } catch (err) {
        logger.captureError(err, 'ReciboSueldo.load');
        setError(toFriendlyLoadMessage(err));
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  const sortedItems = useMemo(() => {
    return [...items].sort((a, b) => {
      const orderA = toOrderTimestamp(a);
      const orderB = toOrderTimestamp(b);
      return orderB - orderA;
    });
  }, [items]);

  const periodGroups = useMemo(() => {
    const groups = new Map<string, PeriodGroup>();

    for (const item of sortedItems) {
      const metadata = buildPeriodMetadata(item);
      const current = groups.get(metadata.key);
      if (!current) {
        groups.set(metadata.key, {
          ...metadata,
          items: [item],
        });
        continue;
      }

      current.items.push(item);
      current.order = Math.max(current.order, metadata.order);
    }

    return Array.from(groups.values()).sort((a, b) => {
      if (b.year !== a.year) {
        return b.year - a.year;
      }

      if (b.month !== a.month) {
        return b.month - a.month;
      }

      return b.order - a.order;
    });
  }, [sortedItems]);

  const availableYears = useMemo(() => {
    return Array.from(new Set(periodGroups.map((group) => group.year))).sort((a, b) => b - a);
  }, [periodGroups]);

  const monthsForSelectedYear = useMemo(() => {
    if (selectedYear === null) {
      return [];
    }

    return periodGroups.filter((group) => group.year === selectedYear);
  }, [periodGroups, selectedYear]);

  const selectedGroup = useMemo(() => {
    if (selectedMonthKey === null) {
      return null;
    }

    return monthsForSelectedYear.find((group) => group.key === selectedMonthKey) ?? null;
  }, [monthsForSelectedYear, selectedMonthKey]);

  const visibleItems = selectedGroup?.items ?? [];

  useEffect(() => {
    if (availableYears.length === 0) {
      setSelectedYear(null);
      return;
    }

    setSelectedYear((current) => {
      if (current !== null && availableYears.includes(current)) {
        return current;
      }

      return availableYears[0];
    });
  }, [availableYears]);

  useEffect(() => {
    if (monthsForSelectedYear.length === 0) {
      setSelectedMonthKey(null);
      return;
    }

    setSelectedMonthKey((current) => {
      if (current !== null && monthsForSelectedYear.some((group) => group.key === current)) {
        return current;
      }

      return monthsForSelectedYear[0].key;
    });
  }, [monthsForSelectedYear]);

  useEffect(() => {
    if (!expandedId) {
      return;
    }

    if (!visibleItems.some((item) => item.id === expandedId)) {
      setExpandedId(null);
      setDetailError(null);
    }
  }, [expandedId, visibleItems]);

  const openReceipt = async (recibo: ReciboItem, action: 'download' | 'sign') => {
    try {
      await logReciboSueldoView('open', recibo.id);
    } catch (err) {
      logger.captureError(err, 'ReciboSueldo.openReceipt');
    }

    const basePdfUrl = recibo.pdfUrl ?? getReciboPdfUrl(recibo.id);
    const targetUrl = action === 'download'
      ? `${basePdfUrl}${basePdfUrl.includes('?') ? '&' : '?'}download=true`
      : basePdfUrl;

    window.open(targetUrl, '_blank', 'noopener,noreferrer');
  };

  const handleToggleDetail = async (recibo: ReciboItem) => {
    if (expandedId === recibo.id) {
      setExpandedId(null);
      setDetailError(null);
      return;
    }

    setExpandedId(recibo.id);
    setDetailError(null);

    if (detailById[recibo.id]) {
      return;
    }

    setDetailLoadingId(recibo.id);
    try {
      const detail = await getReciboDetalle(recibo.id);
      setDetailById((current) => ({ ...current, [recibo.id]: detail }));
    } catch (err) {
      logger.captureError(err, 'ReciboSueldo.getReciboDetalle');
      setDetailError('No pudimos cargar el detalle de este recibo. Intentá nuevamente.');
    } finally {
      setDetailLoadingId((current) => (current === recibo.id ? null : current));
    }
  };

  return (
    <div className="page-container">
      <nav className="navbar">
        <div className="nav-content">
          <h1>Portal Empleado</h1>
          <div className="nav-actions">
            <button onClick={() => navigate('/')} className="nav-link">
              Inicio
            </button>
            {features.gestion && (
              <button onClick={() => navigate('/gestion')} className="nav-link">
                Gestion
              </button>
            )}
            {features.vacaciones && (
              <button onClick={() => navigate('/vacaciones')} className="nav-link">
                Vacaciones
              </button>
            )}
          </div>
        </div>
      </nav>

      <main className="main-content">
        <section className="receipt-page-card" aria-live="polite">
          <h2>Mis recibos de sueldo</h2>

          {loading && (
            <p className="state-message">Estamos cargando tus recibos. Esto puede tardar unos segundos.</p>
          )}

          {!loading && error && (
            <p className="state-message is-error">{error}</p>
          )}

          {!loading && !error && periodGroups.length === 0 && (
            <p className="state-message">No hay recibos disponibles.</p>
          )}

          {!loading && !error && periodGroups.length > 0 && (
            <div className="receipt-layout">
              <aside className="receipt-period-nav" aria-label="Navegación por período">
                <section className="period-nav-section">
                  <h3 className="period-nav-title">Año</h3>
                  <div className="year-list" role="tablist" aria-label="Años disponibles">
                    {availableYears.map((year) => (
                      <button
                        key={year}
                        type="button"
                        role="tab"
                        aria-selected={selectedYear === year}
                        className={`year-button ${selectedYear === year ? 'is-active' : ''}`}
                        onClick={() => setSelectedYear(year)}
                      >
                        {year}
                      </button>
                    ))}
                  </div>
                </section>

                <section className="period-nav-section">
                  <h3 className="period-nav-title">Mes</h3>
                  <div className="month-list" role="tablist" aria-label="Meses disponibles">
                    {monthsForSelectedYear.map((group) => (
                      <button
                        key={group.key}
                        type="button"
                        role="tab"
                        aria-selected={selectedMonthKey === group.key}
                        className={`month-button ${selectedMonthKey === group.key ? 'is-active' : ''}`}
                        onClick={() => setSelectedMonthKey(group.key)}
                      >
                        <span className="month-button-label">{group.monthLabel}</span>
                        <span className="month-button-count">{group.items.length}</span>
                      </button>
                    ))}
                  </div>
                </section>
              </aside>

              <section className="receipt-content" aria-label="Recibos del período seleccionado">
                {selectedGroup && (
                  <header className="receipt-content-header">
                    <h3 className="receipt-selected-period">{selectedGroup.periodLabel}</h3>
                    <p className="receipt-selected-subtitle">
                      {selectedGroup.items.length} recibo{selectedGroup.items.length === 1 ? '' : 's'} por cargo
                    </p>
                  </header>
                )}

                {visibleItems.length === 0 && (
                  <p className="state-message">No hay recibos disponibles para este período.</p>
                )}

                {visibleItems.length > 0 && (
                  <div className="receipt-list" role="list">
                    {visibleItems.map((recibo) => {
                      const status = toHumanStatus(recibo.estado);
                      const canSign = status === 'Pendiente de firma';
                      const detail = detailById[recibo.id];
                      const isExpanded = expandedId === recibo.id;
                      const isDetailLoading = detailLoadingId === recibo.id;

                      return (
                        <article className="receipt-card" role="listitem" key={recibo.id}>
                          <h4 className="receipt-company">{recibo.establecimiento ?? 'Establecimiento'}</h4>
                          <p className="receipt-role">{recibo.cargo ?? 'Cargo no informado'}</p>
                          <p className="receipt-card-meta">Emitido: {formatDate(recibo.fechaEmision)}</p>
                          <p className={`receipt-status ${statusToClassName(status)}`}>
                            Estado: {status}
                          </p>
                          <p className="receipt-liquid-label">Líquido a cobrar</p>
                          <p className="receipt-liquid-value">{formatMoney(recibo.importe, recibo.moneda)}</p>

                          <div className="receipt-actions">
                            <button
                              type="button"
                              className="receipt-action primary"
                              onClick={() => handleToggleDetail(recibo)}
                            >
                              {isExpanded ? 'Ocultar detalle' : 'Ver detalle'}
                            </button>

                            <button
                              type="button"
                              className="receipt-action secondary"
                              onClick={() => openReceipt(recibo, 'download')}
                            >
                              Descargar PDF
                            </button>

                            <button
                              type="button"
                              className="receipt-action accent"
                              onClick={() => openReceipt(recibo, 'sign')}
                              disabled={!canSign}
                              title={canSign ? 'Firmar recibo' : 'Este recibo no requiere firma'}
                            >
                              Firmar
                            </button>
                          </div>

                          {isExpanded && (
                            <section className="receipt-detail-panel">
                              {isDetailLoading && (
                                <p className="state-message">Cargando detalle del recibo...</p>
                              )}

                              {!isDetailLoading && detailError && (
                                <p className="state-message is-error">{detailError}</p>
                              )}

                              {!isDetailLoading && detail && (
                                <>
                                  <div className="receipt-meta-grid">
                                    <p><strong>Periodo:</strong> {detail.periodo}</p>
                                    <p><strong>Fecha de emisión:</strong> {formatDate(detail.fechaEmision)}</p>
                                    <p><strong>Fecha de ingreso:</strong> {formatMaybeDate(detail.fechaIngreso)}</p>
                                    <p><strong>Días trabajados:</strong> {detail.diasTrabajados ?? '-'}</p>
                                  </div>

                                  <div className="receipt-tables">
                                    <div>
                                      <h4>Haberes</h4>
                                      <table>
                                        <thead>
                                          <tr>
                                            <th>Código</th>
                                            <th>Concepto</th>
                                            <th className="amount-col">Monto</th>
                                          </tr>
                                        </thead>
                                        <tbody>
                                          {detail.haberes.length === 0 && (
                                            <tr>
                                              <td colSpan={3} className="empty-row">Sin haberes cargados.</td>
                                            </tr>
                                          )}
                                          {detail.haberes.map((item, index) => (
                                            <tr key={`${detail.id}-hab-${item.codigo}-${index}`}>
                                              <td>{item.codigo}</td>
                                              <td>{item.concepto}</td>
                                              <td className="amount-col">{formatMoney(item.monto, 'ARS')}</td>
                                            </tr>
                                          ))}
                                        </tbody>
                                      </table>
                                    </div>

                                    <div>
                                      <h4>Descuentos</h4>
                                      <table>
                                        <thead>
                                          <tr>
                                            <th>Código</th>
                                            <th>Concepto</th>
                                            <th className="amount-col">Monto</th>
                                          </tr>
                                        </thead>
                                        <tbody>
                                          {detail.descuentos.length === 0 && (
                                            <tr>
                                              <td colSpan={3} className="empty-row">Sin descuentos cargados.</td>
                                            </tr>
                                          )}
                                          {detail.descuentos.map((item, index) => (
                                            <tr key={`${detail.id}-des-${item.codigo}-${index}`}>
                                              <td>{item.codigo}</td>
                                              <td>{item.concepto}</td>
                                              <td className="amount-col">{formatMoney(item.monto, 'ARS')}</td>
                                            </tr>
                                          ))}
                                        </tbody>
                                      </table>
                                    </div>
                                  </div>

                                  <div className="receipt-totals">
                                    <p><strong>Bruto:</strong> {formatMoney(detail.bruto, 'ARS')}</p>
                                    <p><strong>Total descuentos:</strong> {formatMoney(detail.totalDescuentos, 'ARS')}</p>
                                    <p className="liquido-total"><strong>Líquido:</strong> {formatMoney(detail.liquido, 'ARS')}</p>
                                    {detail.liquidoPalabras && (
                                      <p className="liquido-palabras"><strong>En palabras:</strong> {detail.liquidoPalabras}</p>
                                    )}
                                  </div>
                                </>
                              )}
                            </section>
                          )}
                        </article>
                      );
                    })}
                  </div>
                )}
              </section>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

function buildPeriodMetadata(recibo: ReciboItem): Omit<PeriodGroup, 'items'> {
  const period = parsePeriod(recibo);
  const year = period?.year ?? 1970;
  const month = period?.month ?? 1;
  const monthLabel = monthToLabel(month);

  return {
    key: `${year}-${String(month).padStart(2, '0')}`,
    year,
    month,
    monthLabel,
    periodLabel: `${monthLabel} ${year}`,
    order: toOrderTimestamp(recibo, year, month),
  };
}

function parsePeriod(recibo: ReciboItem): { year: number; month: number } | null {
  const fromDate = parseDateWithFallback(recibo.fechaEmision);
  if (fromDate !== null) {
    return {
      year: fromDate.getFullYear(),
      month: fromDate.getMonth() + 1,
    };
  }

  const rawPeriod = (recibo.periodo ?? '').trim();
  if (rawPeriod.length === 0) {
    return null;
  }

  const numericPeriodMatch = rawPeriod.match(/^(\d{1,2})[-/](\d{4})$/);
  if (numericPeriodMatch) {
    const month = Number.parseInt(numericPeriodMatch[1], 10);
    const year = Number.parseInt(numericPeriodMatch[2], 10);
    if (month >= 1 && month <= 12) {
      return { year, month };
    }
  }

  const reverseNumericPeriodMatch = rawPeriod.match(/^(\d{4})[-/](\d{1,2})$/);
  if (reverseNumericPeriodMatch) {
    const year = Number.parseInt(reverseNumericPeriodMatch[1], 10);
    const month = Number.parseInt(reverseNumericPeriodMatch[2], 10);
    if (month >= 1 && month <= 12) {
      return { year, month };
    }
  }

  const normalizedPeriod = normalizeText(rawPeriod);
  const yearMatch = normalizedPeriod.match(/(19|20)\d{2}/);
  if (!yearMatch) {
    return null;
  }

  const year = Number.parseInt(yearMatch[0], 10);
  const month = Object.entries(SPANISH_MONTHS).find(([token]) => normalizedPeriod.includes(token))?.[1];

  if (!month) {
    return null;
  }

  return { year, month };
}

function toOrderTimestamp(recibo: ReciboItem, fallbackYear?: number, fallbackMonth?: number): number {
  const parsed = parseDateWithFallback(recibo.fechaEmision);
  if (parsed !== null) {
    return parsed.getTime();
  }

  const year = fallbackYear ?? 1970;
  const month = fallbackMonth ?? 1;
  return Date.UTC(year, month - 1, 1);
}

function parseDateWithFallback(value: string): Date | null {
  const parsed = new Date(value);
  if (!Number.isNaN(parsed.getTime())) {
    return parsed;
  }

  const slashDateMatch = value.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/);
  if (!slashDateMatch) {
    return null;
  }

  const day = Number.parseInt(slashDateMatch[1], 10);
  const month = Number.parseInt(slashDateMatch[2], 10);
  const year = Number.parseInt(slashDateMatch[3], 10);
  const fallbackDate = new Date(year, month - 1, day);

  if (Number.isNaN(fallbackDate.getTime())) {
    return null;
  }

  return fallbackDate;
}

function monthToLabel(month: number): string {
  return MONTH_LABELS[month - 1] ?? 'Mes';
}

function normalizeText(value: string): string {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();
}

function formatDate(value: string): string {
  const date = parseDateWithFallback(value);
  if (date === null) {
    return '-';
  }

  return date.toLocaleDateString('es-AR');
}

function formatMaybeDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  const parsed = parseDateWithFallback(value);
  if (parsed !== null) {
    return parsed.toLocaleDateString('es-AR');
  }

  return value;
}

function formatMoney(amount: number, currency: string): string {
  const safeCurrency = typeof currency === 'string' && currency.trim().length > 0
    ? currency.toUpperCase()
    : 'ARS';

  return new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: safeCurrency,
    maximumFractionDigits: 2,
  }).format(amount);
}

function toHumanStatus(rawStatus: string): HumanReceiptStatus {
  const status = rawStatus.toLowerCase();

  if (status.includes('firm')) {
    return 'Firmado';
  }

  if (status.includes('pend')) {
    return 'Pendiente de firma';
  }

  return 'Disponible';
}

function statusToClassName(status: HumanReceiptStatus): string {
  if (status === 'Firmado') {
    return 'is-signed';
  }

  if (status === 'Pendiente de firma') {
    return 'is-pending';
  }

  return 'is-available';
}

function toFriendlyLoadMessage(err: unknown): string {
  if (err instanceof Error) {
    const message = err.message.toLowerCase();

    if (message.includes('unauthorized') || message.includes('401')) {
      return 'Tu sesión venció. Ingresá nuevamente para ver tus recibos.';
    }

    if (message.includes('employee')) {
      return 'No pudimos encontrar tus datos de empleado. Contactá a RR.HH.';
    }
  }

  return 'No pudimos cargar tus recibos en este momento. Intentá nuevamente en unos minutos.';
}

const MONTH_LABELS = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
] as const;

const SPANISH_MONTHS: Record<string, number> = {
  enero: 1,
  ene: 1,
  febrero: 2,
  feb: 2,
  marzo: 3,
  mar: 3,
  abril: 4,
  abr: 4,
  mayo: 5,
  jun: 6,
  junio: 6,
  jul: 7,
  julio: 7,
  agosto: 8,
  ago: 8,
  septiembre: 9,
  setiembre: 9,
  sep: 9,
  set: 9,
  octubre: 10,
  oct: 10,
  noviembre: 11,
  nov: 11,
  diciembre: 12,
  dic: 12,
};
