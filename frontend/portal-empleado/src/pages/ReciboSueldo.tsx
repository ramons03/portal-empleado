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

export default function ReciboSueldo({ features }: ReciboSueldoProps) {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<ReciboItem[]>([]);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [detailById, setDetailById] = useState<Record<string, ReciboDetalleResponse>>({});
  const [detailLoadingId, setDetailLoadingId] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);

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
      const dateA = new Date(a.fechaEmision).getTime();
      const dateB = new Date(b.fechaEmision).getTime();
      return dateB - dateA;
    });
  }, [items]);

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

          {!loading && !error && sortedItems.length === 0 && (
            <p className="state-message">No hay recibos disponibles.</p>
          )}

          {!loading && !error && sortedItems.length > 0 && (
            <div className="receipt-list" role="list">
              {sortedItems.map((recibo) => {
                const status = toHumanStatus(recibo.estado);
                const canSign = status === 'Pendiente de firma';
                const detail = detailById[recibo.id];
                const isExpanded = expandedId === recibo.id;
                const isDetailLoading = detailLoadingId === recibo.id;

                return (
                  <article className="receipt-card" role="listitem" key={recibo.id}>
                    <h3 className="receipt-company">{recibo.establecimiento ?? 'Establecimiento'}</h3>
                    <p className="receipt-role">{recibo.cargo ?? 'Cargo no informado'}</p>
                    <p className="receipt-detail">
                      <span className="receipt-label">Periodo:</span> {getPeriodLabel(recibo)}
                    </p>
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
      </main>
    </div>
  );
}

function getPeriodLabel(recibo: ReciboItem): string {
  if (typeof recibo.periodo === 'string' && recibo.periodo.trim().length > 0) {
    return recibo.periodo;
  }

  const date = new Date(recibo.fechaEmision);
  return date.toLocaleDateString('es-AR', { month: 'long', year: 'numeric' });
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return date.toLocaleDateString('es-AR');
}

function formatMaybeDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (!Number.isNaN(parsed.getTime())) {
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
