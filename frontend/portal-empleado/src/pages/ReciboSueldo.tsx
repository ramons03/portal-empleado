import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getReciboPdfUrl, getReciboSueldo, logReciboSueldoView, type ReciboItem } from '../services/recibo-sueldo';
import { logger } from '../services/logger';
import type { FeatureFlags } from '../config/features';
import './ReciboSueldo.css';

type ReciboSueldoProps = {
  features: FeatureFlags;
};

export default function ReciboSueldo({ features }: ReciboSueldoProps) {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<ReciboItem[]>([]);
  const [selectedReciboId, setSelectedReciboId] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        await logReciboSueldoView('page');
        const data = await getReciboSueldo();
        setItems(data.items);
        if (data.items.length > 0) {
          setSelectedReciboId(data.items[0].id);
        }
      } catch (err) {
        logger.captureError(err, 'ReciboSueldo.load');
        const msg = err instanceof Error ? err.message : 'No se pudo cargar ReciboSueldo.';
        setError(msg);
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  const selectedRecibo = items.find((item) => item.id === selectedReciboId) ?? null;
  const selectedPdfUrl = selectedRecibo
    ? (selectedRecibo.pdfUrl ?? getReciboPdfUrl(selectedRecibo.id))
    : null;

  const handleSelect = async (recibo: ReciboItem) => {
    setSelectedReciboId(recibo.id);

    try {
      await logReciboSueldoView('open', recibo.id);
    } catch (err) {
      logger.captureError(err, 'ReciboSueldo.handleSelect');
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
        <div className="page-card">
          <h2>📄 ReciboSueldo</h2>
          {loading && <p className="placeholder-text">Cargando ReciboSueldo...</p>}
          {error && <p className="placeholder-text">{error}</p>}
          {!loading && !error && items.length === 0 && (
            <p className="placeholder-text">No hay elementos de ReciboSueldo para mostrar.</p>
          )}
          {!loading && !error && items.length > 0 && (
            <div className="recibo-sueldo-layout">
              <div className="recibo-sueldo-list">
                {items.map((recibo) => (
                  <button
                    key={recibo.id}
                    type="button"
                    className={`recibo-list-item ${selectedReciboId === recibo.id ? 'is-active' : ''}`}
                    onClick={() => handleSelect(recibo)}
                  >
                    <div className="recibo-info">
                      <h3>{recibo.periodo}</h3>
                      <p>Importe: {recibo.moneda} {recibo.importe.toLocaleString('es-AR')}</p>
                      <p>Estado: {recibo.estado}</p>
                      <p>Fecha emisión: {new Date(recibo.fechaEmision).toLocaleDateString('es-AR')}</p>
                    </div>
                  </button>
                ))}
              </div>

              <div className="recibo-viewer">
                {selectedRecibo && selectedPdfUrl ? (
                  <>
                    <div className="recibo-viewer-header">
                      <h3>{selectedRecibo.periodo}</h3>
                      <button
                        type="button"
                        className="recibo-button"
                        onClick={() => window.open(selectedPdfUrl, '_blank', 'noopener,noreferrer')}
                      >
                        Abrir aparte
                      </button>
                    </div>
                    <iframe
                      className="recibo-pdf-frame"
                      src={selectedPdfUrl}
                      title={`Recibo ${selectedRecibo.periodo}`}
                    />
                  </>
                ) : (
                  <p className="placeholder-text">Seleccioná un recibo para visualizar el PDF.</p>
                )}
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
