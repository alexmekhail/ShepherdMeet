import { NavLink } from 'react-router-dom';
import { API_URL } from './schedule';
import { useState } from 'react';

export default function Sidebar({ profile, onSignOut }) {
  const [error, setError] = useState(false);
  async function signOut() {
    try {
      const response = await fetch(API_URL + '/logout', { method: 'POST', credentials: 'include' });
      if (!response.ok) throw new Error();
      onSignOut();
    } catch {
      setError(true);
    }
  }
  return (
    <aside className="workspace-sidebar">
      <NavLink className="brand" to="/">
        <span className="brand-mark" aria-hidden="true" />
        ShepherdMeet
      </NavLink>
      <nav aria-label="Main navigation">
        <NavLink to="/" end>Appointments</NavLink>
        <NavLink to={profile.isAdmin ? '/availability' : '/book'}>
          {profile.isAdmin ? 'Manage availability' : 'Book a meeting'}
        </NavLink>
      </nav>
      <div className="sidebar-account">
        <strong>{profile.firstName} {profile.lastName}</strong>
        <span>{profile.isAdmin ? 'Schedule administrator' : 'Parishioner'}</span>
        <button type="button" className="secondary" onClick={signOut}>Sign out</button>
        {error && <p role="alert">Could not sign out. Try again.</p>}
      </div>
    </aside>
  );
}
