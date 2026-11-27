import { useEffect, useState } from 'react';
import { api, churchToday, dateLabel, slotLabel } from './schedule';

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export default function PriestAvailabilityForm({ profile }) {
  const [date, setDate] = useState(() => churchToday(profile.timeZone));
  const [mode, setMode] = useState('open');
  const [repeat, setRepeat] = useState(false);
  const [slots, setSlots] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [pending, setPending] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    api('/priestavailabilities', { signal: controller.signal }).then(setSlots).catch((err) => {
      if (err.name !== 'AbortError') setError(err.message);
    });
    return () => controller.abort();
  }, []);

  async function save(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const days = repeat ? form.getAll('days') : [WEEKDAYS[new Date(date + 'T12:00:00Z').getUTCDay()]];
    setError('');
    setMessage('');
    if (!days.length) {
      setError('Choose at least one weekday.');
      return;
    }
    setPending(true);
    try {
      const endTime = form.get('endTime');
      await api('/priestavailabilities', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          startDate: date, endDate: repeat ? form.get('endDate') : date, days,
          startTime: mode === 'day-off' ? '00:00:00' : form.get('startTime') + ':00',
          endTime: mode === 'day-off' || endTime === '00:00' ? '1.00:00:00' : endTime + ':00',
          isAvailable: mode === 'open',
        }),
      });
      setSlots(await api('/priestavailabilities'));
      setMessage('Schedule updated.');
    } catch (err) {
      setError(err.message);
    } finally {
      setPending(false);
    }
  }

  const daySlots = (slots || []).filter((slot) => slot.startDate === date);
  return <>
    <header className="page-heading"><div><h1>Manage availability</h1><p>Set appointment hours and days off for Father Danial.</p></div></header>
    <div className="booking-layout">
      <section className="panel">
        <h2>Update the schedule</h2>
        <p className="muted">Times use {profile.timeZone}.</p>
        <form className="schedule-form" onSubmit={save}>
          <label>Change
            <select value={mode} onChange={(event) => setMode(event.target.value)}>
              <option value="open">Open appointment hours</option>
              <option value="block">Block an interval</option>
              <option value="day-off">Block an entire day</option>
            </select>
          </label>
          <label>{repeat ? 'First date' : 'Date'}<input type="date" value={date} required onChange={(event) => setDate(event.target.value)} /></label>
          <label className="check"><input type="checkbox" checked={repeat} onChange={(event) => setRepeat(event.target.checked)} /> Repeat until a date</label>
          {repeat && <>
            <label>Last date<input type="date" name="endDate" min={date} required /></label>
            <fieldset><legend>Weekdays</legend><div className="weekday-options">
              {WEEKDAYS.map((day) => <label key={day} className="check"><input type="checkbox" name="days" value={day} />{day}</label>)}
            </div></fieldset>
          </>}
          {mode !== 'day-off' && <div className="time-fields">
            <label>Start time<input type="time" name="startTime" step="1800" required /></label>
            <label>End time<input type="time" name="endTime" step="1800" required /></label>
          </div>}
          {error && <p role="alert" className="error">{error}</p>}
          {message && <p role="status">{message}</p>}
          <button className="button" disabled={pending}>{pending ? 'Saving…' : 'Save schedule'}</button>
        </form>
      </section>
      <section className="panel">
        <h2>{date ? dateLabel(date) : 'Daily schedule'}</h2>
        {!slots && !error && <p role="status">Loading schedule…</p>}
        {slots && !daySlots.length && <p className="muted">No intervals are listed for this date.</p>}
        {!!daySlots.length && <ul className="appointment-list">{daySlots.map((slot) => <li key={slot.id}>
          <span>{slotLabel(slot)}</span>
          <span className="status">{slot.isBooked ? 'Booked' : slot.isAvailable ? 'Open' : 'Unavailable'}</span>
        </li>)}</ul>}
      </section>
    </div>
  </>;
}
