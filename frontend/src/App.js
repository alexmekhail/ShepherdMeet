import React, { useState, useEffect, useCallback } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import PriestAvailabilityForm from './PriestAvailabilityForm';
import UserForm from './UserForm';
import ConfirmationPage from './ConfirmationPage';
import AppointmentConfirmed from './AppointmentConfirmed';
import Login from './login';
import Sidebar from './Sidebar';
import { useTheme } from './useTheme';
import './theme.css';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209';

const App = () => {
  const { theme, toggle: toggleTheme } = useTheme();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  // Incrementing this tells the Sidebar to re-fetch appointments
  const [appointmentVersion, setAppointmentVersion] = useState(0);

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const response = await fetch(`${API_URL}/profile`, {
          credentials: 'include',
        });
        if (response.ok) {
          const data = await response.json();
          setProfile(data);
          setIsAuthenticated(true);
        } else {
          setIsAuthenticated(false);
        }
      } catch {
        setIsAuthenticated(false);
      } finally {
        setLoading(false);
      }
    };
    checkAuth();
  }, []);

  const handleGuestLogin = () => {
    setProfile({ firstName: 'Guest', lastName: '', email: '' });
    setIsAuthenticated(true);
  };

  const handleSignOut = () => {
    setIsAuthenticated(false);
    setProfile(null);
  };

  const handleAppointmentBooked = useCallback(() => {
    setAppointmentVersion((v) => v + 1);
  }, []);

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', marginTop: '20%', fontSize: '1.2rem' }}>
        Loading...
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Login onLoginSuccess={() => setIsAuthenticated(true)} onGuestLogin={handleGuestLogin} />;
  }

  const isAdmin = profile?.email?.toLowerCase() === 'alexmekhail10@gmail.com';

  return (
    <>
      <Sidebar
        profile={profile}
        onSignOut={handleSignOut}
        appointmentVersion={appointmentVersion}
        theme={theme}
        onToggleTheme={toggleTheme}
      />
      <Routes>
        <Route
          path="/"
          element={
            isAdmin
              ? <PriestAvailabilityForm profile={profile} />
              : <UserForm profile={profile} />
          }
        />
        <Route path="/confirmation" element={<ConfirmationPage onBooked={handleAppointmentBooked} />} />
        <Route path="/confirmed" element={<AppointmentConfirmed />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
};

export default App;
