/** Dev: ket noi thang API (tranh loi negotiate qua Vite proxy). Prod: cung origin. */
export function getHubUrl(): string {
  if (import.meta.env.VITE_HUB_URL) {
    return import.meta.env.VITE_HUB_URL;
  }
  if (import.meta.env.DEV) {
    return "http://localhost:5280/hubs/market";
  }
  return "/hubs/market";
}
