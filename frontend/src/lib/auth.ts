const TOKEN_KEY = "stockradar_token";
const USER_KEY = "stockradar_user";

export interface AuthUser {
  userId: string;
  email: string;
  displayName: string;
  token: string;
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function getUser(): Omit<AuthUser, "token"> | null {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Omit<AuthUser, "token">;
  } catch {
    return null;
  }
}

export function saveAuth(user: AuthUser) {
  localStorage.setItem(TOKEN_KEY, user.token);
  localStorage.setItem(
    USER_KEY,
    JSON.stringify({ userId: user.userId, email: user.email, displayName: user.displayName }),
  );
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

export function isLoggedIn() {
  return Boolean(getToken());
}
