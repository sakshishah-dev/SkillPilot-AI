declare global {
  interface Window { SKILLPILOT_API?: string }
}
export const API_BASE: string = (typeof window !== 'undefined' && window.SKILLPILOT_API) || '/api';
