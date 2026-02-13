import { useNavigate } from 'react-router-dom';
import './Recibos.css';

export default function Recibos() {
  const navigate = useNavigate();

  return (
    <div className="page-container">
      <nav className="navbar">
        <div className="nav-content">
          <h1>Portal Empleado</h1>
          <div className="nav-actions">
            <button onClick={() => navigate('/')} className="nav-link">
              Inicio
            </button>
            <button onClick={() => navigate('/vacaciones')} className="nav-link">
              Vacaciones
            </button>
          </div>
        </div>
      </nav>

      <main className="main-content">
        <div className="page-card">
          <h2>📄 Recibos de Nómina</h2>
          <p className="placeholder-text">
            Esta sección estará disponible próximamente.
          </p>
          <p className="placeholder-text">
            Aquí podrás consultar y descargar tus recibos de nómina.
          </p>
          <div className="placeholder-box">
            <p>🔄 Función en desarrollo</p>
            <p>Se integrará con el microservicio de Nómina</p>
          </div>
        </div>
      </main>
    </div>
  );
}
