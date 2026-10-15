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
  // Render's free tier spins the API down after 15 minutes idle; the first
  // request can take ~a minute to wake it back up. Show a notice once the
  // initial auth check has been waiting a while, so it doesn't look stuck.
  const [waking, setWaking] = useState(false);
  // Incrementing this tells the Sidebar to re-fetch appointments
  const [appointmentVersion, setAppointmentVersion] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    // Don't let a slow/unreachable backend hang the app on "Loading…" forever —
    // fall through to the login screen after 70s (enough for a Render cold start).
    const timeout = setTimeout(() => controller.abort(), 70000);
    const wakingTimer = setTimeout(() => setWaking(true), 3000);

    const checkAuth = async () => {
      try {
        const response = await fetch(`${API_URL}/profile`, {
          credentials: 'include',
          signal: controller.signal,
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
        clearTimeout(timeout);
        clearTimeout(wakingTimer);
        setWaking(false);
        setLoading(false);
      }
    };
    checkAuth();

    return () => {
      clearTimeout(timeout);
      clearTimeout(wakingTimer);
    };
  }, []);

  const handleAuthenticated = useCallback(async () => {
    try {
      const response = await fetch(`${API_URL}/profile`, { credentials: 'include' });
      if (response.ok) {
        const data = await response.json();
        setProfile(data);
        setIsAuthenticated(true);
      }
    } catch (error) {
      console.error('Failed to load profile:', error);
    }
  }, []);

  const handleSignOut = () => {
    setIsAuthenticated(false);
    setProfile(null);
  };

  const handleAppointmentBooked = useCallback(() => {
    setAppointmentVersion((v) => v + 1);
  }, []);

  if (loading) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', marginTop: '20%', fontSize: '1.2rem' }}>
        Loading...
        {waking && (
          <p style={{ fontSize: '0.9rem', color: 'var(--text-secondary)', maxWidth: 360, textAlign: 'center' }}>
            Waking up the server — this runs on free hosting that falls asleep
            after inactivity, so first load can take about a minute.
          </p>
        )}
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Login onAuthenticated={handleAuthenticated} />;
  }

  const isAdmin = profile?.isAdmin === true;

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
