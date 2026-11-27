import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { api, CHURCH, dateLabel, slotLabel } from './schedule';

export default function ConfirmationPage({ profile }) {
  const slot = useLocation().state;
  const navigate = useNavigate();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  if (!slot?.id) return <Navigate to="/book" replace />;

  async function confirm() {
    setPending(true);
    setError('');
    try {
      const appointment = await api('/appointments', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ availabilityId: slot.id }),
      });
      navigate('/confirmed', { state: appointment, replace: true });
    } catch (err) {
      setError(err.message);
    } finally {
      setPending(false);
    }
  }

  return <>
    <header className="page-heading"><div><h1>Review appointment</h1><p>Check the details before confirming.</p></div></header>
    <section className="panel details-panel">
      <h2>Meeting with Father Danial</h2>
      <p className="muted">{CHURCH}</p>
      <p>{dateLabel(slot.startDate)} · {slotLabel(slot)}</p>
      <p className="muted">{profile.timeZone}</p>
      {error && <p role="alert" className="error">{error}</p>}
      <div className="actions">
        <button className="button" onClick={confirm} disabled={pending}>{pending ? 'Confirming…' : 'Confirm appointment'}</button>
        {!pending && <Link className="secondary" to="/book">Back to calendar</Link>}
      </div>
    </section>
  </>;
}
