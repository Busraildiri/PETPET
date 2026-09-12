import { Ionicons } from '@expo/vector-icons';
import * as Application from 'expo-application';
import * as ImagePicker from 'expo-image-picker';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, AppState, BackHandler, Image, Linking, Platform, Pressable, ScrollView, StyleSheet, Switch, Text, TextInput, View } from 'react-native';
import { apiUrl, createSupportReport, getMobileContactSettings, getMobileNotificationPreferences, getProfileVisibility, updateMobileContactSettings, updateMobileNotificationPreferences, updateProfileVisibility, type CreateSupportReportRequest, type MobileContactSettings, type ProfileVisibilitySettings } from '../api';
import {
  clearNotifications, defaultSettings, isExpoGo, NotificationPermission, NotificationSettingKey,
  notificationsAllowed, PetimSettings, readNotificationPermission, readSettings,
  requestNotificationPermission, writeSettings,
} from '../settings';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';
import { registerForPushNotifications } from '../notifications';

type Props = {
  darkTheme: boolean;
  onThemeChange: (enabled: boolean) => void;
  username: string | null;
  token: string | null;
  unreadNotifications: number;
  onOpenNotifications: () => void;
  onOpenAccount: () => void;
  onLogin: () => void;
  onLogout: () => void;
  onOpenQuestions: () => void;
  onOpenPatiMatch: () => void;
  initialPage?: Page;
  onInitialPageConsumed?: () => void;
};
type Page = 'main' | 'contact' | 'report' | 'help' | 'about' | 'appInfo' | 'legal';
type Permission = NotificationPermission | null;
const notificationKeys: NotificationSettingKey[] = ['communityNotifications', 'lostPetNotifications', 'matchNotifications'];

