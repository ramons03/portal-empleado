import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getCurrentUser, logout } from '../services/auth';
import type { User } from '../types';
import './Home.css';

export default function Home() {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
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
        setError('Error al cargar la información del usuario');
        console.error(err);
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
      console.error('Error al cerrar sesión:', err);
      // Even if logout fails, redirect to login
      navigate('/login');
    }
  };

  if (loading) {
    return (
      <div className="home-container">
        <div className="loading">Cargando...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="home-container">
        <div className="error">{error}</div>
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
            <button onClick={() => navigate('/vacaciones')} className="nav-link">
              Vacaciones
            </button>
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
            </div>
          </div>

          <div className="quick-actions">
            <h3>Acciones Rápidas</h3>
            <div className="action-cards">
              <div className="action-card" onClick={() => navigate('/recibos')}>
                <h4>📄 Recibos de Nómina</h4>
                <p>Ver tus recibos de pago</p>
              </div>
              <div className="action-card" onClick={() => navigate('/vacaciones')}>
                <h4>🏖️ Vacaciones</h4>
                <p>Solicitar días de vacaciones</p>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
