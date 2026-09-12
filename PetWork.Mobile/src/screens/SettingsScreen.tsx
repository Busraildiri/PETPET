import { Ionicons } from '@expo/vector-icons';
import * as Application from 'expo-application';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, AppState, BackHandler, Linking, Platform, Pressable, ScrollView, StyleSheet, Switch, Text, TextInput, View } from 'react-native';
import { apiUrl, getMobileContactSettings, getMobileNotificationPreferences, updateMobileContactSettings, updateMobileNotificationPreferences, type MobileContactSettings } from '../api';
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
};
type Page = 'main' | 'contact' | 'help' | 'legal';
type Permission = NotificationPermission | null;
const notificationKeys: NotificationSettingKey[] = ['communityNotifications', 'lostPetNotifications', 'matchNotifications'];

export function SettingsScreen({ darkTheme, onThemeChange, username, token, unreadNotifications, onOpenNotifications, onOpenAccount, onLogin, onLogout, onOpenQuestions, onOpenPatiMatch }: Props) {
  const [settings, setSettings] = useState<PetimSettings>(defaultSettings);
  const [permission, setPermission] = useState<Permission>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [page, setPage] = useState<Page>('main');

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

  const confirmLogout = () => Alert.alert(
    'Çıkış yapmak istiyor musun?',
    'Bu cihazdaki oturumun kapatılacak. Hesabın ve içeriklerin silinmeyecek.',
    [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Çıkış Yap', style: 'destructive', onPress: onLogout }],
  );

  if (page === 'contact' && token) return <ContactSettingsPage token={token} onBack={() => setPage('main')} />;
  if (page === 'help') return <HelpPage onBack={() => setPage('main')} onOpenQuestions={onOpenQuestions} />;
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
        <InfoRow icon="help-circle-outline" title="Yardım ve geri bildirim" subtitle="Sık sorulanlar, topluluk ve destek" onPress={() => setPage('help')} />
        <InfoRow icon="document-text-outline" title="Gizlilik ve kullanım koşulları" subtitle="Veri, güvenlik ve topluluk ilkeleri" onPress={() => setPage('legal')} />
        <InfoRow icon="phone-portrait-outline" title="Pet’im mobil" subtitle={'Sürüm ' + version + (build ? ' (' + build + ')' : '')} onPress={() => Alert.alert('Pet’im mobil', 'Sürüm ' + version + (build ? '\nYapı ' + build : '') + '\n\nPetWork tarafından geliştirildi.')} last />
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

function HelpPage({ onBack, onOpenQuestions }: { onBack: () => void; onOpenQuestions: () => void }) {
  const [openQuestion, setOpenQuestion] = useState<number | null>(0);
  const faqs = [
    ['Bildirimler gelmiyor', 'Ayarlar ekranında istediğin bildirimi aç. Cihaz izni kapalıysa bildirim kartındaki Aç düğmesini kullan.'],
    ['Konumum paylaşılıyor mu?', 'Kesin ev adresin ve kesin koordinatın diğer kullanıcılara gösterilmez. Konum, ilgili özelliklerde yaklaşık olarak kullanılır.'],
    ['Sağlık içerikleri tanı koyar mı?', 'Hayır. Hastalık içerikleri farkındalık içindir; acil veya ağır belirtilerde veteriner hekime başvurmalısın.'],
  ];
  return <ScrollView style={styles.screen} contentContainerStyle={styles.detailContent} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} /><DetailHeader title="Yardım" onBack={onBack} />
    <View style={styles.urgentCard}><Ionicons name="medkit-outline" size={24} color="#9B463B" /><View style={styles.flexOne}><Text style={styles.urgentTitle}>Acil bir durum mu?</Text><Text style={styles.urgentText}>Burada yanıt bekleme; en yakın veteriner kliniğiyle doğrudan iletişim kur.</Text></View></View>
    <Text style={styles.detailLead}>Sık karşılaşılan konulara hızlıca göz at veya topluluktan destek al.</Text>
    <View style={styles.faqCard}>{faqs.map(([title, answer], index) => {
      const open = openQuestion === index;
      return <Pressable key={title} onPress={() => setOpenQuestion(open ? null : index)} style={({ pressed }) => [styles.faqRow, index === faqs.length - 1 && styles.rowLast, pressed && styles.pressed]}>
        <View style={styles.flexOne}><Text style={styles.faqTitle}>{title}</Text>{open ? <Text style={styles.faqBody}>{answer}</Text> : null}</View><Ionicons name={open ? 'chevron-up' : 'chevron-down'} size={18} color={colors.primary} />
      </Pressable>;
    })}</View>
    <Pressable onPress={onOpenQuestions} style={({ pressed }) => [styles.primaryAction, pressed && styles.pressed]}><Ionicons name="chatbubbles-outline" size={20} color={colors.white} /><Text style={styles.primaryActionText}>Topluluğa soru sor</Text></Pressable>
    <Pressable onPress={() => void openUrl('mailto:admin@petwork.com?subject=Pet%27im%20geri%20bildirim')} style={({ pressed }) => [styles.secondaryAction, pressed && styles.pressed]}><Ionicons name="mail-outline" size={20} color={colors.primary} /><Text style={styles.secondaryActionText}>E-posta ile geri bildirim gönder</Text></Pressable>
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
  legalCard: { backgroundColor: colors.card, borderRadius: 22, paddingHorizontal: 15, borderWidth: 1, borderColor: colors.border, ...shadow },
  legalSection: { flexDirection: 'row', alignItems: 'flex-start', gap: 11, paddingVertical: 17, borderBottomWidth: 1, borderBottomColor: colors.border },
  legalTitle: { color: colors.text, fontSize: 13, fontWeight: '900' },
  legalText: { color: colors.muted, fontSize: 10, lineHeight: 16, marginTop: 5 },
  pressed: { opacity: 0.75, transform: [{ scale: 0.99 }] },
}));
