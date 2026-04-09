import React, { useState, useEffect } from 'react';
import './Sidebar.css';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209';

const formatDate = (dateStr) => {
  if (!dateStr) return '';
  const [year, month, day] = dateStr.split('-').map(Number);
  return new Date(year, month - 1, day).toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
};

const Sidebar = ({ profile, onSignOut, appointmentVersion = 0 }) => {
  const [appointments, setAppointments] = useState([]);
  const [loadingAppts, setLoadingAppts] = useState(true);
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    const fetchAppointments = async () => {
      setLoadingAppts(true);
      try {
        const res = await fetch(`${API_URL}/appointments`, { credentials: 'include' });
        if (res.ok) {
          const data = await res.json();
          setAppointments(data);
        }
      } catch (e) {
        console.error('Failed to load appointments:', e);
      } finally {
        setLoadingAppts(false);
      }
    };
    fetchAppointments();
  }, [appointmentVersion]); // re-fetch whenever a new appointment is booked

  const todayStr = new Date().toISOString().split('T')[0];
  const upcoming = appointments.filter((a) => a.date >= todayStr);
  const previous = appointments.filter((a) => a.date < todayStr).reverse();

  const initials =
    `${profile?.firstName?.[0] ?? ''}${profile?.lastName?.[0] ?? ''}`.toUpperCase() || '?';

  const handleSignOut = async () => {
    try {
      await fetch(`${API_URL}/logout`, { method: 'POST', credentials: 'include' });
    } catch (e) {
      console.error(e);
    }
    onSignOut();
  };

  return (
    <>
      <button
        className={`sidebar-toggle ${isOpen ? 'open' : ''}`}
        onClick={() => setIsOpen(!isOpen)}
        aria-label="Toggle menu"
      >
        <span /><span /><span />
      </button>

      {isOpen && (
        <div className="sidebar-overlay" onClick={() => setIsOpen(false)} />
      )}

      <div className={`sidebar ${isOpen ? 'open' : ''}`}>
        {/* User header */}
        <div className="sidebar-header">
          <div className="sidebar-avatar">{initials}</div>
          <div className="sidebar-user-info">
            <span className="sidebar-name">
              {profile?.firstName} {profile?.lastName}
            </span>
            <span className="sidebar-email">{profile?.email}</span>
          </div>
        </div>

        <div className="sidebar-divider" />

        {/* Upcoming appointments */}
        <div className="sidebar-section">
          <h4 className="sidebar-section-title">Upcoming</h4>
          {loadingAppts ? (
            <p className="sidebar-empty">Loading…</p>
          ) : upcoming.length === 0 ? (
            <p className="sidebar-empty">No upcoming appointments</p>
          ) : (
            <ul className="sidebar-appt-list">
              {upcoming.map((a) => (
                <li key={a.id} className="sidebar-appt-item upcoming">
                  <span className="appt-date">{formatDate(a.date)}</span>
                  <span className="appt-time">{a.time}</span>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="sidebar-divider" />

        {/* Previous appointments */}
        <div className="sidebar-section">
          <h4 className="sidebar-section-title">Previous</h4>
          {loadingAppts ? (
            <p className="sidebar-empty">Loading…</p>
          ) : previous.length === 0 ? (
            <p className="sidebar-empty">No previous appointments</p>
          ) : (
            <ul className="sidebar-appt-list">
              {previous.map((a) => (
                <li key={a.id} className="sidebar-appt-item past">
                  <span className="appt-date">{formatDate(a.date)}</span>
                  <span className="appt-time">{a.time}</span>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Footer */}
        <div className="sidebar-footer">
          <button className="signout-btn" onClick={handleSignOut}>
            Sign Out / Switch Account
          </button>
        </div>
      </div>
    </>
  );
};

export default Sidebar;
