import React, { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import './AppointmentConfirmed.css';

const REDIRECT_SECONDS = 4;

const AppointmentConfirmed = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { name, date, time } = location.state || {};
  const [countdown, setCountdown] = useState(REDIRECT_SECONDS);

  useEffect(() => {
    if (countdown <= 0) {
      navigate('/', { replace: true });
      return;
    }
    const timer = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [countdown, navigate]);

  return (
    <div className="confirmed-container">
      <div className="confirmed-card">
        <div className="confirmed-icon">✓</div>
        <h2 className="confirmed-title">Appointment Confirmed!</h2>
        <p className="confirmed-subtitle">
          Your meeting with Father Danial has been scheduled.
        </p>

        <div className="confirmed-details">
          {name && (
            <div className="confirmed-detail-row">
              <span className="detail-label">Name</span>
              <span className="detail-value">{name}</span>
            </div>
          )}
          {date && (
            <div className="confirmed-detail-row">
              <span className="detail-label">Date</span>
              <span className="detail-value">{date}</span>
            </div>
          )}
          {time && (
            <div className="confirmed-detail-row">
              <span className="detail-label">Time</span>
              <span className="detail-value">{time}</span>
            </div>
          )}
          <div className="confirmed-detail-row">
            <span className="detail-label">Location</span>
            <span className="detail-value">St. Philopater &amp; St. Demiana Coptic Orthodox Church</span>
          </div>
        </div>

        <button className="back-btn" onClick={() => navigate('/', { replace: true })}>
          Back to Calendar
        </button>
        <p className="confirmed-redirect">
          Redirecting in {countdown} second{countdown !== 1 ? 's' : ''}…
        </p>
      </div>
    </div>
  );
};

export default AppointmentConfirmed;
