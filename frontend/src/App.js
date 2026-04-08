import React, { useState, useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import PriestAvailabilityForm from './PriestAvailabilityForm';
import UserForm from './UserForm';
import ConfirmationPage from './ConfirmationPage';
import Login from './login';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209';

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const response = await fetch(`${API_URL}/profile`, {
          credentials: 'include',
        });
        setIsAuthenticated(response.ok);
      } catch {
        setIsAuthenticated(false);
      } finally {
        setLoading(false);
      }
    };
    checkAuth();
  }, []);

  if (loading) {
    return <div style={{ display: 'flex', justifyContent: 'center', marginTop: '20%', fontSize: '1.2rem' }}>Loading...</div>;
  }

  if (!isAuthenticated) {
    return <Login onLoginSuccess={() => setIsAuthenticated(true)} />;
  }

  return (
    <Routes>
      <Route path="/" element={<PriestAvailabilityForm />} />
      <Route path="/schedule" element={<UserForm />} />
      <Route path="/confirmation" element={<ConfirmationPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default App;
