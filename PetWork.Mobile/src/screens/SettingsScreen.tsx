import { Ionicons } from '@expo/vector-icons';
import * as SecureStore from 'expo-secure-store';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';
import { colors, shadow } from '../theme';

type Settings = {
  communityNotifications: boolean;
  lostPetNotifications: boolean;
  matchNotifications: boolean;
  nearbyVisibility: boolean;
  approximateLocationOnly: boolean;
};

type SettingsScreenProps = {
  username: string | null;
  onOpenAccount: () => void;
  onLogin: () => void;
  onLogout: () => void;
};

const STORAGE_KEY = 'petim.settings';
const defaultSettings: Settings = {
  communityNotifications: true,
  lostPetNotifications: true,
  matchNotifications: true,
  nearbyVisibility: true,
  approximateLocationOnly: true,
};

export function SettingsScreen({ username, onOpenAccount, onLogin, onLogout }: SettingsScreenProps) {
  const [settings, setSettings] = useState(defaultSettings);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    SecureStore.getItemAsync(STORAGE_KEY)
      .then(value => {
        if (!active || !value) return;
        const stored = JSON.parse(value) as Partial<Settings>;
        setSettings({ ...defaultSettings, ...stored });
      })
      .catch(() => undefined)
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, []);

  const updateSetting = (key: keyof Settings, value: boolean) => {
    const next = { ...settings, [key]: value };
    setSettings(next);
    SecureStore.setItemAsync(STORAGE_KEY, JSON.stringify(next)).catch(() => {
      setSettings(settings);
      Alert.alert('Ayar kaydedilemedi', 'Lütfen tekrar dene.');
    });
  };

  const confirmLogout = () => Alert.alert(
    'Çıkış yapmak istiyor musun?',
    'Bu cihazdaki oturumun kapatılacak. Hesabın ve içeriklerin silinmeyecek.',
    [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Çıkış Yap', style: 'destructive', onPress: onLogout },
    ],
  );

  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <StatusBar style="dark" />

    <View style={styles.header}>
      <View>
        <Text style={styles.eyebrow}>PET’İM</Text>
        <Text style={styles.title}>Ayarlar</Text>
        <Text style={styles.subtitle}>Deneyimini ve gizliliğini yönet.</Text>
      </View>
      <View style={styles.headerIcon}><Ionicons name="settings" size={27} color={colors.primary} /></View>
    </View>

    <Pressable
      onPress={username ? onOpenAccount : onLogin}
      accessibilityRole="button"
      style={({ pressed }) => [styles.profileCard, pressed && styles.pressed]}
    >
      <View style={styles.avatar}>
        {username
          ? <Text style={styles.avatarText}>{username.charAt(0).toLocaleUpperCase('tr-TR')}</Text>
          : <Ionicons name="person-outline" size={25} color={colors.white} />}
      </View>
      <View style={styles.flexOne}>
        <Text style={styles.profileTitle}>{username || 'Hesabına giriş yap'}</Text>
        <Text style={styles.profileText}>{username ? 'Profil ve hesap bilgilerini görüntüle' : 'Ayarlarını hesabınla birlikte kullan'}</Text>
      </View>
      <Ionicons name="chevron-forward" size={20} color={colors.primary} />
    </Pressable>

    {loading ? <View style={styles.loading}><ActivityIndicator color={colors.primary} /><Text style={styles.loadingText}>Tercihler yükleniyor…</Text></View> : <>
      <SectionTitle icon="notifications-outline" title="Bildirimler" />
      <View style={styles.card}>
        <SettingSwitch icon="chatbubbles-outline" title="Topluluk" subtitle="Yorum, yanıt ve etkileşimler" value={settings.communityNotifications} onChange={value => updateSetting('communityNotifications', value)} />
        <SettingSwitch icon="location-outline" title="Kayıp pati uyarıları" subtitle="Yakınındaki önemli ilanlar" value={settings.lostPetNotifications} onChange={value => updateSetting('lostPetNotifications', value)} />
        <SettingSwitch icon="heart-outline" title="PatiMatch" subtitle="Yeni eşleşme ve mesajlar" value={settings.matchNotifications} onChange={value => updateSetting('matchNotifications', value)} last />
      </View>

      <SectionTitle icon="shield-checkmark-outline" title="Gizlilik ve konum" />
      <View style={styles.card}>
        <SettingSwitch icon="paw-outline" title="Yakındaki patilerde görün" subtitle="Profilin keşif alanında gösterilsin" value={settings.nearbyVisibility} onChange={value => updateSetting('nearbyVisibility', value)} />
        <SettingSwitch icon="navigate-outline" title="Yalnızca yaklaşık konum" subtitle="Kesin adresin diğer kullanıcılara gösterilmez" value={settings.approximateLocationOnly} onChange={value => updateSetting('approximateLocationOnly', value)} last locked />
      </View>

      <View style={styles.safetyNote}>
        <Ionicons name="lock-closed" size={20} color="#4E7458" />
        <Text style={styles.safetyText}>Pet’im kesin ev adresini herkese açık biçimde paylaşmaz. Konum izni yalnızca ihtiyaç duyulan özellik açıldığında istenir.</Text>
      </View>

      <SectionTitle icon="information-circle-outline" title="Uygulama" />
      <View style={styles.card}>
        <InfoRow icon="help-circle-outline" title="Yardım ve geri bildirim" onPress={() => Alert.alert('Yardım merkezi', 'Destek kanalları yayımlandığında burada yer alacak.')} />
        <InfoRow icon="document-text-outline" title="Gizlilik ve kullanım koşulları" onPress={() => Alert.alert('Bilgilendirme', 'Güncel metinler yayımlandığında buradan açılacak.')} />
        <View style={[styles.row, styles.rowLast]}><View style={styles.rowIcon}><Ionicons name="phone-portrait-outline" size={20} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.rowTitle}>Pet’im mobil</Text><Text style={styles.rowSubtitle}>Sürüm 1.0.0</Text></View></View>
      </View>
    </>}

    {username ? <Pressable onPress={confirmLogout} accessibilityRole="button" style={({ pressed }) => [styles.logoutButton, pressed && styles.pressed]}>
      <Ionicons name="log-out-outline" size={20} color="#8E4036" />
      <Text style={styles.logoutText}>Çıkış Yap</Text>
    </Pressable> : null}
  </ScrollView>;
}

