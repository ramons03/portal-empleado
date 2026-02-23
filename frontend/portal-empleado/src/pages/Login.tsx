import { getAuthModeLabel, getLoginUrl, isDevAuth } from '../services/auth';
import './Login.css';

export default function Login() {
  const handleLogin = () => {
    // Redirect to the backend login endpoint
    window.location.href = getLoginUrl();
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <h1>Portal Empleado</h1>
        <p>Por favor, inicia sesión para continuar</p>
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
