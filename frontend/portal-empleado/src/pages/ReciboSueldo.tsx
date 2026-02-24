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

  useEffect(() => {
    const load = async () => {
      try {
        await logReciboSueldoView('page');
        const data = await getReciboSueldo();
        setItems(data.items);
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

  const handleView = async (recibo: ReciboItem) => {
    try {
      await logReciboSueldoView('open', recibo.id);
      const pdfUrl = recibo.pdfUrl ?? getReciboPdfUrl(recibo.id);
      const popup = window.open(pdfUrl, '_blank', 'noopener,noreferrer');
      if (!popup) {
        window.location.href = pdfUrl;
      }
    } catch (err) {
      logger.captureError(err, 'ReciboSueldo.handleView');
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
            <div className="recibo-sueldo-list">
              {items.map((recibo) => (
                <div key={recibo.id} className="recibo-card">
                  <div className="recibo-info">
                    <h3>{recibo.periodo}</h3>
                    <p>Importe: {recibo.moneda} {recibo.importe.toLocaleString('es-AR')}</p>
                    <p>Estado: {recibo.estado}</p>
                    <p>Fecha emisión: {new Date(recibo.fechaEmision).toLocaleDateString('es-AR')}</p>
                  </div>
                  <button className="recibo-button" onClick={() => handleView(recibo)}>
                    Ver PDF
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