export function SettingsScreen({ darkTheme, onThemeChange, username, token, unreadNotifications, onOpenNotifications, onOpenAccount, onLogin, onLogout, onOpenQuestions, onOpenPatiMatch, initialPage = 'main', onInitialPageConsumed }: Props) {
  const [settings, setSettings] = useState<PetimSettings>(defaultSettings);
  const [permission, setPermission] = useState<Permission>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [profileVisibility, setProfileVisibility] = useState<ProfileVisibilitySettings>({ showBioToOthers: true, showPetsToOthers: true });
  const [profileVisibilitySaving, setProfileVisibilitySaving] = useState(false);
  const [page, setPage] = useState<Page>(initialPage);

  useEffect(() => { if (initialPage !== 'main') onInitialPageConsumed?.(); }, []);

  useEffect(() => {
    let active = true;
    Promise.all([
      readSettings(),
      readNotificationPermission().catch(() => null),
      token ? getMobileNotificationPreferences(token).catch(() => null) : Promise.resolve(null),
    ])
      .then(async ([stored, currentPermission, server]) => {
        if (!active) return;
        let next = server?.isConfigured ? {
          ...stored,
          communityNotifications: server.communityNotifications,
          lostPetNotifications: server.lostPetNotifications,
          matchNotifications: server.matchNotifications,
        } : stored;
        let changed = false;
        if (currentPermission && !notificationsAllowed(currentPermission)) {
          next = { ...stored, communityNotifications: false, lostPetNotifications: false, matchNotifications: false };
          changed = notificationKeys.some(key => stored[key]);
        }
        if (changed || server?.isConfigured) await writeSettings(next);
        if (token && (!server?.isConfigured || changed)) {
          await updateMobileNotificationPreferences(token, {
            communityNotifications: next.communityNotifications,
            lostPetNotifications: next.lostPetNotifications,
            matchNotifications: next.matchNotifications,
          }).catch(() => undefined);
        }
        if (active) {
          setSettings(next);
          setPermission(currentPermission);
        }
      })
      .catch(() => active && Alert.alert('Tercihler yüklenemedi', 'Ayarlar varsayılan değerlerle açıldı.'))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [token, username]);

  useEffect(() => {
    if (!token) { setProfileVisibility({ showBioToOthers: true, showPetsToOthers: true }); return; }
    const controller = new AbortController();
    getProfileVisibility(token, controller.signal)
      .then(setProfileVisibility)
      .catch(reason => { if (!controller.signal.aborted) Alert.alert('Profil ayarları yüklenemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.'); });
    return () => controller.abort();
  }, [token]);

  useEffect(() => {
    setSettings(current => current.darkTheme === darkTheme ? current : { ...current, darkTheme });
  }, [darkTheme]);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', state => {
      if (state === 'active') void readNotificationPermission().then(setPermission).catch(() => setPermission(null));
    });
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    if (page === 'main') return;
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      setPage('main');
      return true;
    });
    return () => subscription.remove();
  }, [page]);

  const persist = async (next: PetimSettings) => {
    const previous = settings;
    setSettings(next);
    setSaving(true);
    try {
      await writeSettings(next);
      return true;
    } catch {
      setSettings(previous);
      Alert.alert('Ayar kaydedilemedi', 'Lütfen tekrar dene.');
      return false;
    } finally {
      setSaving(false);
    }
  };

  const updateNotification = async (key: NotificationSettingKey, value: boolean) => {
    if (saving) return;
    if (isExpoGo) {
      const next = { ...settings, [key]: value };
      if (!(await persist(next))) return;
      if (token) {
        try {
          await updateMobileNotificationPreferences(token, {
            communityNotifications: next.communityNotifications,
            lostPetNotifications: next.lostPetNotifications,
            matchNotifications: next.matchNotifications,
          });
        } catch {
          await persist(settings);
          Alert.alert('Ayar kaydedilemedi', 'Bildirim tercihi sunucuya kaydedilemedi.');
        }
      }
      return;
    }
    if (value) {
      let current = permission;
      if (!current || !notificationsAllowed(current)) {
        current = await requestNotificationPermission().catch(() => null);
        setPermission(current);
      }
      if (!current || !notificationsAllowed(current)) {
        const useSettings = current?.canAskAgain === false;
        Alert.alert(
          'Bildirim izni gerekli',
          useSettings ? 'Bildirimlere cihaz ayarlarından izin verdikten sonra bu tercihi açabilirsin.' : 'Bu özellik için Pet’im bildirimlerine izin vermelisin.',
          useSettings
            ? [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Ayarları Aç', onPress: () => void Linking.openSettings() }]
            : [{ text: 'Tamam' }],
        );
        return;
      }
    }
    const next = { ...settings, [key]: value };
    if (!(await persist(next))) return;
    if (token) {
      try {
        await updateMobileNotificationPreferences(token, {
          communityNotifications: next.communityNotifications,
          lostPetNotifications: next.lostPetNotifications,
          matchNotifications: next.matchNotifications,
        });
        if (notificationKeys.some(item => next[item])) void registerForPushNotifications(token).catch(() => undefined);
      } catch {
        await persist(settings);
        Alert.alert('Ayar kaydedilemedi', 'Bildirim tercihi sunucuya kaydedilemedi. Lütfen tekrar dene.');
        return;
      }
    }
    if (!value && !notificationKeys.some(item => next[item])) {
      await clearNotifications().catch(() => undefined);
    }
  };

  const updateTheme = async (value: boolean) => {
    if (saving) return;
    const next = { ...settings, darkTheme: value };
    if (await persist(next)) onThemeChange(value);
  };

  const updateProfilePrivacy = async (key: keyof ProfileVisibilitySettings, value: boolean) => {
    if (!token || profileVisibilitySaving) return;
    const previous = profileVisibility;
    const next = { ...previous, [key]: value };
    setProfileVisibility(next);
    setProfileVisibilitySaving(true);
    try { setProfileVisibility(await updateProfileVisibility(token, next)); }
    catch (reason) {
      setProfileVisibility(previous);
      Alert.alert('Ayar kaydedilemedi', reason instanceof Error ? reason.message : 'Profil görünürlüğü kaydedilemedi.');
    } finally { setProfileVisibilitySaving(false); }
  };

  const confirmLogout = () => Alert.alert(
    'Çıkış yapmak istiyor musun?',
    'Bu cihazdaki oturumun kapatılacak. Hesabın ve içeriklerin silinmeyecek.',
    [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Çıkış Yap', style: 'destructive', onPress: onLogout }],
  );

  if (page === 'contact' && token) return <ContactSettingsPage token={token} onBack={() => setPage('main')} />;
  if (page === 'report' && token) return <ReportProblemPage token={token} onBack={() => setPage('main')} />;
  if (page === 'help') return <HelpPage onBack={() => setPage('main')} onOpenQuestions={onOpenQuestions} />;
  if (page === 'about') return <AboutPage onBack={() => setPage('main')} />;
  if (page === 'appInfo') return <AppInfoPage onBack={() => setPage('main')} onOpenLegal={() => setPage('legal')} />;
  if (page === 'legal') return <LegalPage onBack={() => setPage('main')} />;

  const allowed = permission ? notificationsAllowed(permission) : false;
  const version = Application.nativeApplicationVersion || '1.0.0';
  const build = Application.nativeBuildVersion;

  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <StatusBar style={darkTheme ? 'light' : 'dark'} />
    <View style={styles.header}>
      <View><Text style={styles.eyebrow}>PET’İM</Text><Text style={styles.title}>Ayarlar</Text><Text style={styles.subtitle}>Deneyimini ve gizliliğini yönet.</Text></View>
    </View>

    <Pressable onPress={username ? onOpenAccount : onLogin} accessibilityRole="button" accessibilityLabel={username ? 'Hesap bilgilerini aç' : 'Hesaba giriş yap'} style={({ pressed }) => [styles.profileCard, pressed && styles.pressed]}>
      <View style={styles.avatar}>{username ? <Text style={styles.avatarText}>{username.charAt(0).toLocaleUpperCase('tr-TR')}</Text> : <Ionicons name="person-outline" size={25} color={colors.white} />}</View>
      <View style={styles.flexOne}><Text style={styles.profileTitle}>{username || 'Hesabına giriş yap'}</Text><Text style={styles.profileText}>{username ? 'Profil ve hesap bilgilerini görüntüle' : 'Ayarlarını hesabınla birlikte kullan'}</Text></View>
      <Ionicons name="chevron-forward" size={20} color={colors.white} />
    </Pressable>

    {loading ? <View style={styles.loading}><ActivityIndicator color={colors.primary} /><Text style={styles.loadingText}>Tercihler yükleniyor…</Text></View> : <>
      <SectionTitle icon="person-circle-outline" title="Profil bilgileri" />
      <View style={styles.card}>
        <SettingSwitch icon="document-text-outline" title="Hakkımda bilgimi diğer kullanıcılar görsün" subtitle="Kapalıyken biyografin herkese açık profilinde gösterilmez" value={profileVisibility.showBioToOthers} disabled={!token || profileVisibilitySaving} onChange={value => void updateProfilePrivacy('showBioToOthers', value)} />
        <SettingSwitch icon="paw-outline" title="Pati bilgilerimi diğer kullanıcılar görsün" subtitle="Kapalıyken patilerin herkese açık profilinde listelenmez" value={profileVisibility.showPetsToOthers} disabled={!token || profileVisibilitySaving} onChange={value => void updateProfilePrivacy('showPetsToOthers', value)} last />
      </View>

      <SectionTitle icon="color-palette-outline" title="Görünüm" />
      <View style={styles.card}>
        <SettingSwitch icon="moon-outline" title="Koyu tema" subtitle="Uygulamayı koyu renklerle kullan" value={settings.darkTheme} disabled={saving} onChange={value => void updateTheme(value)} last />
      </View>

      <SectionTitle icon="notifications-outline" title="Bildirimler" />
      {!isExpoGo ? <View style={styles.permissionCard}>
        <View style={[styles.permissionIcon, allowed ? styles.permissionIconOn : styles.permissionIconOff]}><Ionicons name={allowed ? 'checkmark' : 'notifications-off-outline'} size={18} color={allowed ? '#3F684A' : '#9B463B'} /></View>
        <View style={styles.flexOne}><Text style={styles.permissionTitle}>{allowed ? 'Cihaz bildirimleri açık' : 'Cihaz bildirimleri kapalı'}</Text><Text style={styles.permissionText}>{allowed ? 'Hangi bildirimleri istediğini aşağıdan seçebilirsin.' : 'Bir tercihi açtığında cihaz izni istenir.'}</Text></View>
        {!allowed && permission?.canAskAgain === false ? <Pressable onPress={() => void Linking.openSettings()}><Text style={styles.inlineAction}>Aç</Text></Pressable> : null}
      </View> : null}
      <View style={styles.card}>
        <InfoRow icon="notifications-circle-outline" title="Bildirim merkezi" subtitle={username ? `${unreadNotifications} okunmamış bildirim` : 'Bildirimleri görmek için giriş yap'} onPress={onOpenNotifications} />
        <SettingSwitch icon="chatbubbles-outline" title="Topluluk" subtitle="Yorum, yanıt ve etkileşimler" value={settings.communityNotifications} disabled={saving} onChange={value => void updateNotification('communityNotifications', value)} />
        <SettingSwitch icon="location-outline" title="Kayıp pati uyarıları" subtitle="Yakınındaki önemli ilanlar" value={settings.lostPetNotifications} disabled={saving} onChange={value => void updateNotification('lostPetNotifications', value)} />
        <SettingSwitch icon="heart-outline" title="PatiMatch" subtitle="Yeni eşleşme ve mesajlar" value={settings.matchNotifications} disabled={saving} onChange={value => void updateNotification('matchNotifications', value)} last />
      </View>

      <SectionTitle icon="shield-checkmark-outline" title="Gizlilik ve konum" />
      <View style={styles.card}>
        <InfoRow icon="call-outline" title="İletişim bilgileri" subtitle={username ? 'Telefon, e-posta ve paylaşım izinlerini yönet' : 'Yönetmek için hesabına giriş yap'} onPress={username ? () => setPage('contact') : onLogin} />
        <InfoRow icon="paw-outline" title="PatiMatch görünürlüğü" subtitle={username ? 'Görünür patilerini ve eşleşme tercihlerini yönet' : 'Yönetmek için hesabına giriş yap'} onPress={username ? onOpenPatiMatch : onLogin} />
        <InfoRow icon="navigate-outline" title="Yaklaşık konum koruması" subtitle="Kesin ev adresin paylaşılmaz; bu koruma her zaman açık" onPress={() => Alert.alert('Yaklaşık konum koruması', 'Pet’im diğer kullanıcılara kesin koordinatını veya ev adresini göstermez. İlan ve keşif alanlarında konum yaklaşıklaştırılır.')} />
        <InfoRow icon="options-outline" title="Cihaz konum izni" subtitle="Pet’im için konum erişimini yönet" onPress={() => void Linking.openSettings()} last />
      </View>
      <View style={styles.safetyNote}><Ionicons name="lock-closed" size={20} color="#4E7458" /><Text style={styles.safetyText}>Pet’im kesin ev adresini herkese açık biçimde paylaşmaz. Konum izni yalnızca ihtiyaç duyulan özellik açıldığında istenir.</Text></View>

      <SectionTitle icon="information-circle-outline" title="Uygulama" />
      <View style={styles.card}>
        <InfoRow icon="warning-outline" title="Sorun bildir" subtitle={username ? 'Bir sorun anlat ve takip numarası al' : 'Sorun bildirmek için giriş yap'} onPress={username ? () => setPage('report') : onLogin} />
        <InfoRow icon="help-circle-outline" title="Yardım ve destek" subtitle="SSS, topluluk ve destek e-postası" onPress={() => setPage('help')} />
        <InfoRow icon="paw-outline" title="Hakkımızda" subtitle="Pet’im’in amacı ve topluluk ilkeleri" onPress={() => setPage('about')} />
        <InfoRow icon="phone-portrait-outline" title="Uygulama bilgisi" subtitle={'Sürüm ' + version + (build ? ' (' + build + ')' : '')} onPress={() => setPage('appInfo')} last />
      </View>
    </>}
    {username ? <Pressable onPress={confirmLogout} accessibilityRole="button" style={({ pressed }) => [styles.logoutButton, pressed && styles.pressed]}><Ionicons name="log-out-outline" size={20} color="#8E4036" /><Text style={styles.logoutText}>Çıkış Yap</Text></Pressable> : null}
  </ScrollView>;
}

function ContactSettingsPage({ token, onBack }: { token: string; onBack: () => void }) {
  const [form, setForm] = useState<MobileContactSettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let active = true;
    getMobileContactSettings(token)
      .then(value => active && setForm(value))
      .catch(reason => active && Alert.alert('Bilgiler yüklenemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.'))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [token]);

  const update = <K extends keyof MobileContactSettings>(key: K, value: MobileContactSettings[K]) => {
    setForm(current => current ? { ...current, [key]: value } : current);
  };
  const save = async () => {
    if (!form || saving) return;
    setSaving(true);
    try {
      const saved = await updateMobileContactSettings(token, {
        phone: form.phone?.trim() || null,
        email: form.email?.trim() || null,
        allowPatiMatchSharing: form.allowPatiMatchSharing,
        allowAdoptionSharing: form.allowAdoptionSharing,
        allowLostPetSharing: form.allowLostPetSharing,
      });
      setForm(saved);
      Alert.alert('Kaydedildi', 'İletişim bilgilerin ve paylaşım izinlerin güncellendi.');
    } catch (reason) {
      Alert.alert('Kaydedilemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.');
    } finally { setSaving(false); }
  };

  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false} keyboardShouldPersistTaps="handled">
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="İletişim bilgileri" onBack={onBack} />
    <Text style={styles.detailLead}>Telefon ve e-posta bilgilerin varsayılan olarak gizlidir. İzinleri ayrı ayrı sen yönetirsin.</Text>
    {loading || !form ? <View style={styles.loading}><ActivityIndicator color={colors.primary} /><Text style={styles.loadingText}>İletişim bilgileri yükleniyor…</Text></View> : <>
      <View style={styles.formCard}>
        <Text style={styles.inputLabel}>Telefon</Text>
        <TextInput value={form.phone ?? ''} onChangeText={value => update('phone', value)} placeholder="Örn. +90 5xx xxx xx xx" placeholderTextColor={colors.muted} keyboardType="phone-pad" textContentType="telephoneNumber" autoComplete="tel" style={styles.input} />
        <Text style={styles.inputLabel}>E-posta</Text>
        <TextInput value={form.email ?? ''} onChangeText={value => update('email', value)} placeholder="ornek@eposta.com" placeholderTextColor={colors.muted} keyboardType="email-address" textContentType="emailAddress" autoComplete="email" autoCapitalize="none" autoCorrect={false} style={styles.input} />
      </View>
      <SectionTitle icon="lock-closed-outline" title="Paylaşım izinleri" />
      <View style={styles.card}>
        <SettingSwitch icon="heart-outline" title="Eşleşen patilerle paylaşmama izin ver" subtitle="PatiMatch sohbetinde yalnızca sen paylaştığında kullanılır" value={form.allowPatiMatchSharing} disabled={saving} onChange={value => update('allowPatiMatchSharing', value)} />
        <SettingSwitch icon="home-outline" title="Yuva olma ilanlarında paylaşmama izin ver" subtitle="Seçtiğin sahiplendirme ilanlarında kullanılabilir" value={form.allowAdoptionSharing} disabled={saving} onChange={value => update('allowAdoptionSharing', value)} />
        <SettingSwitch icon="location-outline" title="Kayıp pati ilanlarında paylaşmama izin ver" subtitle="Seçtiğin kayıp pati ilanlarında kullanılabilir" value={form.allowLostPetSharing} disabled={saving} onChange={value => update('allowLostPetSharing', value)} last />
      </View>
      <View style={styles.safetyNote}><Ionicons name="eye-off-outline" size={20} color="#4E7458" /><Text style={styles.safetyText}>İzin vermen bilgilerini herkese açmaz. İletişim bilgileri yalnızca sonraki adımlarda açıkça seçtiğin eşleşme veya ilanda paylaşılır.</Text></View>
      <Pressable onPress={() => void save()} disabled={saving} style={({ pressed }) => [styles.primaryAction, styles.contactSave, (pressed || saving) && styles.pressed]}>
        {saving ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="checkmark-circle-outline" size={20} color={colors.white} /><Text style={styles.primaryActionText}>Bilgileri kaydet</Text></>}
      </Pressable>
    </>}
  </ScrollView>;
}

