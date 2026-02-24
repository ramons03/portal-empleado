import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  getReciboPdfUrl,
  getReciboSueldo,
  logReciboSueldoView,
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

  const openReceipt = async (recibo: ReciboItem, action: 'view' | 'download' | 'sign') => {
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
            <p className="state-message">
              Todavía no encontramos recibos para mostrar. Cuando estén disponibles, van a aparecer acá.
            </p>
          )}

          {!loading && !error && sortedItems.length > 0 && (
            <div className="receipt-list" role="list">
              {sortedItems.map((recibo) => {
                const status = toHumanStatus(recibo.estado);
                const signable = status === 'Pendiente de firma';

                return (
                  <article className="receipt-card" role="listitem" key={recibo.id}>
                    <h3 className="receipt-period">{getPeriodLabel(recibo)}</h3>
                    <p className="receipt-detail">
                      <span className="receipt-label">Emitido:</span> {formatDate(recibo.fechaEmision)}
                    </p>
                    <p className="receipt-detail">
                      <span className="receipt-label">Neto a cobrar:</span> {formatMoney(recibo.importe, recibo.moneda)}
                    </p>
                    <p className={`receipt-status ${statusToClassName(status)}`}>
                      Estado: {status}
                    </p>

                    <div className="receipt-actions">
                      <button
                        type="button"
                        className="receipt-action primary"
                        onClick={() => openReceipt(recibo, 'view')}
                      >
                        Ver recibo
                      </button>

                      {signable ? (
                        <button
                          type="button"
                          className="receipt-action accent"
                          onClick={() => openReceipt(recibo, 'sign')}
                        >
                          Firmar
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="receipt-action secondary"
                          onClick={() => openReceipt(recibo, 'download')}
                        >
                          Descargar PDF
                        </button>
                      )}
                    </div>
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
