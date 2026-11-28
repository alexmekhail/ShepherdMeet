import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, churchToday, dateLabel } from './schedule';

export default function Dashboard({ profile }) {
  const [appointments, setAppointments] = useState(null);
  const [error, setError] = useState('');
  const [pending, setPending] = useState(null);
  const [cancelId, setCancelId] = useState(null);
  const today = churchToday(profile.timeZone);
  const upcoming = (appointments || []).filter((item) => item.startUtc
    ? Date.parse(item.startUtc) >= Date.now() : item.date >= today);
  const past = (appointments || []).filter((item) => !upcoming.includes(item)).reverse();
  const path = profile.isAdmin ? '/availability' : '/book';

  function load(signal) {
    setError('');
    api('/appointments', { signal }).then(setAppointments).catch((err) => {
      if (err.name !== 'AbortError') setError(err.message);
    });
  }

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, []);

  async function cancel(id) {
    setPending(id);
    setError('');
    try {
      await api('/appointments/' + id, { method: 'DELETE' });
      setAppointments((items) => items.filter((item) => item.id !== id));
      setCancelId(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setPending(null);
    }
  }

  function list(items, canCancel) {
    return <ul className="appointment-list">{items.map((item) => (
      <li key={item.id}>
        <div><strong>{dateLabel(item.date)}</strong><div className="muted">{item.time} · {item.location}</div></div>
        {canCancel && <div className="appointment-actions">
          <span className="status">Confirmed</span>
          {cancelId === item.id ? <>
            <button type="button" className="secondary" disabled={pending !== null} onClick={() => cancel(item.id)}>Confirm cancellation</button>
            <button type="button" className="secondary" disabled={pending !== null} onClick={() => setCancelId(null)}>Keep appointment</button>
          </> : <button type="button" className="secondary" disabled={pending !== null} onClick={() => setCancelId(item.id)}>Cancel appointment</button>}
        </div>}
      </li>
    ))}</ul>;
  }

  return <>
    <header className="page-heading">
      <div><h1>Appointments</h1><p>{profile.isAdmin ? 'View your appointments and manage the church schedule.' : 'View and manage your meetings with Father Danial.'}</p></div>
      <Link className="button" to={path}>{profile.isAdmin ? 'Manage availability' : 'Book a meeting'}</Link>
    </header>
    {error && <p role="alert" className="error">{error} <button className="secondary" onClick={() => load()}>Reload appointments</button></p>}
    {!appointments && !error && <p role="status">Loading appointments…</p>}
    {appointments && <>
      <section className="panel">
        <h2>Upcoming appointments</h2>
        {upcoming.length ? list(upcoming, true) : <p className="muted">You have no upcoming appointments.</p>}
      </section>
      <section className="panel">
        <h2>Appointment history</h2>
        {past.length ? list(past, false) : <p className="muted">Past appointments will appear here.</p>}
      </section>
    </>}
  </>;
}
