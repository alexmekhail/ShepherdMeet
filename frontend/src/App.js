import React, { useState, useEffect, useCallback } from 'react';
import { Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import PriestAvailabilityForm from './PriestAvailabilityForm';
import UserForm from './UserForm';
import ConfirmationPage from './ConfirmationPage';
import AppointmentConfirmed from './AppointmentConfirmed';
import Login from './login';
import Sidebar from './Sidebar';
import Dashboard from './Dashboard';
import './theme.css';

import { API_URL, api } from './schedule';

const App = () => {
  const navigate = useNavigate();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  // Render's free tier spins the API down after 15 minutes idle; the first
  // request can take ~a minute to wake it back up. Show a notice once the
  // initial auth check has been waiting a while, so it doesn't look stuck.
  const [waking, setWaking] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    // Don't let a slow/unreachable backend hang the app on "Loading…" forever —
    // fall through to the login screen after 70s (enough for a Render cold start).
    let disposed = false;
    const timeout = setTimeout(() => controller.abort(), 70000);
    const wakingTimer = setTimeout(() => setWaking(true), 3000);

    const checkAuth = async () => {
      try {
        const response = await fetch(`${API_URL}/profile`, {
          credentials: 'include',
          signal: controller.signal,
        });
        if (disposed) return;
        if (response.ok) {
          const data = await response.json();
          if (disposed) return;
          setProfile(data);
          setIsAuthenticated(true);
        } else {
          setIsAuthenticated(false);
        }
      } catch {
        if (!disposed) setIsAuthenticated(false);
      } finally {
        clearTimeout(timeout);
        clearTimeout(wakingTimer);
        if (!disposed) {
          setWaking(false);
          setLoading(false);
        }
      }
    };
    checkAuth();

    return () => {
      disposed = true;
      controller.abort();
      clearTimeout(timeout);
      clearTimeout(wakingTimer);
    };
  }, []);

  const handleAuthenticated = useCallback(async () => {
    setProfile(await api('/profile'));
    setIsAuthenticated(true);
  }, []);

  const handleSignOut = () => {
    setIsAuthenticated(false);
    setProfile(null);
    navigate('/', { replace: true });
  };

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
    <div className="workspace">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <Sidebar profile={profile} onSignOut={handleSignOut} />
      <main id="main-content" className="workspace-main">
        <Routes>
          <Route path="/" element={<Dashboard profile={profile} />} />
          <Route path="/availability" element={isAdmin ? <PriestAvailabilityForm profile={profile} /> : <Navigate to="/" replace />} />
          <Route path="/book" element={isAdmin ? <Navigate to="/" replace /> : <UserForm profile={profile} />} />
          <Route path="/confirmation" element={isAdmin ? <Navigate to="/" replace /> : <ConfirmationPage profile={profile} />} />
          <Route path="/confirmed" element={<AppointmentConfirmed />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
};

export default App;
