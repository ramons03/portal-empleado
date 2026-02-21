import { useNavigate } from 'react-router-dom';
import './Vacaciones.css';

export default function Vacaciones() {
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
            <button onClick={() => navigate('/gestion')} className="nav-link">
              Gestion
            </button>
            <button onClick={() => navigate('/recibos')} className="nav-link">
              Recibos
            </button>
          </div>
        </div>
      </nav>

      <main className="main-content">
        <div className="page-card">
          <h2>🏖️ Solicitud de Vacaciones</h2>
          <p className="placeholder-text">
            Esta sección estará disponible próximamente.
          </p>
          <p className="placeholder-text">
            Aquí podrás solicitar y gestionar tus días de vacaciones.
          </p>
          <div className="placeholder-box">
            <p>🔄 Función en desarrollo</p>
            <p>Se integrará con el microservicio de Asistencia</p>
          </div>
        </div>
      </main>
    </div>
  );
}
