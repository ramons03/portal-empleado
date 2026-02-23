import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getCurrentUser, logout } from '../services/auth';
import { logger } from '../services/logger';
import type { User } from '../types';
import './Home.css';
import type { FeatureFlags } from '../config/features';
import { updateCuil } from '../services/profile';

type HomeProps = {
  features: FeatureFlags;
};

export default function Home({ features }: HomeProps) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [cuilInput, setCuilInput] = useState('');
  const [cuilSaving, setCuilSaving] = useState(false);
  const [cuilError, setCuilError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchUser = async () => {
      try {
        const userData = await getCurrentUser();
        if (userData === null) {
          // User is not authenticated, redirect to login
          navigate('/login');
          return;
        }
        setUser(userData);
      } catch (err) {
        logger.captureError(err, 'Home.fetchUser');
        navigate('/login');
      } finally {
        setLoading(false);
      }
    };

    fetchUser();
  }, [navigate]);

  const handleLogout = async () => {
    try {
      await logout();
      navigate('/login');
    } catch (err) {
      logger.captureError(err, 'Home.handleLogout');
      // Even if logout fails, redirect to login
      navigate('/login');
    }
  };

  const handleSaveCuil = async () => {
    if (!cuilInput.trim()) return;
    try {
      setCuilSaving(true);
      setCuilError(null);
      const saved = await updateCuil(cuilInput.trim());
      setUser((prev) => (prev ? { ...prev, cuil: saved } : prev));
      setCuilInput('');
    } catch (err) {
      logger.captureError(err, 'Home.handleSaveCuil');
      setCuilError(err instanceof Error ? err.message : 'No se pudo guardar el CUIL.');
    } finally {
      setCuilSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="home-container">
        <div className="loading">Cargando...</div>
      </div>
    );
  }

  return (
    <div className="home-container">
      <nav className="navbar">
        <div className="nav-content">
          <h1>Portal Empleado</h1>
          <div className="nav-actions">
            <button onClick={() => navigate('/recibos')} className="nav-link">
              Recibos
            </button>
            <button onClick={() => navigate('/gestion')} className="nav-link">
              Gestion
            </button>
            {features.vacaciones && (
              <button onClick={() => navigate('/vacaciones')} className="nav-link">
                Vacaciones
              </button>
            )}
            <button onClick={handleLogout} className="logout-button">
              Cerrar Sesión
            </button>
          </div>
        </div>
      </nav>

      <main className="main-content">
        <div className="welcome-card">
          <div className="user-info">
            {user?.pictureUrl && (
              <img src={user.pictureUrl} alt="Profile" className="profile-picture" />
            )}
            <div>
              <h2>Bienvenido, {user?.fullName}</h2>
              <p className="user-email">{user?.email}</p>
              <div className="cuil-row">
                <span className="cuil-label">CUIL:</span>
                <span className="cuil-value">{user?.cuil ?? 'No configurado'}</span>
              </div>
              <div className="cuil-form">
                <input
                  type="text"
                  inputMode="numeric"
                  placeholder="Ingresá tu CUIL (11 dígitos)"
                  value={cuilInput}
                  onChange={(e) => setCuilInput(e.target.value)}
                  className="cuil-input"
                />
                <button
                  className="cuil-button"
                  onClick={handleSaveCuil}
                  disabled={cuilSaving || !cuilInput.trim()}
                >
                  {cuilSaving ? 'Guardando...' : 'Guardar'}
                </button>
              </div>
              {cuilError && <p className="cuil-error">{cuilError}</p>}
            </div>
          </div>

          <div className="quick-actions">
            <h3>Acciones Rápidas</h3>
            <div className="action-cards">
              <div className="action-card" onClick={() => navigate('/recibos')}>
                <h4>📄 Recibos de Nómina</h4>
                <p>Ver tus recibos de pago</p>
              </div>
              <div className="action-card" onClick={() => navigate('/gestion')}>
                <h4>🧭 Gestion y RRHH</h4>
                <p>Dependencias, notificaciones y autogestion</p>
              </div>
              {features.vacaciones && (
                <div className="action-card" onClick={() => navigate('/vacaciones')}>
                  <h4>🏖️ Vacaciones</h4>
                  <p>Solicitar días de vacaciones</p>
                </div>
              )}
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