function SectionTitle({ icon, title }: { icon: keyof typeof Ionicons.glyphMap; title: string }) {
  return <View style={styles.sectionTitleRow}><Ionicons name={icon} size={18} color={colors.primary} /><Text style={styles.sectionTitle}>{title}</Text></View>;
}

function SettingSwitch({ icon, title, subtitle, value, onChange, last = false, locked = false }: {
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  subtitle: string;
  value: boolean;
  onChange: (value: boolean) => void;
  last?: boolean;
  locked?: boolean;
}) {
  return <View style={[styles.row, last && styles.rowLast]}>
    <View style={styles.rowIcon}><Ionicons name={icon} size={20} color={colors.primary} /></View>
    <View style={styles.flexOne}><Text style={styles.rowTitle}>{title}</Text><Text style={styles.rowSubtitle}>{subtitle}</Text></View>
    <Switch
      value={value}
      onValueChange={locked ? undefined : onChange}
      disabled={locked}
      trackColor={{ false: '#D9CFD3', true: colors.sage }}
      thumbColor={value ? colors.primary : colors.card}
      ios_backgroundColor="#D9CFD3"
      accessibilityLabel={title}
    />
  </View>;
}

function InfoRow({ icon, title, onPress }: { icon: keyof typeof Ionicons.glyphMap; title: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.row, pressed && styles.pressed]}>
    <View style={styles.rowIcon}><Ionicons name={icon} size={20} color={colors.primary} /></View>
    <Text style={[styles.rowTitle, styles.flexOne]}>{title}</Text>
    <Ionicons name="chevron-forward" size={18} color="#AA9DA4" />
  </Pressable>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingTop: Platform.OS === 'ios' ? 58 : 32, paddingHorizontal: 20, paddingBottom: 118 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 22 },
  eyebrow: { color: colors.peach, fontSize: 11, fontWeight: '900', letterSpacing: 2 },
  title: { color: colors.text, fontFamily: serif, fontSize: 34, fontWeight: '700', marginTop: 4 },
  subtitle: { color: colors.muted, fontSize: 12, marginTop: 4 },
  headerIcon: { width: 54, height: 54, borderRadius: 27, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft },
  profileCard: { minHeight: 86, flexDirection: 'row', alignItems: 'center', gap: 13, backgroundColor: colors.primary, borderRadius: 24, padding: 16, ...shadow },
  avatar: { width: 52, height: 52, borderRadius: 26, alignItems: 'center', justifyContent: 'center', backgroundColor: '#FFFFFF24', borderWidth: 1, borderColor: '#FFFFFF46' },
  avatarText: { color: colors.white, fontFamily: serif, fontSize: 23, fontWeight: '700' },
  profileTitle: { color: colors.white, fontSize: 15, fontWeight: '900' },
  profileText: { color: '#EADFE7', fontSize: 10, lineHeight: 15, marginTop: 4 },
  sectionTitleRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 25, marginBottom: 10, paddingHorizontal: 3 },
  sectionTitle: { color: colors.text, fontSize: 14, fontWeight: '900' },
  card: { backgroundColor: colors.card, borderRadius: 22, paddingHorizontal: 15, borderWidth: 1, borderColor: colors.border, ...shadow },
  row: { minHeight: 76, flexDirection: 'row', alignItems: 'center', gap: 11, borderBottomWidth: 1, borderBottomColor: colors.border },
  rowLast: { borderBottomWidth: 0 },
  rowIcon: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft },
  rowTitle: { color: colors.text, fontSize: 13, fontWeight: '800' },
  rowSubtitle: { color: colors.muted, fontSize: 9.5, lineHeight: 14, marginTop: 3, paddingRight: 6 },
  flexOne: { flex: 1 },
  safetyNote: { flexDirection: 'row', alignItems: 'flex-start', gap: 11, marginTop: 13, padding: 15, borderRadius: 18, backgroundColor: colors.sageSoft },
  safetyText: { flex: 1, color: '#526259', fontSize: 10, lineHeight: 15 },
  loading: { minHeight: 220, alignItems: 'center', justifyContent: 'center', gap: 10 },
  loadingText: { color: colors.muted, fontSize: 11 },
  logoutButton: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, marginTop: 22, borderRadius: 18, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach },
  logoutText: { color: '#8E4036', fontSize: 13, fontWeight: '900' },
  pressed: { opacity: 0.75, transform: [{ scale: 0.99 }] },
});
