import * as SecureStore from 'expo-secure-store';
import { ApiError, getCurrentUser, refreshAuthSession, type AuthResponse, type CurrentUser } from './api';

const sessionKey = 'petim.session';

export type StoredAuthSession = Pick<AuthResponse, 'userId' | 'username' | 'token' | 'expiresAt' | 'refreshToken' | 'refreshExpiresAt'>;

export async function saveSession(session: AuthResponse | StoredAuthSession) {
  await SecureStore.setItemAsync(sessionKey, JSON.stringify({
    userId: session.userId, username: session.username, token: session.token, expiresAt: session.expiresAt,
    refreshToken: session.refreshToken, refreshExpiresAt: session.refreshExpiresAt,
  } satisfies StoredAuthSession));
}

export async function clearSession() { await SecureStore.deleteItemAsync(sessionKey); }

export async function updateStoredUsername(username: string) {
  const stored = await SecureStore.getItemAsync(sessionKey);
  if (!stored) return;
  try {
    const session = JSON.parse(stored) as StoredAuthSession;
    await saveSession({ ...session, username });
  } catch {
    await clearSession();
  }
}

export async function restoreSession(): Promise<{ session: StoredAuthSession; user: CurrentUser } | null> {
  const stored = await SecureStore.getItemAsync(sessionKey);
  if (!stored) return null;
  let session: StoredAuthSession;
  try {
    session = JSON.parse(stored) as StoredAuthSession;
    if (!session.token || !session.refreshToken || !session.expiresAt || !session.refreshExpiresAt) throw new Error('invalid');
    if (new Date(session.refreshExpiresAt).getTime() <= Date.now()) throw new Error('expired');
  } catch {
    await clearSession();
    return null;
  }

  try {
    const user = await getCurrentUser(session.token);
    return { session, user };
  } catch (reason) {
    if (!(reason instanceof ApiError) || reason.status !== 401) return null;
  }

  try {
    const refreshed = await refreshAuthSession(session.refreshToken);
    await saveSession(refreshed);
    const user = await getCurrentUser(refreshed.token);
    return { session: refreshed, user };
  } catch (reason) {
    if (reason instanceof ApiError && reason.status === 401) await clearSession();
    return null;
  }
}
