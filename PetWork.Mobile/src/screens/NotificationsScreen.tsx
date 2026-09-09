import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import {
  getMobileNotifications, markAllMobileNotificationsRead, markMobileNotificationRead,
  type MobileNotification,
} from '../api';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

type Props = {
  token: string;
  onBack: () => void;
  onUnreadChanged: (count: number) => void;
  onOpenLost: (id?: number) => void;
  onOpenAdoption: (id?: number) => void;
  onOpenSocial: (id?: number) => void;
  onOpenMatch: (petId?: number) => void;
  onOpenQuestion: (id?: number) => void;
};

export function NotificationsScreen({ token, onBack, onUnreadChanged, onOpenLost, onOpenAdoption, onOpenSocial, onOpenMatch, onOpenQuestion }: Props) {
  const [items, setItems] = useState<MobileNotification[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (refresh = false) => {
    refresh ? setRefreshing(true) : setLoading(true);
    setError(null);
    try {
      const response = await getMobileNotifications(token);
      setItems(response.items);
      onUnreadChanged(response.unreadCount);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Bildirimler yüklenemedi.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [onUnreadChanged, token]);

  useEffect(() => { void load(); }, [load]);

  const openItem = async (item: MobileNotification) => {
    if (!item.isRead) {
      await markMobileNotificationRead(token, item.id).catch(() => undefined);
      setItems(current => current.map(value => value.id === item.id ? { ...value, isRead: true } : value));
      onUnreadChanged(Math.max(0, items.filter(value => !value.isRead).length - 1));
    }
    if (item.entityType === 'lost_pet') onOpenLost(item.entityId ?? undefined);
    else if (item.entityType === 'adoption') onOpenAdoption(item.entityId ?? undefined);
    else if (item.entityType === 'social_post') onOpenSocial(item.entityId ?? undefined);
    else if (item.entityType === 'pati_match') onOpenMatch(item.entityId ?? undefined);
    else if (item.entityType === 'question') onOpenQuestion(item.entityId ?? undefined);
  };

  const readAll = async () => {
    await markAllMobileNotificationsRead(token);
    setItems(current => current.map(item => ({ ...item, isRead: true })));
    onUnreadChanged(0);
  };

  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} tintColor={colors.primary} />}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <View style={styles.header}>
      <Pressable onPress={onBack} accessibilityLabel="Geri dön" style={styles.back}><Ionicons name="arrow-back" size={23} color={colors.primary} /></Pressable>
      <View style={styles.flex}><Text style={styles.title}>Bildirimler</Text><Text style={styles.subtitle}>Topluluk, sahiplendirme, kayıp pati ve PatiMatch</Text></View>
      {items.some(item => !item.isRead) ? <Pressable onPress={() => void readAll()}><Text style={styles.readAll}>Tümünü oku</Text></Pressable> : <View style={styles.spacer} />}
    </View>
    {loading && !items.length ? <ActivityIndicator color={colors.primary} size="large" style={styles.loader} /> : null}
    {error ? <View style={styles.error}><Text style={styles.errorText}>{error}</Text><Pressable onPress={() => void load()}><Text style={styles.retry}>Yenile</Text></Pressable></View> : null}
    {!loading && !error && !items.length ? <View style={styles.empty}><Ionicons name="notifications-outline" size={38} color="#AA9DA4" /><Text style={styles.emptyTitle}>Henüz bildirimin yok</Text><Text style={styles.emptyText}>Yeni yorum, eşleşme, başvuru veya görülme bildirimi geldiğinde burada görünecek.</Text></View> : null}
    {items.map(item => <Pressable key={item.id} onPress={() => void openItem(item)} style={({ pressed }) => [styles.card, !item.isRead && styles.unread, pressed && styles.pressed]}>
      <View style={[styles.icon, item.type === 'lost_sighting' ? styles.lostIcon : styles.adoptionIcon]}>
        <Ionicons name={item.type === 'lost_sighting' ? 'location-outline' : item.type === 'adoption_status' ? 'checkmark-circle-outline' : item.type.startsWith('social_') ? 'chatbubble-outline' : 'heart-outline'} size={22} color={item.type === 'lost_sighting' ? '#9B463B' : colors.primary} />
      </View>
      <View style={styles.flex}><View style={styles.cardTop}><Text style={styles.cardTitle}>{item.title}</Text>{!item.isRead ? <View style={styles.dot} /> : null}</View><Text style={styles.body}>{item.body}</Text><Text style={styles.date}>{new Date(item.createdAt).toLocaleString('tr-TR')}</Text></View>
      <Ionicons name="chevron-forward" size={18} color="#AA9DA4" />
    </Pressable>)}
  </ScrollView>;
}

const styles = createThemedStyles(() => ({
  screen: { flex: 1, backgroundColor: colors.background }, content: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingTop: 58, paddingHorizontal: 20, paddingBottom: 120 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 13, marginBottom: 24 }, back: { width: 46, height: 46, borderRadius: 23, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, flex: { flex: 1 }, spacer: { width: 68 },
  title: { color: colors.text, fontFamily: 'Georgia', fontSize: 28, fontWeight: '800' }, subtitle: { color: colors.muted, fontSize: 10, marginTop: 3 }, readAll: { color: colors.primary, fontSize: 11, fontWeight: '900', paddingVertical: 10 }, loader: { marginTop: 60 },
  card: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 15, marginBottom: 11, borderRadius: 20, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, ...shadow }, unread: { borderColor: '#CDB5D7', backgroundColor: '#FFFBFF' }, pressed: { opacity: 0.72 },
  icon: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center' }, lostIcon: { backgroundColor: colors.peachSoft }, adoptionIcon: { backgroundColor: colors.lilacSoft }, cardTop: { flexDirection: 'row', alignItems: 'center', gap: 7 }, cardTitle: { flex: 1, color: colors.text, fontSize: 13, fontWeight: '900' }, dot: { width: 8, height: 8, borderRadius: 4, backgroundColor: colors.primary }, body: { color: colors.muted, fontSize: 11, lineHeight: 16, marginTop: 4 }, date: { color: '#9A8E95', fontSize: 9, marginTop: 7 },
  empty: { alignItems: 'center', padding: 28, marginTop: 24, borderRadius: 22, backgroundColor: colors.card }, emptyTitle: { color: colors.text, fontWeight: '900', marginTop: 10 }, emptyText: { color: colors.muted, fontSize: 11, lineHeight: 17, textAlign: 'center', marginTop: 6 },
  error: { padding: 18, borderRadius: 18, backgroundColor: colors.peachSoft }, errorText: { color: '#91443B' }, retry: { color: colors.primary, fontWeight: '900', marginTop: 10 },
}));
