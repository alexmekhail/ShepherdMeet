import React, { useEffect, useState } from 'react';
import './Login.css';
import logo from './spsdlogo.png';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209';

const Login = ({ onAuthenticated }) => {
  const [demoLoading, setDemoLoading] = useState(null);

  const handleGoogleSignIn = () => {
    window.location.href = `${API_URL}/login`;
  };

  const handleDemoLogin = async (role) => {
    setDemoLoading(role);
    try {
      const response = await fetch(`${API_URL}/auth/demo-login`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ role }),
      });
      if (response.ok) {
        onAuthenticated();
      }
    } catch (error) {
      console.error('Demo login failed:', error);
    } finally {
      setDemoLoading(null);
    }
  };

  useEffect(() => {
    const checkLoginStatus = async () => {
      try {
        const response = await fetch(`${API_URL}/profile`, {
          method: 'GET',
          credentials: 'include',
        });

        if (response.ok) {
          const profile = await response.json();
          if (profile && profile.email) {
            onAuthenticated();
          }
        }
      } catch (error) {
        console.error('Error checking login status:', error);
      }
    };

    const intervalId = setInterval(checkLoginStatus, 2000);

    return () => clearInterval(intervalId);
  }, [onAuthenticated]);

  return (
    <div className="login-container">
      <img src={logo} alt="Logo" className="logo" style={{ width: '300px', height: 'auto' }} />
      <h2>ShepherdMeet: Schedule a Meeting</h2>

      <button className="gsi-material-button" style={{ width: '400px', padding: '16px' }} onClick={handleGoogleSignIn}>
        <div className="gsi-material-button-state"></div>
        <div className="gsi-material-button-content-wrapper">
          <div className="gsi-material-button-icon">
            <svg
              version="1.1"
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 48 48"
              xmlnsXlink="http://www.w3.org/1999/xlink"
              style={{ display: 'block', width: '24px', height: '24px' }}
            >
              <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"></path>
              <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"></path>
              <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"></path>
              <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"></path>
              <path fill="none" d="M0 0h48v48H0z"></path>
            </svg>
          </div>
          <span className="gsi-material-button-contents" style={{ fontSize: '18px' }}>Sign in with Google</span>
          <span style={{ display: 'none' }}>Sign in with Google</span>
        </div>
      </button>

      <div className="demo-section">
        <div className="demo-divider">or try it instantly</div>
        <button
          className="demo-button"
          onClick={() => handleDemoLogin('parishioner')}
          disabled={demoLoading !== null}
        >
          {demoLoading === 'parishioner' ? 'Signing in…' : 'Demo as Parishioner'}
        </button>
        <button
          className="demo-button"
          onClick={() => handleDemoLogin('admin')}
          disabled={demoLoading !== null}
        >
          {demoLoading === 'admin' ? 'Signing in…' : 'Demo as Priest (Admin)'}
        </button>
      </div>
    </div>
  );
};

export default Login;
