/**
 * Formats a number of bytes into a human-readable string (e.g. 1.25 MB).
 * @param {number} bytes The number of bytes
 * @param {number} decimals The number of decimal places (default 2)
 * @returns {string} The formatted string
 */
export function formatBytes(bytes, decimals = 2) {
  if (bytes === 0) return '0 B';
  if (!bytes || isNaN(bytes)) return '0 B';

  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

export function formatDateTime(dateInput) {
  if (!dateInput) return '';
  const date = new Date(dateInput);
  return isNaN(date.getTime()) ? dateInput.toString() : date.toLocaleString();
}

