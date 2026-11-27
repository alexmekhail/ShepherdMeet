export const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209/api';
export const CHURCH = 'St. Philopater & St. Demiana Coptic Orthodox Church';
export const dateLabel = (date, options = { dateStyle: 'long' }) => {
  const value = new Date(date + 'T12:00:00Z');
  return Number.isNaN(value.getTime())
    ? date || 'Date unavailable'
    : new Intl.DateTimeFormat('en-US', { ...options, timeZone: 'UTC' }).format(value);
};
export const timeLabel = (time) => {
  const [hour, minute] = time.split(':').map(Number);
  return `${hour % 12 || 12}:${String(minute).padStart(2, '0')} ${hour < 12 ? 'AM' : 'PM'}`;
};
export const slotLabel = (slot) => `${timeLabel(slot.startTime)} – ${timeLabel(slot.endTime)}`;
export const churchToday = (zone, now = new Date()) => {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(now);
  const get = (type) => parts.find((part) => part.type === type).value;
  return `${get('year')}-${get('month')}-${get('day')}`;
};

export async function api(path, options) {
  const response = await fetch(API_URL + path, { ...options, credentials: 'include' });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.detail || (response.status === 409
      ? 'This time is no longer available. Pick another time from the calendar.'
      : 'The request failed. Please try again.'));
  }
  return response.status === 204 ? null : response.json();
}
