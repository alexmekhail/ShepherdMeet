import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, CHURCH, churchToday, dateLabel, slotLabel } from './schedule';

export default function UserForm({ profile }) {
  const [month, setMonth] = useState(() => new Date(churchToday(profile.timeZone) + 'T12:00:00Z'));
  const [day, setDay] = useState('');
  const [selected, setSelected] = useState(null);
  const [slots, setSlots] = useState(null);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const first = new Date(Date.UTC(month.getUTCFullYear(), month.getUTCMonth(), 1));
  const days = new Date(Date.UTC(month.getUTCFullYear(), month.getUTCMonth() + 1, 0)).getUTCDate();
  const monthKey = first.toISOString().slice(0, 7);

  function load(signal) {
    setError('');
    api('/priestavailabilities', { signal }).then(setSlots).catch((err) => {
      if (err.name !== 'AbortError') setError(err.message);
    });
  }

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, []);

  function changeMonth(offset) {
    setMonth(new Date(Date.UTC(month.getUTCFullYear(), month.getUTCMonth() + offset, 1)));
    setDay('');
    setSelected(null);
  }

  const daySlots = (slots || []).filter((slot) => slot.startDate === day);

  return <>
    <header className="page-heading"><div><h1>Book a meeting</h1><p>30 minutes with Father Danial</p></div></header>
    <div className="booking-layout panel">
      <section className="booking-calendar" aria-label="Choose a date">
        <div className="calendar-toolbar">
          <h2 aria-live="polite">{dateLabel(monthKey + '-01', { month: 'long', year: 'numeric' })}</h2>
          <div>
            <button className="month-button" aria-label="Previous month" onClick={() => changeMonth(-1)}><span aria-hidden="true" /></button>
            <button className="month-button next" aria-label="Next month" onClick={() => changeMonth(1)}><span aria-hidden="true" /></button>
          </div>
        </div>
        {error && <p className="error" role="alert">{error} <button className="secondary" onClick={() => load()}>Retry</button></p>}
        {!slots && !error && <p role="status">Loading availability…</p>}
        {slots && <div className="calendar">
          {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((label) => <span key={label}>{label}</span>)}
          {Array.from({ length: first.getUTCDay() }, (_, i) => <span key={'blank-' + i} />)}
          {Array.from({ length: days }, (_, i) => {
            const date = monthKey + '-' + String(i + 1).padStart(2, '0');
            const available = slots.some((slot) => slot.startDate === date && slot.isAvailable && !slot.isBooked);
            return <button key={date} className={available ? 'day available' : 'day'} aria-pressed={day === date}
              aria-label={dateLabel(date) + (available ? ', times available' : ', no open times')}
              onClick={() => { setDay(date); setSelected(null); }}>{i + 1}</button>;
          })}
        </div>}
        <p className="booking-location muted">{CHURCH}</p>
      </section>
      <section className="booking-times" aria-labelledby="booking-times-heading">
        <h2 id="booking-times-heading">Appointment times</h2>
        {!day && <p className="muted">Choose a date on the calendar.</p>}
        {day && <p><strong>{dateLabel(day)}</strong></p>}
        {day && !daySlots.length && <p className="muted">No times are listed for this date.</p>}
        <div className="time-options">{daySlots.map((slot) => (
          <button className={slot.isBooked ? 'secondary booked-time' : 'secondary'} key={slot.id}
            disabled={!slot.isAvailable || slot.isBooked}
            aria-pressed={selected?.id === slot.id} onClick={() => setSelected(slot)}>
            {slotLabel(slot)}{slot.isBooked ? ' · Booked' : !slot.isAvailable ? ' · Unavailable' : ''}
          </button>
        ))}</div>
        <button className="button review-button" disabled={!selected} onClick={() => navigate('/confirmation', { state: selected })}>Review appointment</button>
      </section>
    </div>
  </>;
}
