import { getAuthModeLabel, getLoginUrl, isDevAuth } from '../services/auth';
import { useSearchParams } from 'react-router-dom';
import './Login.css';

export default function Login() {
  const [searchParams] = useSearchParams();
  const errorCode = searchParams.get('error');
  const requiredDomain = searchParams.get('requiredDomain') ?? 'saed.ar';

  const errorMessage =
    errorCode === 'domain_not_allowed'
      ? `Debes iniciar sesión con una cuenta @${requiredDomain}. Si Google te abre otra cuenta, elige "Usar otra cuenta" o cierra sesión de esa cuenta primero.`
      : null;

  const handleLogin = () => {
    // Redirect to the backend login endpoint
    window.location.href = getLoginUrl();
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <h1>Portal Empleado</h1>
        <p>Por favor, inicia sesión para continuar</p>
        {errorMessage && <p className="login-warning">{errorMessage}</p>}
        <button onClick={handleLogin} className="login-button">
          {!isDevAuth() && (
            <img
              src="https://www.google.com/favicon.ico"
              alt="Google logo"
              className="google-logo"
            />
          )}
          {getAuthModeLabel()}
        </button>
      </div>
    </div>
  );
}