const reportCategories: { value: CreateSupportReportRequest['category']; label: string; icon: keyof typeof Ionicons.glyphMap }[] = [
  { value: 'technical', label: 'Teknik sorun', icon: 'build-outline' },
  { value: 'account', label: 'Hesap', icon: 'person-outline' },
  { value: 'content', label: 'İçerik', icon: 'document-text-outline' },
  { value: 'privacy', label: 'Gizlilik', icon: 'shield-outline' },
  { value: 'other', label: 'Diğer', icon: 'ellipsis-horizontal' },
];

function ReportProblemPage({ token, onBack }: { token: string; onBack: () => void }) {
  const [category, setCategory] = useState<CreateSupportReportRequest['category']>('technical');
  const [description, setDescription] = useState('');
  const [screenshot, setScreenshot] = useState<ImagePicker.ImagePickerAsset | null>(null);
  const [sending, setSending] = useState(false);

  const pickScreenshot = async () => {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      Alert.alert('Fotoğraf izni gerekli', 'Ekran görüntüsü ekleyebilmek için galeri izni vermelisin.');
      return;
    }
    const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.85 });
    if (!result.canceled) setScreenshot(result.assets[0]);
  };

  const submit = async () => {
    const cleanDescription = description.trim();
    if (cleanDescription.length < 10) {
      Alert.alert('Açıklama gerekli', 'Sorunu en az 10 karakterle anlat.');
      return;
    }
    setSending(true);
    try {
      const result = await createSupportReport(token, {
        category,
        description: cleanDescription,
        screenshot: screenshot ? { uri: screenshot.uri, mimeType: screenshot.mimeType } : undefined,
      });
      setDescription('');
      setScreenshot(null);
      Alert.alert('Bildirimin alındı', `Takip numaran: ${result.trackingNumber}\n\nBu numarayı gerektiğinde destek ekibiyle paylaşabilirsin.`, [{ text: 'Tamam', onPress: onBack }]);
    } catch (reason) {
      Alert.alert('Gönderilemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.');
    } finally {
      setSending(false);
    }
  };

  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false} keyboardShouldPersistTaps="handled">
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="Sorun bildir" onBack={onBack} />
    <Text style={styles.detailLead}>Yaşadığın sorunu anlat. Bildirimin veritabanına kaydedilir ve sana bir takip numarası verilir.</Text>
    <Text style={styles.inputLabel}>Kategori</Text>
    <View style={styles.categoryGrid}>{reportCategories.map(item => {
      const selected = item.value === category;
      return <Pressable key={item.value} onPress={() => setCategory(item.value)} style={({ pressed }) => [styles.categoryChip, selected && styles.categoryChipSelected, pressed && styles.pressed]}>
        <Ionicons name={item.icon} size={18} color={selected ? colors.white : colors.primary} /><Text style={[styles.categoryText, selected && styles.categoryTextSelected]}>{item.label}</Text>
      </Pressable>;
    })}</View>
    <View style={styles.formCard}>
      <Text style={styles.inputLabel}>Açıklama</Text>
      <TextInput value={description} onChangeText={setDescription} placeholder="Ne oldu, hangi ekranda oldu ve tekrar nasıl oluşuyor?" placeholderTextColor={colors.muted} multiline maxLength={3000} textAlignVertical="top" style={[styles.input, styles.reportDescription]} />
      <Text style={styles.characterCount}>{description.length}/3000</Text>
      <Text style={styles.inputLabel}>Ekran görüntüsü (isteğe bağlı)</Text>
      {screenshot ? <View style={styles.screenshotWrap}><Image source={{ uri: screenshot.uri }} style={styles.screenshotPreview} /><Pressable accessibilityLabel="Ekran görüntüsünü kaldır" onPress={() => setScreenshot(null)} style={styles.removeScreenshot}><Ionicons name="close" size={18} color={colors.white} /></Pressable></View> : null}
      <Pressable onPress={() => void pickScreenshot()} style={({ pressed }) => [styles.uploadButton, pressed && styles.pressed]}><Ionicons name="image-outline" size={20} color={colors.primary} /><Text style={styles.uploadText}>{screenshot ? 'Farklı görsel seç' : 'Galeriden ekran görüntüsü ekle'}</Text></Pressable>
    </View>
    <Pressable disabled={sending} onPress={() => void submit()} style={({ pressed }) => [styles.primaryAction, styles.reportSubmit, (pressed || sending) && styles.pressed]}>
      {sending ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="send-outline" size={20} color={colors.white} /><Text style={styles.primaryActionText}>Bildirimi gönder</Text></>}
    </Pressable>
  </ScrollView>;
}

