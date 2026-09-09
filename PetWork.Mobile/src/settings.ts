import Constants, { AppOwnership } from 'expo-constants';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

export type NotificationPermission = {
  granted: boolean;
  canAskAgain: boolean;
  ios?: { status: number };
};

export const isExpoGo = Constants.appOwnership === AppOwnership.Expo;

export type PetimSettings = {
  communityNotifications: boolean;
  lostPetNotifications: boolean;
  matchNotifications: boolean;
  approximateLocationOnly: true;
};

export type NotificationSettingKey =
  | 'communityNotifications'
  | 'lostPetNotifications'
  | 'matchNotifications';

export const SETTINGS_STORAGE_KEY = 'petim.settings';

export const defaultSettings: PetimSettings = {
  communityNotifications: false,
  lostPetNotifications: false,
  matchNotifications: false,
  approximateLocationOnly: true,
};

export async function readSettings(): Promise<PetimSettings> {
  const value = await SecureStore.getItemAsync(SETTINGS_STORAGE_KEY);
  if (!value) return defaultSettings;

  try {
    const stored = JSON.parse(value) as Partial<PetimSettings>;
    return { ...defaultSettings, ...stored, approximateLocationOnly: true };
  } catch {
    await SecureStore.deleteItemAsync(SETTINGS_STORAGE_KEY);
    return defaultSettings;
  }
}

export async function writeSettings(settings: PetimSettings) {
  await SecureStore.setItemAsync(SETTINGS_STORAGE_KEY, JSON.stringify({
    ...settings,
    approximateLocationOnly: true,
  }));
}

export function notificationsAllowed(status: NotificationPermission) {
  return status.granted || status.ios?.status === 3 || status.ios?.status === 4;
}

export async function readNotificationPermission() {
  if (Platform.OS === 'web' || isExpoGo) return null;
  const Notifications = await import('expo-notifications');
  return Notifications.getPermissionsAsync();
}

export async function requestNotificationPermission() {
  if (Platform.OS === 'web' || isExpoGo) return null;
  const Notifications = await import('expo-notifications');
  return Notifications.requestPermissionsAsync({
    ios: { allowAlert: true, allowBadge: true, allowSound: true },
  });
}

export async function clearNotifications() {
  if (Platform.OS === 'web' || isExpoGo) return;
  const Notifications = await import('expo-notifications');
  await Promise.all([
    Notifications.setBadgeCountAsync(0),
    Notifications.dismissAllNotificationsAsync(),
  ]);
}

export function notificationPreferenceForType(
  settings: PetimSettings,
  type: unknown,
) {
  if (type === 'lost' || type === 'lost-pet') return settings.lostPetNotifications;
  if (type === 'match' || type === 'message') return settings.matchNotifications;
  return settings.communityNotifications;
}
