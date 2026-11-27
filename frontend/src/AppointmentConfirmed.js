import { Link, Navigate, useLocation } from 'react-router-dom';
import { dateLabel } from './schedule';

export default function AppointmentConfirmed() {
  const appointment = useLocation().state;
  if (!appointment?.id) return <Navigate to="/" replace />;
  return <>
    <header className="page-heading"><div><h1>Appointment confirmed</h1><p>Your meeting with Father Danial is scheduled.</p></div></header>
    <section className="panel details-panel">
      <h2>{dateLabel(appointment.date)}</h2>
      <p>{appointment.time}</p>
      <p className="muted">{appointment.location}</p>
      <Link className="button" to="/">View appointments</Link>
    </section>
  </>;
}