function HelpPage({ onBack, onOpenQuestions }: { onBack: () => void; onOpenQuestions: () => void }) {
  const [openQuestion, setOpenQuestion] = useState<number | null>(0);
  const faqs = [
    ['Bildirimler gelmiyor', 'Ayarlar ekranında istediğin bildirimi aç. Cihaz izni kapalıysa bildirim kartındaki Aç düğmesini kullan.'],
    ['Konumum paylaşılıyor mu?', 'Kesin ev adresin ve kesin koordinatın diğer kullanıcılara gösterilmez. Konum, ilgili özelliklerde yaklaşık olarak kullanılır.'],
    ['Sağlık içerikleri tanı koyar mı?', 'Hayır. Hastalık içerikleri farkındalık içindir; acil veya ağır belirtilerde veteriner hekime başvurmalısın.'],
  ];
  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="Yardım ve destek" onBack={onBack} />
    <View style={styles.urgentCard}><Ionicons name="medkit-outline" size={24} color="#9B463B" /><View style={styles.flexOne}><Text style={styles.urgentTitle}>Acil bir durum mu?</Text><Text style={styles.urgentText}>Burada yanıt bekleme; en yakın veteriner kliniğiyle doğrudan iletişim kur.</Text></View></View>
    <Text style={styles.detailLead}>Sık karşılaşılan konulara hızlıca göz at veya topluluktan destek al.</Text>
    <View style={styles.faqCard}>{faqs.map(([title, answer], index) => {
      const open = openQuestion === index;
      return <Pressable key={title} onPress={() => setOpenQuestion(open ? null : index)} style={({ pressed }) => [styles.faqRow, index === faqs.length - 1 && styles.rowLast, pressed && styles.pressed]}>
        <View style={styles.flexOne}><Text style={styles.faqTitle}>{title}</Text>{open ? <Text style={styles.faqBody}>{answer}</Text> : null}</View><Ionicons name={open ? 'chevron-up' : 'chevron-down'} size={18} color={colors.primary} />
      </Pressable>;
    })}</View>
    <Pressable onPress={onOpenQuestions} style={({ pressed }) => [styles.primaryAction, pressed && styles.pressed]}><Ionicons name="chatbubbles-outline" size={20} color={colors.white} /><Text style={styles.primaryActionText}>Topluluğa soru sor</Text></Pressable>
    <Pressable onPress={() => void openUrl('mailto:admin@petwork.com?subject=Pet%27im%20destek')} style={({ pressed }) => [styles.secondaryAction, pressed && styles.pressed]}><Ionicons name="mail-outline" size={20} color={colors.primary} /><Text style={styles.secondaryActionText}>Destek ekibine e-posta gönder</Text></Pressable>
  </ScrollView>;
}

