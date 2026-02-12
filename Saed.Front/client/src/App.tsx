import React, { useState } from 'react';
import './App.css';
import axios from 'axios';

interface UserData {
  email: string;
  name: string;
  policy: string;
}

interface SecureData {
  message: string;
  timestamp: string;
  user: UserData;
}

function App() {
  const [jwtToken, setJwtToken] = useState<string>('');
  const [secureData, setSecureData] = useState<SecureData | null>(null);
  const [error, setError] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);

  const authBaseUrl = 'https://localhost:5001';
  const apiBaseUrl = 'https://localhost:5002';

  const handleTokenSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSecureData(null);
    if (jwtToken.trim()) {
      alert('Token stored in memory! You can now call the API.');
    }
  };

  const callSecureApi = async () => {
    if (!jwtToken.trim()) {
      setError('Please enter a JWT token first');
      return;
    }

    setIsLoading(true);
    setError('');
    setSecureData(null);

    try {
      const response = await axios.get(`${apiBaseUrl}/api/secure`, {
        headers: {
          'Authorization': `Bearer ${jwtToken}`
        }
      });
      setSecureData(response.data);
    } catch (err: any) {
      if (err.response) {
        setError(`API Error: ${err.response.status} - ${err.response.statusText}`);
      } else {
        setError(`Network Error: ${err.message}`);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const clearToken = () => {
    setJwtToken('');
    setSecureData(null);
    setError('');
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>Saed Security PoC - Frontend</h1>
        <p>React Application for Testing Authentication</p>
      </header>

      <main className="App-main">
        {/* Login Section */}
        <section className="section">
          <h2>Step 1: Login with Google</h2>
          <p>Choose a domain policy and login via the Auth service:</p>
          <div className="button-group">
            <a 
              href={`${authBaseUrl}/Auth/GoogleLogin?policy=Strict`}
              className="btn btn-danger"
              target="_blank"
              rel="noopener noreferrer"
            >
              Login with Strict Policy
            </a>
            <a 
              href={`${authBaseUrl}/Auth/GoogleLogin?policy=Edu`}
              className="btn btn-primary"
              target="_blank"
              rel="noopener noreferrer"
            >
              Login with Edu Policy
            </a>
            <a 
              href={`${authBaseUrl}/Auth/GoogleLogin?policy=Public`}
              className="btn btn-success"
              target="_blank"
              rel="noopener noreferrer"
            >
              Login with Public Policy
            </a>
          </div>
          <p className="info-text">
            After login, copy the JWT token from the authentication page.
          </p>
        </section>

        {/* Token Input Section */}
        <section className="section">
          <h2>Step 2: Enter JWT Token</h2>
          <form onSubmit={handleTokenSubmit} className="token-form">
            <div className="form-group">
              <label htmlFor="jwtToken">JWT Token:</label>
              <textarea
                id="jwtToken"
                value={jwtToken}
                onChange={(e) => setJwtToken(e.target.value)}
                placeholder="Paste your JWT token here..."
                rows={6}
                className="token-input"
              />
            </div>
            <div className="button-group">
              <button type="submit" className="btn btn-primary">
                Store Token in Memory
              </button>
              {jwtToken && (
                <button type="button" onClick={clearToken} className="btn btn-secondary">
                  Clear Token
                </button>
              )}
            </div>
          </form>
        </section>

        {/* API Call Section */}
        <section className="section">
          <h2>Step 3: Call Protected API</h2>
          <button 
            onClick={callSecureApi} 
            disabled={!jwtToken || isLoading}
            className="btn btn-success"
          >
            {isLoading ? 'Calling API...' : 'Call /api/secure'}
          </button>
        </section>

        {/* Results Section */}
        {error && (
          <section className="section error-section">
            <h3>Error</h3>
            <div className="error-message">{error}</div>
          </section>
        )}

        {secureData && (
          <section className="section success-section">
            <h3>API Response - Secure Data</h3>
            <div className="response-data">
              <p><strong>Message:</strong> {secureData.message}</p>
              <p><strong>Timestamp:</strong> {secureData.timestamp}</p>
              <h4>User Information:</h4>
              <ul>
                <li><strong>Email:</strong> {secureData.user.email}</li>
                <li><strong>Name:</strong> {secureData.user.name}</li>
                <li><strong>Policy:</strong> {secureData.user.policy}</li>
              </ul>
            </div>
          </section>
        )}

        {/* Info Section */}
        <section className="section info-section">
          <h3>How It Works</h3>
          <ol>
            <li>Click a login button to authenticate with Google via Saed.Auth (opens in new tab)</li>
            <li>After successful authentication, copy the JWT token from the auth page</li>
            <li>Paste the token in the text area above and click "Store Token in Memory"</li>
            <li>Click "Call /api/secure" to make an authenticated request to Saed.Api</li>
            <li>The API validates the JWT and returns secure data</li>
          </ol>
          <p className="note">
            <strong>Note:</strong> Google tokens are never exposed. Only internal JWT tokens 
            (issued by saed-auth for saed-api) are used.
          </p>
        </section>
      </main>
    </div>
  );
}

export default App;
