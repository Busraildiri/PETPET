import Constants, { AppOwnership } from 'expo-constants';
import { Platform } from 'react-native';
import { notificationPreferenceForType, readSettings } from './settings';
import { registerMobilePushToken } from './api';

let configured = false;

export async function configureNotifications() {
  if (configured || Platform.OS === 'web' || Constants.appOwnership === AppOwnership.Expo) return;
  configured = true;
  const Notifications = await import('expo-notifications');

  Notifications.setNotificationHandler({
    handleNotification: async notification => {
      const settings = await readSettings();
      const shouldShow = notificationPreferenceForType(
        settings,
        notification.request.content.data?.type,
      );

      return {
        shouldShowBanner: shouldShow,
        shouldShowList: shouldShow,
        shouldPlaySound: shouldShow,
        shouldSetBadge: shouldShow,
      };
    },
  });

  if (Platform.OS === 'android') {
    void Promise.all([
      Notifications.setNotificationChannelAsync('community', {
        name: 'Topluluk',
        description: 'Yorum, yanıt ve topluluk etkileşimleri',
        importance: Notifications.AndroidImportance.DEFAULT,
      }),
      Notifications.setNotificationChannelAsync('lost-pets', {
        name: 'Kayıp pati uyarıları',
        description: 'Yakındaki önemli kayıp ve bulunan pati ilanları',
        importance: Notifications.AndroidImportance.HIGH,
      }),
      Notifications.setNotificationChannelAsync('matches', {
        name: 'PatiMatch',
        description: 'Yeni eşleşme ve mesaj bildirimleri',
        importance: Notifications.AndroidImportance.DEFAULT,
      }),
    ]).catch(() => undefined);
  }
}

export async function registerForPushNotifications(authToken: string) {
  if (Platform.OS === 'web' || Constants.appOwnership === AppOwnership.Expo) return null;
  const settings = await readSettings();
  if (!settings.communityNotifications && !settings.lostPetNotifications && !settings.matchNotifications) return null;

  const projectId = Constants.easConfig?.projectId ??
    (Constants.expoConfig?.extra?.eas?.projectId as string | undefined);
  if (!projectId) return null;

  const Notifications = await import('expo-notifications');
  const permission = await Notifications.getPermissionsAsync();
  if (!permission.granted && permission.ios?.status !== Notifications.IosAuthorizationStatus.PROVISIONAL) return null;
  const pushToken = (await Notifications.getExpoPushTokenAsync({ projectId })).data;
  await registerMobilePushToken(authToken, pushToken, Platform.OS);
  return pushToken;
}