function AboutPage({ onBack }: { onBack: () => void }) {
  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="Hakkımızda" onBack={onBack} />
    <View style={styles.aboutHero}><Ionicons name="paw" size={32} color={colors.peach} /><Text style={styles.aboutBrand}>Pet’im</Text><Text style={styles.aboutBy}>by Paws&Us</Text></View>
    <Text style={styles.detailLead}>Pet’im, patilerin sağlığı, bakımı ve güvenli biçimde bir araya gelmesi için Paws&Us tarafından geliştirilen bir topluluk uygulamasıdır.</Text>
    <View style={styles.legalCard}>
      <LegalSection icon="heart-outline" title="Amacımız" text="Doğru bilgiye erişimi kolaylaştırmak, kayıp patilerin evine dönmesine yardımcı olmak ve güvenli sahiplendirme ile arkadaşlık süreçlerini desteklemek." />
      <LegalSection icon="shield-checkmark-outline" title="Güvenli topluluk" text="Saygılı iletişimi, kişisel bilgilerin kontrollü paylaşılmasını ve şüpheli içeriklerin bildirilmesini temel alırız." />
      <LegalSection icon="cash-outline" title="Ücretsiz sahiplendirme" text="Pet’imde sahiplendirmeler tamamen ücretsizdir. Hayvan satışı veya sahiplendirme için para talep edilmesine izin verilmez." last />
    </View>
  </ScrollView>;
}

function AppInfoPage({ onBack, onOpenLegal }: { onBack: () => void; onOpenLegal: () => void }) {
  const version = Application.nativeApplicationVersion || '1.0.0';
  const build = Application.nativeBuildVersion || 'geliştirme';
  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="Uygulama bilgisi" onBack={onBack} />
    <View style={styles.versionCard}><View style={styles.versionIcon}><Ionicons name="paw" size={28} color={colors.white} /></View><View><Text style={styles.versionName}>Pet’im mobil</Text><Text style={styles.versionText}>Sürüm {version}</Text><Text style={styles.versionText}>Yapı {build}</Text></View></View>
    <View style={styles.card}>
      <InfoRow icon="shield-checkmark-outline" title="Gizlilik politikası" subtitle="Verilerin nasıl korunduğunu gör" onPress={() => void openUrl(apiUrl + '/Home/Privacy')} />
      <InfoRow icon="document-text-outline" title="Kullanım koşulları" subtitle="Topluluk ve kullanım kurallarını gör" onPress={onOpenLegal} last />
    </View>
  </ScrollView>;
}

function LegalPage({ onBack }: { onBack: () => void }) {
  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="Gizlilik ve koşullar" onBack={onBack} />
    <Text style={styles.detailLead}>Pet’im’i kullanırken verilerin ve topluluk güvenliği için geçerli temel ilkeler.</Text>
    <View style={styles.legalCard}>
      <LegalSection icon="shield-checkmark-outline" title="Verilerin" text="Oturum ve uygulama tercihleri cihazında güvenli biçimde saklanır. Kesin ev adresin herkese açık paylaşılmaz. Kullanıcı verileri dış içerik veya çeviri servislerine gönderilmez." />
      <LegalSection icon="location-outline" title="Konum" text="Konum yalnızca yakındaki hizmetler, kayıp pati ilanları ve konuma bağlı özellikler için, sen izin verdiğinde kullanılır. İzni cihaz ayarlarından kaldırabilirsin." />
      <LegalSection icon="medkit-outline" title="Sağlık ve beslenme" text="İçerikler bilgi ve farkındalık içindir; veteriner muayenesinin, tanının veya tedavinin yerini almaz. Acil durumlarda doğrudan veteriner hekime başvur." />
      <LegalSection icon="people-outline" title="Topluluk koşulları" text="Paylaşımlarda saygılı ol, yanıltıcı sağlık bilgisi verme, kişisel bilgi veya izinsiz görüntü paylaşma. Bildirilen içerikler güvenlik amacıyla incelenebilir." last />
    </View>
    <Pressable onPress={() => void openUrl(apiUrl + '/Home/Privacy')} style={({ pressed }) => [styles.secondaryAction, pressed && styles.pressed]}><Ionicons name="open-outline" size={20} color={colors.primary} /><Text style={styles.secondaryActionText}>Güncel politikayı webde aç</Text></Pressable>
  </ScrollView>;
}

async function openUrl(url: string) {
  try {
    if (!await Linking.canOpenURL(url)) throw new Error('unsupported');
    await Linking.openURL(url);
  } catch {
    Alert.alert('Bağlantı açılamadı', 'Bu bağlantıyı açabilecek bir uygulama bulunamadı.');
  }
}

function DetailHeader({ title, onBack }: { title: string; onBack: () => void }) {
  return <View style={styles.detailHeader}><Pressable accessibilityLabel="Ayarlara dön" onPress={onBack} style={({ pressed }) => [styles.backButton, pressed && styles.pressed]}><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable><Text style={styles.detailTitle}>{title}</Text><View style={styles.headerSpacer} /></View>;
}
function LegalSection({ icon, title, text, last = false }: { icon: keyof typeof Ionicons.glyphMap; title: string; text: string; last?: boolean }) {
  return <View style={[styles.legalSection, last && styles.rowLast]}><View style={styles.rowIcon}><Ionicons name={icon} size={20} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.legalTitle}>{title}</Text><Text style={styles.legalText}>{text}</Text></View></View>;
}
function SectionTitle({ icon, title }: { icon: keyof typeof Ionicons.glyphMap; title: string }) {
  return <View style={styles.sectionTitleRow}><Ionicons name={icon} size={18} color={colors.primary} /><Text style={styles.sectionTitle}>{title}</Text></View>;
}
function SettingSwitch({ icon, title, subtitle, value, onChange, last = false, disabled = false }: { icon: keyof typeof Ionicons.glyphMap; title: string; subtitle: string; value: boolean; onChange: (value: boolean) => void; last?: boolean; disabled?: boolean }) {
  return <View style={[styles.row, last && styles.rowLast, disabled && styles.disabled]}><View style={styles.rowIcon}><Ionicons name={icon} size={20} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.rowTitle}>{title}</Text><Text style={styles.rowSubtitle}>{subtitle}</Text></View><Switch value={value} onValueChange={onChange} disabled={disabled} trackColor={{ false: '#D9CFD3', true: colors.sage }} thumbColor={value ? colors.primary : colors.card} ios_backgroundColor="#D9CFD3" accessibilityLabel={title} /></View>;
}
function InfoRow({ icon, title, subtitle, onPress, last = false }: { icon: keyof typeof Ionicons.glyphMap; title: string; subtitle?: string; onPress: () => void; last?: boolean }) {
  return <Pressable accessibilityRole="button" accessibilityLabel={title} onPress={onPress} style={({ pressed }) => [styles.row, last && styles.rowLast, pressed && styles.pressed]}><View style={styles.rowIcon}><Ionicons name={icon} size={20} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.rowTitle}>{title}</Text>{subtitle ? <Text style={styles.rowSubtitle}>{subtitle}</Text> : null}</View><Ionicons name="chevron-forward" size={18} color="#AA9DA4" /></Pressable>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = createThemedStyles(() => ({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingTop: Platform.OS === 'ios' ? 58 : 32, paddingHorizontal: 20, paddingBottom: 118 },
  detailContent: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingTop: Platform.OS === 'ios' ? 58 : 32, paddingHorizontal: 20, paddingBottom: 70 },
  header: { marginBottom: 22 },
  eyebrow: { color: colors.peach, fontSize: 11, fontWeight: '900', letterSpacing: 2 },
  title: { color: colors.text, fontFamily: serif, fontSize: 34, fontWeight: '700', marginTop: 4 },
  subtitle: { color: colors.muted, fontSize: 12, marginTop: 4 },
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
  disabled: { opacity: 0.7 },
  permissionCard: { minHeight: 70, flexDirection: 'row', alignItems: 'center', gap: 11, padding: 13, borderRadius: 18, backgroundColor: colors.sageSoft, marginBottom: 10 },
  permissionIcon: { width: 36, height: 36, borderRadius: 18, alignItems: 'center', justifyContent: 'center' },
  permissionIconOn: { backgroundColor: '#D5E5D3' },
  permissionIconOff: { backgroundColor: colors.peachSoft },
  permissionTitle: { color: colors.text, fontSize: 12, fontWeight: '900' },
  permissionText: { color: colors.muted, fontSize: 9.5, lineHeight: 14, marginTop: 3 },
  inlineAction: { color: colors.primary, fontSize: 12, fontWeight: '900', padding: 8 },
  safetyNote: { flexDirection: 'row', alignItems: 'flex-start', gap: 11, marginTop: 13, padding: 15, borderRadius: 18, backgroundColor: colors.sageSoft },
  safetyText: { flex: 1, color: '#526259', fontSize: 10, lineHeight: 15 },
  loading: { minHeight: 220, alignItems: 'center', justifyContent: 'center', gap: 10 },
  loadingText: { color: colors.muted, fontSize: 11 },
  logoutButton: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, marginTop: 22, borderRadius: 18, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach },
  logoutText: { color: '#8E4036', fontSize: 13, fontWeight: '900' },
  detailHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 22 },
  backButton: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft },
  headerSpacer: { width: 44 },
  detailTitle: { color: colors.text, fontFamily: serif, fontSize: 23, fontWeight: '700' },
  detailLead: { color: colors.muted, fontSize: 12, lineHeight: 18, marginBottom: 16 },
  urgentCard: { flexDirection: 'row', alignItems: 'center', gap: 12, borderRadius: 19, padding: 15, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach, marginBottom: 18 },
  urgentTitle: { color: '#8E4036', fontSize: 12, fontWeight: '900' },
  urgentText: { color: '#76544E', fontSize: 9.5, lineHeight: 14, marginTop: 3 },
  faqCard: { backgroundColor: colors.card, borderRadius: 22, paddingHorizontal: 15, borderWidth: 1, borderColor: colors.border, marginBottom: 18, ...shadow },
  faqRow: { minHeight: 66, flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 14, borderBottomWidth: 1, borderBottomColor: colors.border },
  faqTitle: { color: colors.text, fontSize: 12, fontWeight: '900' },
  faqBody: { color: colors.muted, fontSize: 10, lineHeight: 16, marginTop: 8 },
  primaryAction: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, borderRadius: 18, backgroundColor: colors.primary, marginBottom: 10 },
  primaryActionText: { color: colors.white, fontSize: 12, fontWeight: '900' },
  secondaryAction: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, borderRadius: 18, backgroundColor: colors.lilacSoft, borderWidth: 1, borderColor: '#DDCEE5', marginTop: 10 },
  secondaryActionText: { color: colors.primary, fontSize: 12, fontWeight: '900' },
  formCard: { backgroundColor: colors.card, borderRadius: 22, padding: 16, borderWidth: 1, borderColor: colors.border, ...shadow },
  inputLabel: { color: colors.text, fontSize: 11, fontWeight: '900', marginBottom: 7 },
  input: { minHeight: 50, color: colors.text, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border, borderRadius: 15, paddingHorizontal: 14, fontSize: 13, marginBottom: 15 },
  contactSave: { marginTop: 18 },
  categoryGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: 16 },
  categoryChip: { minHeight: 42, flexDirection: 'row', alignItems: 'center', gap: 7, paddingHorizontal: 13, borderRadius: 15, backgroundColor: colors.lilacSoft, borderWidth: 1, borderColor: '#DDCEE5' },
  categoryChipSelected: { backgroundColor: colors.primary, borderColor: colors.primary },
  categoryText: { color: colors.primary, fontSize: 10.5, fontWeight: '800' },
  categoryTextSelected: { color: colors.white },
  reportDescription: { minHeight: 130, paddingTop: 13, paddingBottom: 13, marginBottom: 5 },
  characterCount: { color: colors.muted, fontSize: 9, textAlign: 'right', marginBottom: 14 },
  uploadButton: { minHeight: 48, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, borderRadius: 15, borderWidth: 1, borderColor: '#DDCEE5', backgroundColor: colors.lilacSoft },
  uploadText: { color: colors.primary, fontSize: 11, fontWeight: '800' },
  screenshotWrap: { position: 'relative', marginBottom: 10 },
  screenshotPreview: { width: '100%', height: 210, borderRadius: 15, backgroundColor: colors.background },
  removeScreenshot: { position: 'absolute', right: 9, top: 9, width: 32, height: 32, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary },
  reportSubmit: { marginTop: 18 },
  aboutHero: { alignItems: 'center', justifyContent: 'center', minHeight: 160, borderRadius: 24, backgroundColor: colors.primary, marginBottom: 18, ...shadow },
  aboutBrand: { color: colors.white, fontFamily: serif, fontSize: 31, fontWeight: '700', marginTop: 5 },
  aboutBy: { color: '#EADFE7', fontSize: 11, marginTop: 2 },
  versionCard: { flexDirection: 'row', alignItems: 'center', gap: 14, padding: 18, borderRadius: 22, backgroundColor: colors.sageSoft, borderWidth: 1, borderColor: colors.border, marginBottom: 16 },
  versionIcon: { width: 58, height: 58, borderRadius: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary },
  versionName: { color: colors.text, fontSize: 15, fontWeight: '900' },
  versionText: { color: colors.muted, fontSize: 10.5, marginTop: 4 },
  legalCard: { backgroundColor: colors.card, borderRadius: 22, paddingHorizontal: 15, borderWidth: 1, borderColor: colors.border, ...shadow },
  legalSection: { flexDirection: 'row', alignItems: 'flex-start', gap: 11, paddingVertical: 17, borderBottomWidth: 1, borderBottomColor: colors.border },
  legalTitle: { color: colors.text, fontSize: 13, fontWeight: '900' },
  legalText: { color: colors.muted, fontSize: 10, lineHeight: 16, marginTop: 5 },
  pressed: { opacity: 0.75, transform: [{ scale: 0.99 }] },
}));
