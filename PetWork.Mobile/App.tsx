import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import * as Location from 'expo-location';
import * as SecureStore from 'expo-secure-store';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator, Alert, AppState, BackHandler, Image, ImageBackground, Linking, Modal, Platform, Pressable, RefreshControl, ScrollView,
  StatusBar as NativeStatusBar, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { ApiError, apiUrl, getHome, getMobileNotifications, getNearbyGroomers, getNearbyPetHotels, getNearbyVeterinarians, HomePayload, logoutSession, mediaUrl, Question, refreshAuthSession, searchGroomersByArea, searchPetHotelsByArea, searchVeterinariansByArea, Story, type AuthResponse, type ContentKind, type NearbyVeterinarian } from './src/api';
import { AuthScreen } from './src/screens/AuthScreen';
import { AccountScreen } from './src/screens/AccountScreen';
import { PatiSocialScreen } from './src/screens/PatiSocialScreen';
import { PatiMatchScreen } from './src/screens/PatiMatchScreen';
import { PetsScreen } from './src/screens/PetsScreen';
import { SettingsScreen } from './src/screens/SettingsScreen';
import { ContentScreen } from './src/screens/ContentScreen';
import { LostPetsScreen } from './src/screens/LostPetsScreen';
import { AdoptionScreen } from './src/screens/AdoptionScreen';
import { ReviewsScreen } from './src/screens/ReviewsScreen';
import { NotificationsScreen } from './src/screens/NotificationsScreen';
import { configureNotifications, registerForPushNotifications } from './src/notifications';
import { isExpoGo, readSettings } from './src/settings';
import { colors, createThemedStyles, getThemeMode, setThemeMode, shadow } from './src/theme';
import type { SocialTab } from './src/types/social';
import { clearSession, restoreSession, saveSession } from './src/session';

type TabKey = 'home' | 'social' | 'lost' | 'match' | 'settings';
type PageKey = 'root' | 'login' | 'register' | 'reset' | 'account' | 'pets' | 'nearby' | 'adoption' | 'reviews' | 'content' | 'notifications';

const locationConsentKey = 'petwork_location_consent_v1';

void configureNotifications();

type HomeCategory = {
  title: string;
  icon: keyof typeof Ionicons.glyphMap;
  color: string;
  ink: string;
  kind: ContentKind | null;
};

const getCategories = (): HomeCategory[] => {
  const dark = getThemeMode() === 'dark';
  return [
    { title: 'Soru-Cevap', icon: 'help-circle-outline', color: dark ? '#352A3D' : colors.lilacSoft, ink: dark ? '#E7C8F0' : colors.primary, kind: null },
    { title: 'Bakım Rehberleri', icon: 'book-outline', color: dark ? '#26372D' : colors.sageSoft, ink: dark ? '#AAD3B3' : '#4E7458', kind: 'guides' },
    { title: 'Hastalıklar', icon: 'medkit-outline', color: dark ? '#432D2C' : colors.peachSoft, ink: dark ? '#F0A18F' : '#A65345', kind: 'diseases' },
    { title: 'Tarifler', icon: 'restaurant-outline', color: dark ? '#3D3423' : colors.yellowSoft, ink: dark ? '#F1D17D' : '#8A6515', kind: 'recipes' },
    { title: 'Blog', icon: 'create-outline', color: dark ? '#352A3D' : colors.lilacSoft, ink: dark ? '#E7C8F0' : colors.primary, kind: 'blogs' },
    { title: 'Yas ve Kayıp', icon: 'heart-outline', color: dark ? '#26372D' : colors.sageSoft, ink: dark ? '#AAD3B3' : '#4E7458', kind: 'grief' },
  ];
};

const tabs = [
  { key: 'home' as const, label: 'Ana Sayfa', icon: 'home-outline' as const, active: 'home' as const },
  { key: 'social' as const, label: 'PatiSosyal', icon: 'paw-outline' as const, active: 'paw' as const },
  { key: 'lost' as const, label: 'Kayıp', icon: 'location-outline' as const, active: 'location' as const },
  { key: 'match' as const, label: 'PatiMatch', icon: 'heart-outline' as const, active: 'heart' as const },
  { key: 'settings' as const, label: 'Ayarlar', icon: 'settings-outline' as const, active: 'settings' as const },
];

export default function App() {
  const [tab, setTab] = useState<TabKey>('home');
  const [page, setPage] = useState<PageKey>('root');
  const [showSplash, setShowSplash] = useState(true);
  const [restoringSession, setRestoringSession] = useState(true);
  const [currentUser, setCurrentUser] = useState<string | null>(null);
  const [authToken, setAuthToken] = useState<string | null>(null);
  const [authExpiresAt, setAuthExpiresAt] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState<string | null>(null);
  const [resetToken, setResetToken] = useState('');
  const [socialInitialTab, setSocialInitialTab] = useState<SocialTab>('posts');
  const [nearbyCategory, setNearbyCategory] = useState<'veterinarian' | 'groomer' | 'hotel'>('veterinarian');
  const [contentKind, setContentKind] = useState<ContentKind>('blogs');
  const [matchChatOpen, setMatchChatOpen] = useState(false);
  const [unreadNotifications, setUnreadNotifications] = useState(0);
  const [darkTheme, setDarkTheme] = useState(false);
  const [lostInitialListingId, setLostInitialListingId] = useState<number | null>(null);
  const [adoptionInitialListingId, setAdoptionInitialListingId] = useState<number | null>(null);
  const [socialInitialPostId, setSocialInitialPostId] = useState<number | null>(null);
  const [matchInitialTargetPetId, setMatchInitialTargetPetId] = useState<number | null>(null);
  const [questionInitialId, setQuestionInitialId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    void readSettings().then(settings => {
      if (!active) return;
      setThemeMode(settings.darkTheme ? 'dark' : 'light');
      setDarkTheme(settings.darkTheme);
    }).catch(() => undefined);
    return () => { active = false; };
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setShowSplash(false), 1600);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => { void restoreSession().then(result => {
    if (result) {
      setCurrentUser(result.user.username);
      setAuthToken(result.session.token);
      setAuthExpiresAt(result.session.expiresAt);
      setRefreshToken(result.session.refreshToken);
    }
  }).finally(() => setRestoringSession(false)); }, []);

  useEffect(() => {
    if (!refreshToken || !authExpiresAt) return;
    let cancelled = false;
    let renewing = false;
    let timer: ReturnType<typeof setTimeout>;
    const expires = new Date(authExpiresAt).getTime();
    const renew = async () => {
      if (renewing) return;
      renewing = true;
      try {
        const session = await refreshAuthSession(refreshToken);
        if (cancelled) return;
        await saveSession(session);
        setCurrentUser(session.username);
        setAuthToken(session.token);
        setAuthExpiresAt(session.expiresAt);
        setRefreshToken(session.refreshToken);
      } catch (reason) {
        if (cancelled) return;
        if (reason instanceof ApiError && reason.status === 401) {
          await clearSession();
          setCurrentUser(null);
          setAuthToken(null);
          setAuthExpiresAt(null);
          setRefreshToken(null);
          return;
        }
        timer = setTimeout(() => void renew(), 30_000);
      } finally {
        renewing = false;
      }
    };
    const delay = Number.isFinite(expires) ? Math.max(0, expires - Date.now() - 60_000) : 0;
    timer = setTimeout(() => void renew(), delay);
    const subscription = AppState.addEventListener('change', state => {
      if (state === 'active' && (!Number.isFinite(expires) || expires <= Date.now() + 60_000))
        void renew();
    });
    return () => { cancelled = true; clearTimeout(timer); subscription.remove(); };
  }, [authExpiresAt, refreshToken]);

  const refreshUnreadNotifications = useCallback(async () => {
    if (!authToken) { setUnreadNotifications(0); return; }
    try { setUnreadNotifications((await getMobileNotifications(authToken)).unreadCount); }
    catch { /* Content screens will surface API errors when explicitly opened. */ }
  }, [authToken]);

  useEffect(() => {
    void refreshUnreadNotifications();
    if (!authToken) return;
    const interval = setInterval(() => void refreshUnreadNotifications(), 30_000);
    const subscription = AppState.addEventListener('change', state => { if (state === 'active') void refreshUnreadNotifications(); });
    return () => { clearInterval(interval); subscription.remove(); };
  }, [authToken, refreshUnreadNotifications]);

  useEffect(() => {
    if (!authToken) return;
    void registerForPushNotifications(authToken).catch(() => undefined);
  }, [authToken]);

  useEffect(() => {
    if (isExpoGo || Platform.OS === 'web') return;
    let active = true;
    let subscription: { remove: () => void } | undefined;
    void import('expo-notifications').then(Notifications => {
      if (!active) return;
      subscription = Notifications.addNotificationResponseReceivedListener(response => {
        const data = response.notification.request.content.data ?? {};
        const entityId = typeof data.entityId === 'number' ? data.entityId : Number(data.entityId) || undefined;
        if (data.entityType === 'lost_pet') { setLostInitialListingId(entityId ?? null); setTab('lost'); setPage('root'); }
        else if (data.entityType === 'adoption') { setAdoptionInitialListingId(entityId ?? null); setTab('social'); setPage('adoption'); }
        else if (data.entityType === 'social_post') { setSocialInitialPostId(entityId ?? null); setSocialInitialTab('posts'); setTab('social'); setPage('root'); }
        else if (data.entityType === 'pati_match') { setMatchInitialTargetPetId(entityId ?? null); setTab('match'); setPage('root'); }
        else if (data.entityType === 'question') { setQuestionInitialId(entityId ?? null); setSocialInitialTab('questions'); setTab('social'); setPage('root'); }
      });
    });
    return () => { active = false; subscription?.remove(); };
  }, []);

  useEffect(() => {
    const handleUrl = (url: string | null) => {
      if (!url) return;
      try {
        const parsed = new URL(url);
        if (parsed.hostname === 'reset-password' || parsed.pathname.includes('reset-password')) {
          const token = parsed.searchParams.get('token');
          if (token) { setResetToken(token); setPage('reset'); }
        }
      } catch { /* Ignore malformed external links. */ }
    };
    void Linking.getInitialURL().then(handleUrl);
    const subscription = Linking.addEventListener('url', event => handleUrl(event.url));
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      if (page === 'pets') { setPage('account'); return true; }
      if (page === 'reset') { setPage('login'); return true; }
      if (page !== 'root') { setPage('root'); return true; }
      if (tab !== 'home') { setTab('home'); return true; }
      return false;
    });
    return () => subscription.remove();
  }, [page, tab]);

  if (showSplash || restoringSession) {
    return <View style={styles.splashView}>
      <StatusBar style={darkTheme ? 'light' : 'dark'} />
      <Image source={require('./assets/splash-petim.png')} style={styles.splashArtwork} resizeMode="contain" accessibilityLabel="Pet'im by PetWork" />
    </View>;
  }

  const changeTab = (next: TabKey) => { if (next === 'social') setSocialInitialTab('posts'); setLostInitialListingId(null); setAdoptionInitialListingId(null); setSocialInitialPostId(null); setMatchInitialTargetPetId(null); setQuestionInitialId(null); setMatchChatOpen(false); setTab(next); setPage('root'); };
  const openLost = (id?: number) => { setLostInitialListingId(id ?? null); setTab('lost'); setPage('root'); };
  const openQuestions = () => { setQuestionInitialId(null); setSocialInitialTab('questions'); setTab('social'); setPage('root'); };
  const sessionChanged = (session: AuthResponse) => {
    void saveSession(session);
    setCurrentUser(session.username);
    setAuthToken(session.token);
    setAuthExpiresAt(session.expiresAt);
    setRefreshToken(session.refreshToken);
  };
  const authenticated = (session: AuthResponse) => { sessionChanged(session); setTab('home'); setPage('root'); };
  const logout = async () => {
    if (authToken) { try { await logoutSession(authToken); } catch { /* Local logout must still complete. */ } }
    await clearSession();
    setCurrentUser(null);
    setAuthToken(null);
    setAuthExpiresAt(null);
    setRefreshToken(null);
    setUnreadNotifications(0);
    setTab('home');
    setPage('root');
  };

  let screen;
  if (page === 'login') screen = <AuthScreen key="login" initialMode="login" onBack={() => setPage('root')} onAuthenticated={authenticated} />;
  else if (page === 'register') screen = <AuthScreen key="register" initialMode="register" onBack={() => setPage('root')} onAuthenticated={authenticated} />;
  else if (page === 'reset') screen = <AuthScreen key={`reset-${resetToken}`} initialMode="reset" resetToken={resetToken} onBack={() => setPage('login')} onAuthenticated={authenticated} />;
  else if (page === 'account' && currentUser && authToken) screen = <AccountScreen username={currentUser} token={authToken} onBack={() => setPage('root')} onOpenPets={() => setPage('pets')} onLogout={logout} onSessionChanged={sessionChanged} onUsernameChanged={setCurrentUser} onDeleted={logout} />;
  else if (page === 'pets' && authToken) screen = <PetsScreen token={authToken} onBack={() => setPage('account')} />;
  else if (page === 'nearby' && authToken) screen = <NearbyScreen token={authToken} category={nearbyCategory} onBack={() => setPage('root')} />;
  else if (page === 'nearby') screen = <AuthScreen key="nearby-login" initialMode="login" onBack={() => setPage('root')} onAuthenticated={authenticated} />;
  else if (page === 'adoption') screen = <AdoptionScreen token={authToken} initialListingId={adoptionInitialListingId} onLogin={() => setPage('login')} onBack={() => { setAdoptionInitialListingId(null); setPage('root'); }} />;
  else if (page === 'reviews') screen = <ReviewsScreen token={authToken} onLogin={() => setPage('login')} onBack={() => setPage('root')} />;
  else if (page === 'content') screen = <ContentScreen kind={contentKind} onBack={() => setPage('root')} onOpenLost={openLost} />;
  else if (page === 'notifications' && authToken) screen = <NotificationsScreen token={authToken} onBack={() => setPage('root')} onUnreadChanged={setUnreadNotifications} onOpenLost={openLost} onOpenAdoption={id => { setAdoptionInitialListingId(id ?? null); setTab('social'); setPage('adoption'); }} onOpenSocial={id => { setSocialInitialPostId(id ?? null); setSocialInitialTab('posts'); setTab('social'); setPage('root'); }} onOpenMatch={id => { setMatchInitialTargetPetId(id ?? null); setTab('match'); setPage('root'); }} onOpenQuestion={id => { setQuestionInitialId(id ?? null); setSocialInitialTab('questions'); setTab('social'); setPage('root'); }} />;
  else if (tab === 'home') screen = <HomeScreen currentUser={currentUser} unreadNotifications={unreadNotifications} onOpenNotifications={() => setPage(currentUser ? 'notifications' : 'login')} onOpenAccount={() => setPage('account')} onOpenLost={openLost} onOpenQuestions={openQuestions} onOpenContent={kind => { setContentKind(kind); setPage('content'); }} onLogin={() => setPage('login')} onRegister={() => setPage('register')} />;
  else if (tab === 'social') screen = <PatiSocialScreen
    initialTab={socialInitialTab}
    initialPostId={socialInitialPostId}
    initialQuestionId={questionInitialId}
    username={currentUser}
    authToken={authToken}
    onOpenAccount={() => setPage('account')}
    onLogin={() => setPage('login')}
    onOpenNearby={category => {
      setNearbyCategory(category);
      setPage('nearby');
    }}
    onOpenAdoption={() => { setAdoptionInitialListingId(null); setPage('adoption'); }}
    onOpenReviews={() => setPage('reviews')}
    onOpenLost={openLost}
  />;
  else if (tab === 'lost') screen = <LostPetsScreen token={authToken} username={currentUser} initialListingId={lostInitialListingId} onLogin={() => setPage('login')} />;
  else if (tab === 'match') screen = <PatiMatchScreen token={authToken} username={currentUser} initialTargetPetId={matchInitialTargetPetId} onLogin={() => setPage('login')} onOpenPets={() => setPage('pets')} onSessionExpired={() => { void logout(); setPage('login'); }} onChatStateChange={setMatchChatOpen} />;
  else if (tab === 'settings') screen = <SettingsScreen darkTheme={darkTheme} onThemeChange={value => { setThemeMode(value ? 'dark' : 'light'); setDarkTheme(value); }} username={currentUser} token={authToken} unreadNotifications={unreadNotifications} onOpenNotifications={() => setPage(currentUser ? 'notifications' : 'login')} onOpenAccount={() => setPage('account')} onLogin={() => setPage('login')} onLogout={logout} onOpenQuestions={openQuestions} onOpenPatiMatch={() => setTab('match')} />;
  else screen = <ComingSoon tab={tab} onHome={() => changeTab('home')} />;

  return (
    <View style={styles.app}>
      <StatusBar style={darkTheme || tab === 'home' ? 'light' : 'dark'} />
      {screen}
      {page !== 'login' && page !== 'register' && page !== 'reset' && !matchChatOpen ? <BottomTabs active={tab} onChange={changeTab} /> : null}
    </View>
  );
}

function HomeScreen({ currentUser, unreadNotifications, onOpenNotifications, onOpenAccount, onOpenLost, onOpenQuestions, onOpenContent, onLogin, onRegister }: { currentUser: string | null; unreadNotifications: number; onOpenNotifications: () => void; onOpenAccount: () => void; onOpenLost: () => void; onOpenQuestions: () => void; onOpenContent: (kind: ContentKind) => void; onLogin: () => void; onRegister: () => void }) {
  const [data, setData] = useState<HomePayload | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  const load = useCallback(async (refresh = false) => {
    refresh ? setRefreshing(true) : setLoading(true);
    setError(null);
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 12000);
    try {
      setData(await getHome(controller.signal));
    } catch (reason) {
      setError(reason instanceof Error && reason.name === 'AbortError'
        ? 'Bağlantı zaman aşımına uğradı.' : 'PetWork sunucusuna ulaşılamadı.');
    } finally {
      clearTimeout(timer);
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const results = useMemo(() => {
    const search = query.trim().toLocaleLowerCase('tr-TR');
    if (!search || !data) return [];
    return [
      ...data.featured.map(item => ({ kind: 'story' as const, item })),
      ...data.blogs.map(item => ({ kind: 'story' as const, item })),
      ...data.questions.map(item => ({ kind: 'question' as const, item })),
    ].filter(result => `${result.item.title} ${result.item.category}`.toLocaleLowerCase('tr-TR').includes(search))
      .filter((result, index, all) => all.findIndex(other => `${other.kind}-${other.item.id}` === `${result.kind}-${result.item.id}`) === index);
  }, [data, query]);

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={styles.scrollContent}
      showsVerticalScrollIndicator={false}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => load(true)} tintColor={colors.primary} />}
      keyboardShouldPersistTaps="handled"
    >
      <Hero currentUser={currentUser} unreadNotifications={unreadNotifications} query={query} setQuery={setQuery} onOpenNotifications={onOpenNotifications} onOpenAccount={onOpenAccount} onLogin={onLogin} onRegister={onRegister} />
      <View style={styles.content}>
        {query.trim() ? <SearchResults query={query} results={results} loading={loading} onOpenContent={onOpenContent} onOpenQuestions={onOpenQuestions} /> : (
          <>
            <SectionTitle title="Konular" />
            <View style={styles.categoryGrid}>{getCategories().map(category => <CategoryCard key={category.title} {...category} onPress={category.kind ? () => onOpenContent(category.kind!) : onOpenQuestions} />)}</View>
            <LostHomeBanner onPress={onOpenLost} />
            <SectionTitle title="Güncel İçerikler" action="Tümü" onAction={() => onOpenContent('all')} />
            {loading && !data ? <LoadingCards /> : null}
            {error && !data ? <ErrorState message={error} onRetry={() => load()} /> : null}
            {data?.featured.length ? (
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.horizontalList}>
                {data.featured.map(item => <StoryCard key={`${item.type}-${item.id}`} story={item} onPress={() => onOpenContent(storyKind(item.type))} />)}
              </ScrollView>
            ) : null}
            {data?.blogs[0] ? <BlogSpotlight story={data.blogs[0]} onPress={() => onOpenContent('blogs')} /> : null}
            {data?.questions[0] ? <QuestionCard question={data.questions[0]} onPress={onOpenQuestions} /> : null}
            <View style={styles.trustCard}>
              <View style={styles.trustIcon}><Ionicons name="shield-checkmark-outline" size={24} color="#4E7458" /></View>
              <View style={styles.trustCopy}>
                <Text style={styles.trustTitle}>Kaynağı açık, özenle seçilmiş içerikler</Text>
                <Text style={styles.trustText}>Sağlık bilgileri bilgilendirme amaçlıdır; gerektiğinde veteriner desteği alın.</Text>
              </View>
            </View>
            {data ? <Text style={styles.communityNote}>{data.memberCount} üye · {data.questionCount} gerçek soru</Text> : null}
          </>
        )}
      </View>
    </ScrollView>
  );
}

function Hero({ currentUser, unreadNotifications, query, setQuery, onOpenNotifications, onOpenAccount, onLogin, onRegister }: { currentUser: string | null; unreadNotifications: number; query: string; setQuery: (value: string) => void; onOpenNotifications: () => void; onOpenAccount: () => void; onLogin: () => void; onRegister: () => void }) {
  return (
    <LinearGradient colors={getThemeMode() === 'dark' ? ['#493044', '#352532', '#251C25'] : [colors.primaryDark, colors.primary, '#845B7C']} style={styles.hero}>
      <View style={styles.headerRow}>
        <View>
          <View style={styles.brandRow}><Ionicons name="paw" size={19} color={colors.peach} /><Text style={styles.brand}>Pet'im</Text></View>
          <Text style={styles.byline}>by PetWork</Text>
        </View>
        {currentUser ? <View style={styles.authRow}><Pressable onPress={onOpenNotifications} accessibilityRole="button" accessibilityLabel={`${unreadNotifications} okunmamış bildirim`} style={({ pressed }) => [styles.notificationButton, pressed && styles.pressed]}><Ionicons name="notifications-outline" size={21} color={colors.primary} />{unreadNotifications > 0 ? <View style={styles.notificationBadge}><Text style={styles.notificationBadgeText}>{Math.min(unreadNotifications, 99)}</Text></View> : null}</Pressable><Pressable onPress={onOpenAccount} accessibilityRole="button" accessibilityLabel={`${currentUser} hesap menüsünü aç`} style={({ pressed }) => [styles.userChip, pressed && styles.pressed]}><Ionicons name="person-circle-outline" size={21} color={colors.primary} /><Text style={styles.userChipText} numberOfLines={1}>{currentUser}</Text></Pressable></View> : <View style={styles.authRow}>
          <Pressable accessibilityRole="button" accessibilityLabel="Giriş yap"
            onPress={onLogin}
            style={({ pressed }) => [styles.loginButton, pressed && styles.pressed]}>
            <Text style={styles.loginText}>Giriş Yap</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Üye ol"
            onPress={onRegister}
            style={({ pressed }) => [styles.joinButton, pressed && styles.pressed]}>
            <Text style={styles.joinText}>Üye Ol</Text>
          </Pressable>
        </View>}
      </View>
      <View style={styles.searchBox}>
        <Ionicons name="search" size={18} color={colors.peach} />
        <TextInput value={query} onChangeText={setQuery} placeholder="Pati sağlığı, bakım, topluluk ara..."
          placeholderTextColor="#E2D5DF" style={styles.searchInput} returnKeyType="search" accessibilityLabel="PetWork içinde ara" />
        {query ? <Pressable onPress={() => setQuery('')} accessibilityLabel="Aramayı temizle" hitSlop={10}>
          <Ionicons name="close-circle" size={20} color="#E2D5DF" />
        </Pressable> : null}
      </View>
    </LinearGradient>
  );
}

function SectionTitle({ title, action, onAction }: { title: string; action?: string; onAction?: () => void }) {
  return <View style={styles.sectionHeader}><Text style={styles.sectionTitle}>{title}</Text>
    {action ? <Pressable onPress={onAction}><Text style={styles.sectionAction}>{action} →</Text></Pressable> : null}
  </View>;
}

function CategoryCard({ title, icon, color, ink, onPress }: HomeCategory & { onPress?: () => void }) {
  const dark = getThemeMode() === 'dark';
  return <Pressable accessibilityRole="button" accessibilityLabel={`${title} kategorisini aç`}
    onPress={onPress ?? (() => Alert.alert(title, 'Bu kategori ekranını sıradaki adımda bağlıyoruz.'))}
    style={({ pressed }) => [styles.categoryCard, { backgroundColor: color }, dark && styles.categoryCardDark, pressed && styles.cardPressed]}>
    <View style={[styles.categoryIcon, { backgroundColor: `${ink}${dark ? '20' : '14'}` }]}><Ionicons name={icon} size={27} color={ink} /></View>
    <Text style={[styles.categoryLabel, { color: ink }]} numberOfLines={2}>{title}</Text>
  </Pressable>;
}

function StoryCard({ story, onPress }: { story: Story; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.storyCard, pressed && styles.cardPressed]}>
    <ImageBackground source={{ uri: mediaUrl(story.imagePath) }} style={styles.storyImage} imageStyle={styles.storyImageRadius}>
      <View style={styles.storyBadge}><Text style={styles.storyBadgeText}>{story.category}</Text></View>
    </ImageBackground>
    <View style={styles.storyBody}><Text style={styles.storyTitle} numberOfLines={2}>{story.title}</Text>
      <Text style={styles.storySource}>Kaynak: PetWork bilgi merkezi</Text></View>
  </Pressable>;
}

function BlogSpotlight({ story, onPress }: { story: Story; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.blogSpotlight, pressed && styles.cardPressed]}>
    <Text style={styles.outlineBadge}>Blog</Text><Text style={styles.spotlightTitle}>{story.title}</Text>
    <Text style={styles.spotlightMeta}>PetWork topluluğu · 5 dk okuma</Text><Text style={styles.spotlightSource}>Kaynak: PetWork</Text>
  </Pressable>;
}

function QuestionCard({ question, onPress }: { question: Question; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.questionCard, pressed && styles.cardPressed]}>
    <View style={styles.questionIcon}><Ionicons name="help" size={25} color={colors.peach} /></View>
    <View style={styles.questionBody}><Text style={styles.questionTitle}>{question.title}</Text>
      <Text style={styles.questionMeta}>{question.answerCount} yanıt · {question.username}</Text><Text style={styles.questionLink}>Yanıtları gör →</Text></View>
  </Pressable>;
}

type SearchResult = { kind: 'story'; item: Story } | { kind: 'question'; item: Question };
function storyKind(type: string): ContentKind {
  if (type === 'disease') return 'diseases';
  if (type === 'recipe') return 'recipes';
  if (type === 'guide') return 'guides';
  return 'blogs';
}

function SearchResults({ query, results, loading, onOpenContent, onOpenQuestions }: { query: string; results: SearchResult[]; loading: boolean; onOpenContent: (kind: ContentKind) => void; onOpenQuestions: () => void }) {
  return <View><SectionTitle title={`“${query.trim()}” sonuçları`} />
    {loading ? <ActivityIndicator color={colors.primary} size="large" /> : null}
    {!loading && !results.length ? <Text style={styles.emptyText}>Bu aramayla eşleşen güncel içerik bulunamadı.</Text> : null}
    {results.map(result => result.kind === 'story'
      ? <View key={`s-${result.item.type}-${result.item.id}`} style={styles.searchResult}><StoryCard story={result.item} onPress={() => onOpenContent(storyKind(result.item.type))} /></View>
      : <View key={`q-${result.item.id}`} style={styles.searchResult}><QuestionCard question={result.item} onPress={onOpenQuestions} /></View>)}
  </View>;
}

function LoadingCards() {
  return <View style={styles.loading}><ActivityIndicator color={colors.primary} size="large" /><Text style={styles.loadingText}>Güncel içerikler getiriliyor…</Text></View>;
}

function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return <View style={styles.errorCard}><Ionicons name="cloud-offline-outline" size={28} color={colors.danger} />
    <View style={styles.errorCopy}><Text style={styles.errorTitle}>{message}</Text><Text style={styles.errorText}>API: {apiUrl}</Text></View>
    <Pressable onPress={onRetry} style={styles.retryButton}><Text style={styles.retryText}>Yenile</Text></Pressable>
  </View>;
}

function LostHomeBanner({ onPress }: { onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.lostHomeBanner, pressed && styles.cardPressed]}>
    <View style={styles.lostHomeIcon}><Ionicons name="location" size={26} color={colors.white} /></View>
    <View style={styles.flexOne}><Text style={styles.lostEyebrow}>KAYIP PATİLER</Text>
      <Text style={styles.lostHomeTitle}>Bir pati eve dönsün</Text><Text style={styles.lostHomeText}>Yakındaki ilanları gör veya hızlıca bildirim oluştur.</Text></View>
    <Ionicons name="chevron-forward" size={22} color={colors.primary} />
  </Pressable>;
}

function ScreenShell({ title, subtitle, onBack, children }: { title: string; subtitle?: string; onBack?: () => void; children: React.ReactNode }) {
  return <ScrollView style={styles.screen} contentContainerStyle={styles.subScreenContent} showsVerticalScrollIndicator={false} keyboardShouldPersistTaps="handled">
    <View style={styles.subHeader}>
      {onBack ? <Pressable onPress={onBack} style={styles.backButton} accessibilityLabel="Geri dön"><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable> : <View style={styles.headerMark}><Ionicons name="paw" size={20} color={colors.peach} /></View>}
      <View style={styles.flexOne}><Text style={styles.subTitle}>{title}</Text>{subtitle ? <Text style={styles.subSubtitle}>{subtitle}</Text> : null}</View>
    </View>
    {children}
  </ScrollView>;
}

function NearbyScreen({ token, category, onBack }: { token: string; category: 'veterinarian' | 'groomer' | 'hotel'; onBack: () => void }) {
  const isGroomer = category === 'groomer';
  const isHotel = category === 'hotel';
  const placeLabel = isHotel ? 'pet oteli' : isGroomer ? 'pet kuaförü' : 'veteriner';
  const placeLabelPlural = isHotel ? 'pet oteli' : isGroomer ? 'pet kuaförü' : 'veteriner';
  const placeTitle = isHotel ? 'Pet Otelleri' : isGroomer ? 'Pet Kuaförleri' : 'Veterinerler';
  const [locationText, setLocationText] = useState('Konum kullanılmadı');
  const [city, setCity] = useState('');
  const [district, setDistrict] = useState('');
  const [places, setPlaces] = useState<NearbyVeterinarian[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [privacyVisible, setPrivacyVisible] = useState(false);
  const [locationConsent, setLocationConsent] = useState(false);

  useEffect(() => {
    void SecureStore.getItemAsync(locationConsentKey).then(value => setLocationConsent(value === 'accepted'));
  }, []);

  const performLocationSearch = async () => {
    setLoading(true);
    setError(null);
    setLocationText('Konum izni kontrol ediliyor…');
    try {
      const servicesEnabled = await Location.hasServicesEnabledAsync();
      if (!servicesEnabled) {
        const message = 'Telefonunun konum servisi kapalı. Ayarlardan konumu açıp tekrar dene.';
        setLocationText('Konum servisi kapalı');
        setError(message);
        Alert.alert('Konum kapalı', message, [
          { text: 'Vazgeç', style: 'cancel' },
          { text: 'Ayarları aç', onPress: () => void Linking.openSettings() },
        ]);
        return;
      }

      let permission = await Location.getForegroundPermissionsAsync();
      if (!permission.granted) permission = await Location.requestForegroundPermissionsAsync();
      if (!permission.granted) {
        const message = 'Yakındaki veterinerleri gösterebilmemiz için konum izni vermelisin.';
        setLocationText('Konum izni verilmedi');
        setError(message);
        Alert.alert('Konum izni gerekli', message, [
          { text: 'Vazgeç', style: 'cancel' },
          { text: 'Ayarları aç', onPress: () => void Linking.openSettings() },
        ]);
        return;
      }

      setLocationText('Konum alınıyor…');
      const lastKnown = await Location.getLastKnownPositionAsync({ maxAge: 5 * 60 * 1000, requiredAccuracy: 1500 });
      const current = lastKnown ?? await Promise.race([
        Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }),
        new Promise<never>((_, reject) => setTimeout(
          () => reject(new Error('Konum alınması uzun sürdü. Açık bir alanda tekrar dene.')),
          15_000,
        )),
      ]);
      setLocationText(`Konum bulundu · ${placeLabelPlural} aranıyor…`);
      const results = isHotel
        ? await getNearbyPetHotels(token, current.coords.latitude, current.coords.longitude)
        : isGroomer
          ? await getNearbyGroomers(token, current.coords.latitude, current.coords.longitude)
          : await getNearbyVeterinarians(token, current.coords.latitude, current.coords.longitude);
      setPlaces(results);
      setLocationText(`Konum izni açık · ${results.length} ${placeLabel} bulundu`);
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : `Yakındaki ${placeLabelPlural} sonuçları alınamadı.`;
      setError(message);
      setLocationText('Konum kullanıldı ancak sonuçlar alınamadı');
      Alert.alert(`${placeTitle} alınamadı`, message);
    } finally {
      setLoading(false);
    }
  };
  const askLocation = () => {
    if (!locationConsent) {
      setPrivacyVisible(true);
      return;
    }
    void performLocationSearch();
  };
  const acceptLocationUse = async () => {
    await SecureStore.setItemAsync(locationConsentKey, 'accepted');
    setLocationConsent(true);
    setPrivacyVisible(false);
    await performLocationSearch();
  };
  const withdrawLocationUse = async () => {
    await SecureStore.deleteItemAsync(locationConsentKey);
    setLocationConsent(false);
    setPlaces([]);
    setLocationText('Konum kullanımı tercihi kaldırıldı');
    setPrivacyVisible(false);
    Alert.alert('Tercihin kaldırıldı', 'Cihaz iznini de kaldırmak istersen uygulama ayarlarından konum erişimini kapatabilirsin.', [
      { text: 'Tamam', style: 'cancel' },
      { text: 'Ayarları aç', onPress: () => void Linking.openSettings() },
    ]);
  };
  const applyManualLocation = async () => {
    const trimmedCity = city.trim();
    const trimmedDistrict = district.trim();
    if (!trimmedCity && !trimmedDistrict) {
      Alert.alert('Konum bilgisi gerekli', 'Şehir veya ilçe alanlarından en az birini yaz.');
      return;
    }
    setLoading(true);
    setError(null);
    setLocationText(`${[trimmedDistrict, trimmedCity].filter(Boolean).join(', ')} için ${placeLabelPlural} aranıyor…`);
    try {
      const results = isHotel
        ? await searchPetHotelsByArea(token, trimmedCity, trimmedDistrict)
        : isGroomer
          ? await searchGroomersByArea(token, trimmedCity, trimmedDistrict)
          : await searchVeterinariansByArea(token, trimmedCity, trimmedDistrict);
      setPlaces(results);
      setLocationText(`Manuel konum uygulandı · ${results.length} ${placeLabel} bulundu`);
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : `Bu konumdaki ${placeLabelPlural} sonuçları alınamadı.`;
      setError(message);
      setLocationText('Manuel konum uygulanamadı');
      Alert.alert(`${placeTitle} alınamadı`, message);
    } finally {
      setLoading(false);
    }
  };
  return <><ScreenShell title={placeTitle} subtitle="İhtiyacın olan yerleri güvenle bul" onBack={onBack}>
    <View style={styles.mapCard}><Ionicons name="map-outline" size={54} color={colors.primary} /><Text style={styles.mapTitle}>Harita ve liste görünümü</Text><Text style={styles.mapText}>{locationText}</Text>
      <ActionButton label={loading ? `${placeTitle} aranıyor…` : 'Konumumu kullan'} icon="navigate-outline" onPress={() => { if (!loading) askLocation(); }} />
      <Pressable onPress={() => setPrivacyVisible(true)} hitSlop={8}><Text style={styles.locationPrivacyLink}>Konum ve gizlilik bilgisi</Text></Pressable>
    </View>
    <Text style={styles.formLabel}>Manuel konum (isteğe bağlı)</Text><View style={styles.inlineFields}><FormInput value={city} onChangeText={setCity} placeholder="Şehir (isteğe bağlı)" /><FormInput value={district} onChangeText={setDistrict} placeholder="İlçe (isteğe bağlı)" /></View>
    <ActionButton label={loading ? 'Aranıyor…' : 'Uygula'} icon="search-outline" onPress={() => { if (!loading) void applyManualLocation(); }} />
    <SectionTitle title={`Yakındaki ${placeLabelPlural}`} />
    {loading ? <View style={styles.nearbyState}><ActivityIndicator color={colors.primary} /><Text style={styles.mapText}>Google Places sonuçları alınıyor…</Text></View> : null}
    {!loading && error ? <Pressable onPress={() => void askLocation()} style={styles.nearbyError}><Text style={styles.nearbyErrorText}>{error}</Text><Text style={styles.textAction}>Tekrar dene</Text></Pressable> : null}
    {!loading && !error && places.length === 0 ? <View style={styles.nearbyState}><Ionicons name="navigate-outline" size={28} color={colors.primary} /><Text style={styles.mapText}>Yakındaki {placeLabelPlural} sonuçlarını görmek için konumunu kullan.</Text></View> : null}
    {!loading && places.map(place => <PlaceCard key={place.id} place={place} kind={isHotel ? 'Pet oteli' : isGroomer ? 'Pet kuaförü' : 'Veteriner'} />)}
    {places.length > 0 ? <Text style={styles.googleAttribution}>Sonuçlar Google Maps Platform tarafından sağlanır.</Text> : null}
  </ScreenShell>
    <Modal visible={privacyVisible} transparent animationType="fade" onRequestClose={() => setPrivacyVisible(false)}>
      <View style={styles.privacyOverlay}>
        <View style={styles.privacyModal}>
          <View style={styles.privacyModalHeader}><View style={styles.privacyModalIcon}><Ionicons name="shield-checkmark-outline" size={24} color="#4E7458" /></View><Text style={styles.privacyModalTitle}>Konum bilgilendirmesi</Text></View>
          <Text style={styles.privacyModalText}>Yakındaki {placeLabelPlural} sonuçlarını göstermek için yalnızca sen istediğinde cihazının yaklaşık konumunu kullanırız.</Text>
          <View style={styles.privacyPoint}><Ionicons name="navigate-outline" size={18} color={colors.primary} /><Text style={styles.privacyPointText}>Koordinatın PetWork sunucusuna gönderilir ve yakındaki yerleri aramak için Google Maps Platform ile paylaşılır.</Text></View>
          <View style={styles.privacyPoint}><Ionicons name="server-outline" size={18} color={colors.primary} /><Text style={styles.privacyPointText}>Kesin konum veritabanımıza kaydedilmez, profilinde tutulmaz ve diğer kullanıcılara gösterilmez.</Text></View>
          <View style={styles.privacyPoint}><Ionicons name="globe-outline" size={18} color={colors.primary} /><Text style={styles.privacyPointText}>Google hizmetleri nedeniyle veri yurt dışında işlenebilir. Konum kullanmak zorunlu değildir; şehir veya ilçeyle manuel arama yapabilirsin.</Text></View>
          <Text style={styles.privacyModalFoot}>İznini cihaz ayarlarından, bu tercihi ise buradaki gizlilik ekranından istediğin zaman kaldırabilirsin.</Text>
          {locationConsent ? <Pressable onPress={() => void withdrawLocationUse()} style={styles.withdrawButton}><Text style={styles.withdrawButtonText}>Konum tercihini kaldır</Text></Pressable> : <Pressable onPress={() => void acceptLocationUse()} style={styles.acceptPrivacyButton}><Ionicons name="checkmark-circle-outline" size={19} color={colors.white} /><Text style={styles.acceptPrivacyText}>İzin ver ve konumla devam et</Text></Pressable>}
          <Pressable onPress={() => setPrivacyVisible(false)} style={styles.manualPrivacyButton}><Text style={styles.manualPrivacyText}>{locationConsent ? 'Kapat' : 'Manuel konum kullan'}</Text></Pressable>
        </View>
      </View>
    </Modal>
  </>;
}

function PlaceCard({ place, kind }: { place: NearbyVeterinarian; kind: string }) {
  const distance = place.distanceMeters === null || place.distanceMeters === undefined
    ? 'Mesafe bilgisi yok'
    : place.distanceMeters < 1000 ? `${place.distanceMeters} m` : `${(place.distanceMeters / 1000).toFixed(1).replace('.', ',')} km`;
  const rating = place.rating ? ` · ★ ${place.rating.toFixed(1).replace('.', ',')}${place.userRatingCount ? ` (${place.userRatingCount})` : ''}` : '';
  const openMaps = () => {
    const url = place.googleMapsUri || `https://www.google.com/maps/search/?api=1&query=${place.latitude},${place.longitude}`;
    void openExternalUrl(url, 'Harita bağlantısı şu anda açılamıyor.');
  };
  return <View style={styles.placeCard}><View style={styles.placePin}><Ionicons name="location" size={22} color={colors.primary} /></View><View style={styles.flexOne}>
    <Text style={styles.placeTitle}>{place.name}</Text><Text style={styles.placeMeta}>{kind} · {distance}{rating}</Text>
    {place.address ? <Text style={styles.placeAddress}>{place.address}</Text> : null}
    {place.openNow !== null && place.openNow !== undefined ? <Text style={[styles.openText, !place.openNow && styles.closedText]}>{place.openNow ? 'Şimdi açık' : 'Şu anda kapalı'}</Text> : null}
    <View style={styles.miniActions}><Pressable onPress={openMaps}><Text style={styles.textAction}>Yol tarifi</Text></Pressable><Pressable onPress={openMaps}><Text style={styles.textAction}>Google Maps'te gör</Text></Pressable></View></View></View>;
}

function FormInput({ value, onChangeText, placeholder, multiline = false }: { value: string; onChangeText: (text: string) => void; placeholder: string; multiline?: boolean }) {
  return <TextInput value={value} onChangeText={onChangeText} placeholder={placeholder} placeholderTextColor="#958991" multiline={multiline} style={[styles.formInput, multiline && styles.formInputMultiline]} />;
}

function ActionButton({ label, icon, onPress }: { label: string; icon: keyof typeof Ionicons.glyphMap; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.fullButton, pressed && styles.pressed]}><Ionicons name={icon} size={19} color={colors.white} /><Text style={styles.fullButtonText}>{label}</Text></Pressable>;
}

async function openExternalUrl(url: string, message: string) {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') throw new Error('unsupported');
    if (!await Linking.canOpenURL(url)) throw new Error('unsupported');
    await Linking.openURL(url);
  } catch {
    Alert.alert('Bağlantı açılamadı', message);
  }
}

function SmallAction({ icon, label, onPress }: { icon: keyof typeof Ionicons.glyphMap; label: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.smallAction, pressed && styles.cardPressed]}><Ionicons name={icon} size={24} color={colors.primary} /><Text style={styles.smallActionText}>{label}</Text></Pressable>;
}

function FeatureRow({ icon, title, text, color, onPress }: { icon: keyof typeof Ionicons.glyphMap; title: string; text: string; color: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.featureRow, { backgroundColor: color }, pressed && styles.cardPressed]}><View style={styles.featureIcon}><Ionicons name={icon} size={25} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.featureTitle}>{title}</Text><Text style={styles.featureText}>{text}</Text></View><Ionicons name="chevron-forward" size={20} color={colors.primary} /></Pressable>;
}

function MiniCommunityCard({ icon, title, meta }: { icon: keyof typeof Ionicons.glyphMap; title: string; meta: string }) {
  return <View style={styles.miniCard}><Ionicons name={icon} size={25} color={colors.primary} /><View style={styles.flexOne}><Text style={styles.miniTitle}>{title}</Text><Text style={styles.miniMeta}>{meta}</Text></View></View>;
}

function InfoLine({ icon, text }: { icon: keyof typeof Ionicons.glyphMap; text: string }) {
  return <View style={styles.infoLine}><Ionicons name={icon} size={18} color="#4E7458" /><Text style={styles.infoText}>{text}</Text></View>;
}

function ComingSoon({ tab, onHome }: { tab: 'match'; onHome: () => void }) {
  const current = tabs.find(item => item.key === tab)!;
  return <View style={styles.comingSoon}><View style={styles.comingIcon}><Ionicons name={current.active} size={42} color={colors.primary} /></View>
    <Text style={styles.comingTitle}>{current.label}</Text><Text style={styles.comingText}>Bu alan evcil hayvan paneliyle birlikte şekillenecek. Ana sayfanın çalışan temeli hazır.</Text>
    <Pressable onPress={onHome} style={styles.primaryButton}><Text style={styles.primaryButtonText}>Ana sayfaya dön</Text></Pressable>
  </View>;
}

function BottomTabs({ active, onChange }: { active: TabKey; onChange: (tab: TabKey) => void }) {
  return <View style={styles.bottomTabs}>{tabs.map(tab => {
    const selected = active === tab.key;
    return <Pressable key={tab.key} accessibilityRole="tab" accessibilityState={{ selected }} onPress={() => onChange(tab.key)} style={styles.tabButton}>
      <View style={selected ? styles.activeTabIcon : undefined}><Ionicons name={selected ? tab.active : tab.icon} size={22} color={selected ? colors.primary : '#A89DA3'} /></View>
      <Text style={[styles.tabLabel, selected && styles.tabLabelActive]}>{tab.label}</Text>
    </Pressable>;
  })}</View>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const statusInset = Platform.OS === 'android' ? NativeStatusBar.currentHeight ?? 24 : 48;

const styles = createThemedStyles(() => ({
  app: { flex: 1, backgroundColor: colors.background }, screen: { flex: 1, backgroundColor: colors.background }, scrollContent: { paddingBottom: 108 },
  splashView: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background }, splashArtwork: { width: '100%', height: '100%' },
  hero: { paddingTop: statusInset + 14, paddingHorizontal: 22, paddingBottom: 24, borderBottomLeftRadius: 24, borderBottomRightRadius: 24 },
  headerRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 }, brandRow: { flexDirection: 'row', alignItems: 'center', gap: 7 },
  brand: { color: colors.white, fontFamily: serif, fontSize: 29, fontWeight: '700', letterSpacing: -0.6 }, byline: { color: '#DDCED9', fontSize: 11, marginTop: 1, marginLeft: 27, letterSpacing: 0.3 },
  authRow: { flexDirection: 'row', gap: 8 }, loginButton: { minHeight: 42, paddingHorizontal: 16, borderRadius: 22, backgroundColor: colors.card, justifyContent: 'center' },
  notificationButton: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center' }, notificationBadge: { position: 'absolute', right: -2, top: -3, minWidth: 18, height: 18, borderRadius: 9, paddingHorizontal: 4, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.peach }, notificationBadgeText: { color: colors.primaryDark, fontSize: 9, fontWeight: '900' },
  userChip: { maxWidth: 150, minHeight: 42, flexDirection: 'row', alignItems: 'center', gap: 7, backgroundColor: colors.card, paddingHorizontal: 13, borderRadius: 22 }, userChipText: { flexShrink: 1, color: colors.primaryDark, fontSize: 12, fontWeight: '800' },
  loginText: { color: colors.primaryDark, fontWeight: '700', fontSize: 13 }, joinButton: { minHeight: 42, paddingHorizontal: 15, borderRadius: 22, borderWidth: 1.2, borderColor: '#F6EAF2', justifyContent: 'center' },
  joinText: { color: colors.white, fontWeight: '700', fontSize: 13 }, pressed: { opacity: 0.78, transform: [{ scale: 0.98 }] },
  searchBox: { minHeight: 48, marginTop: 22, borderWidth: 1, borderColor: '#A987A0', borderRadius: 25, backgroundColor: '#FFFFFF16', flexDirection: 'row', alignItems: 'center', paddingHorizontal: 17, gap: 10 },
  searchInput: { flex: 1, color: colors.white, fontSize: 14, paddingVertical: 10 }, content: { width: '100%', maxWidth: 760, alignSelf: 'center', paddingHorizontal: 20 },
  sectionHeader: { marginTop: 27, marginBottom: 14, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, sectionTitle: { color: colors.text, fontFamily: serif, fontWeight: '700', fontSize: 23, letterSpacing: -0.4 },
  sectionAction: { color: colors.primary, fontWeight: '700', fontSize: 13, paddingVertical: 8 }, categoryGrid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 12 },
  categoryCard: { width: '31.5%', aspectRatio: 1.03, borderRadius: 19, alignItems: 'center', justifyContent: 'center', padding: 8, ...shadow }, categoryCardDark: { borderWidth: 1, borderColor: '#51414A' }, categoryIcon: { width: 43, height: 43, borderRadius: 22, alignItems: 'center', justifyContent: 'center', marginBottom: 9 },
  categoryLabel: { textAlign: 'center', fontWeight: '700', fontSize: 12, lineHeight: 16 }, cardPressed: { opacity: 0.82, transform: [{ scale: 0.985 }] }, horizontalList: { gap: 13, paddingRight: 20, paddingBottom: 6 },
  storyCard: { width: 286, minHeight: 228, backgroundColor: colors.card, borderRadius: 20, overflow: 'hidden', ...shadow }, storyImage: { height: 145, padding: 12, justifyContent: 'flex-start', alignItems: 'flex-start', backgroundColor: colors.sageSoft },
  storyImageRadius: { borderTopLeftRadius: 20, borderTopRightRadius: 20 }, storyBadge: { backgroundColor: '#FFFCF8E8', paddingVertical: 5, paddingHorizontal: 10, borderRadius: 14 }, storyBadgeText: { color: '#4E7458', fontWeight: '700', fontSize: 11 },
  storyBody: { padding: 14 }, storyTitle: { color: colors.text, fontWeight: '800', fontSize: 15, lineHeight: 20 }, storySource: { color: colors.muted, fontSize: 10, marginTop: 8 },
  blogSpotlight: { backgroundColor: colors.card, borderWidth: 1, borderColor: colors.yellow, borderRadius: 22, padding: 20, marginTop: 26, ...shadow },
  outlineBadge: { color: '#8A6515', borderWidth: 1, borderColor: colors.yellow, alignSelf: 'flex-start', borderRadius: 14, paddingVertical: 4, paddingHorizontal: 10, overflow: 'hidden', fontWeight: '700', fontSize: 11 },
  spotlightTitle: { color: colors.text, fontFamily: serif, fontSize: 21, lineHeight: 27, fontWeight: '700', marginTop: 13 }, spotlightMeta: { color: colors.muted, fontSize: 12, marginTop: 9 }, spotlightSource: { color: colors.muted, fontSize: 10, marginTop: 12 },
  questionCard: { flexDirection: 'row', backgroundColor: colors.lilacSoft, borderWidth: 1, borderColor: '#D8C1E8', borderRadius: 22, padding: 17, marginTop: 14, gap: 13 }, questionIcon: { width: 48, height: 48, borderRadius: 24, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  questionBody: { flex: 1 }, questionTitle: { color: colors.text, fontWeight: '800', fontSize: 15, lineHeight: 20 }, questionMeta: { color: colors.muted, fontSize: 11, marginTop: 5 }, questionLink: { color: colors.primary, fontWeight: '700', fontSize: 12, marginTop: 13 },
  trustCard: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.sageSoft, borderRadius: 20, padding: 17, gap: 13, marginTop: 18 }, trustIcon: { width: 44, height: 44, borderRadius: 22, backgroundColor: '#D5E3D2', alignItems: 'center', justifyContent: 'center' },
  trustCopy: { flex: 1 }, trustTitle: { color: colors.text, fontWeight: '800', fontSize: 14 }, trustText: { color: '#56635A', fontSize: 11, lineHeight: 16, marginTop: 4 }, communityNote: { textAlign: 'center', color: colors.muted, fontSize: 11, marginTop: 18 },
  loading: { height: 180, alignItems: 'center', justifyContent: 'center', gap: 12 }, loadingText: { color: colors.muted, fontSize: 13 }, errorCard: { backgroundColor: colors.peachSoft, borderRadius: 18, padding: 16, flexDirection: 'row', alignItems: 'center', gap: 12 },
  errorCopy: { flex: 1 }, errorTitle: { color: colors.text, fontWeight: '700', fontSize: 13 }, errorText: { color: colors.muted, fontSize: 10, marginTop: 4 }, retryButton: { backgroundColor: colors.card, paddingVertical: 9, paddingHorizontal: 12, borderRadius: 14 },
  retryText: { color: colors.primary, fontWeight: '800', fontSize: 12 }, emptyText: { color: colors.muted, backgroundColor: colors.card, padding: 20, borderRadius: 16, lineHeight: 21 }, searchResult: { marginBottom: 14 },
  bottomTabs: { position: 'absolute', left: 0, right: 0, bottom: 0, minHeight: 82, paddingBottom: Platform.OS === 'ios' ? 20 : 10, paddingTop: 10, paddingHorizontal: 9, flexDirection: 'row', backgroundColor: colors.white, borderTopWidth: 1, borderTopColor: colors.border, ...shadow },
  tabButton: { flex: 1, alignItems: 'center', justifyContent: 'center', minHeight: 52, gap: 4 }, activeTabIcon: { backgroundColor: colors.lilacSoft, paddingHorizontal: 14, paddingVertical: 4, borderRadius: 14 }, tabLabel: { color: '#958991', fontSize: 10, fontWeight: '600' }, tabLabelActive: { color: colors.primary, fontWeight: '800' },
  comingSoon: { flex: 1, backgroundColor: colors.background, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 36, paddingBottom: 70 }, comingIcon: { width: 88, height: 88, borderRadius: 44, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  comingTitle: { fontFamily: serif, fontSize: 30, fontWeight: '700', color: colors.text, marginTop: 22 }, comingText: { color: colors.muted, fontSize: 15, lineHeight: 22, textAlign: 'center', marginTop: 10 }, primaryButton: { backgroundColor: colors.primary, borderRadius: 20, paddingHorizontal: 24, paddingVertical: 14, marginTop: 24 }, primaryButtonText: { color: colors.white, fontWeight: '800' },
  flexOne: { flex: 1 }, rowBetween: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10 }, stack: { gap: 12, marginTop: 18 },
  lostHomeBanner: { flexDirection: 'row', alignItems: 'center', gap: 13, marginTop: 18, padding: 17, backgroundColor: getThemeMode() === 'dark' ? '#302326' : colors.peachSoft, borderWidth: 1, borderColor: getThemeMode() === 'dark' ? '#A76B60' : colors.peach, borderRadius: 22, ...shadow },
  lostHomeIcon: { width: 48, height: 48, borderRadius: 24, backgroundColor: getThemeMode() === 'dark' ? '#6D4A65' : colors.primary, alignItems: 'center', justifyContent: 'center' }, lostEyebrow: { color: getThemeMode() === 'dark' ? '#F0A18F' : '#9B463B', fontSize: 10, fontWeight: '900', letterSpacing: 1 },
  lostHomeTitle: { color: colors.text, fontFamily: serif, fontSize: 18, fontWeight: '700', marginTop: 3 }, lostHomeText: { color: colors.muted, fontSize: 11, lineHeight: 16, marginTop: 3 },
  subScreenContent: { paddingTop: statusInset + 8, paddingHorizontal: 20, paddingBottom: 116, width: '100%', maxWidth: 760, alignSelf: 'center' }, subHeader: { minHeight: 62, flexDirection: 'row', alignItems: 'center', gap: 12 },
  backButton: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, headerMark: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  subTitle: { color: colors.text, fontFamily: serif, fontSize: 27, fontWeight: '700' }, subSubtitle: { color: colors.muted, fontSize: 11, marginTop: 2 },
  segmented: { flexDirection: 'row', marginTop: 17, padding: 4, backgroundColor: '#EFE8E9', borderRadius: 18 }, segmentButton: { flex: 1, minHeight: 42, borderRadius: 15, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 4 },
  segmentActive: { backgroundColor: colors.card, ...shadow }, segmentText: { color: colors.muted, fontSize: 11, fontWeight: '700' }, segmentTextActive: { color: colors.primary },
  cardSectionTitle: { color: colors.text, fontFamily: serif, fontSize: 23, fontWeight: '700' }, cardSectionHint: { color: colors.muted, fontSize: 11, marginTop: 4 },
  nearbyGrid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 12 }, nearbyShortcut: { width: '48.4%', minHeight: 126, padding: 16, borderRadius: 20, justifyContent: 'space-between', ...shadow },
  nearbyTitle: { color: colors.text, fontWeight: '800', fontSize: 14, lineHeight: 18, marginTop: 13 }, nearbyCount: { color: colors.muted, fontSize: 10, marginTop: 3 }, privacyNote: { color: colors.muted, textAlign: 'center', fontSize: 10, marginTop: 14 },
  adoptionHero: { borderRadius: 24, overflow: 'hidden', marginTop: 20, ...shadow }, adoptionImage: { height: 245, justifyContent: 'flex-end' }, adoptionImageRadius: { borderRadius: 24 }, imageShade: { minHeight: 150, justifyContent: 'flex-end', padding: 20 },
  lightBadge: { color: colors.primary, backgroundColor: '#FFFCF8E8', alignSelf: 'flex-start', paddingVertical: 5, paddingHorizontal: 10, borderRadius: 13, overflow: 'hidden', fontSize: 10, fontWeight: '800' }, adoptionTitle: { color: colors.white, fontFamily: serif, fontSize: 27, fontWeight: '700', marginTop: 9 }, adoptionText: { color: '#F9EEF4', fontSize: 12, lineHeight: 17, marginTop: 3, maxWidth: 270 },
  featureRow: { minHeight: 88, flexDirection: 'row', alignItems: 'center', gap: 12, borderRadius: 20, padding: 15, marginTop: 14 }, featureIcon: { width: 46, height: 46, borderRadius: 23, backgroundColor: '#FFFCF8AA', alignItems: 'center', justifyContent: 'center' }, featureTitle: { color: colors.text, fontSize: 15, fontWeight: '800' }, featureText: { color: colors.muted, fontSize: 11, lineHeight: 16, marginTop: 3 },
  miniCard: { flexDirection: 'row', alignItems: 'center', gap: 13, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 18, padding: 16 }, miniTitle: { color: colors.text, fontSize: 14, fontWeight: '800' }, miniMeta: { color: colors.muted, fontSize: 10, marginTop: 5 },
  mapCard: { alignItems: 'center', backgroundColor: colors.sageSoft, borderRadius: 24, padding: 22, marginTop: 18 }, mapTitle: { color: colors.text, fontFamily: serif, fontSize: 21, fontWeight: '700', marginTop: 8 }, mapText: { color: colors.muted, textAlign: 'center', fontSize: 11, marginTop: 5 }, locationPrivacyLink: { color: colors.primary, fontSize: 10, fontWeight: '800', textDecorationLine: 'underline', marginTop: 13 }, nearbyState: { minHeight: 110, alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: colors.card, borderRadius: 20, padding: 18, marginBottom: 12 }, nearbyError: { alignItems: 'center', gap: 8, backgroundColor: colors.peachSoft, borderRadius: 18, padding: 15, marginBottom: 12 }, nearbyErrorText: { color: '#8D4339', textAlign: 'center', fontSize: 11, lineHeight: 16 }, googleAttribution: { color: colors.muted, textAlign: 'center', fontSize: 9, marginTop: 4, marginBottom: 10 },
  privacyOverlay: { flex: 1, justifyContent: 'center', backgroundColor: '#251E22AA', padding: 20 }, privacyModal: { width: '100%', maxWidth: 560, alignSelf: 'center', backgroundColor: colors.card, borderRadius: 25, padding: 21, ...shadow }, privacyModalHeader: { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 13 }, privacyModalIcon: { width: 43, height: 43, borderRadius: 22, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.sageSoft }, privacyModalTitle: { flex: 1, color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' }, privacyModalText: { color: colors.text, fontSize: 12, lineHeight: 19, marginBottom: 13 }, privacyPoint: { flexDirection: 'row', alignItems: 'flex-start', gap: 9, marginTop: 10 }, privacyPointText: { flex: 1, color: colors.muted, fontSize: 11, lineHeight: 17 }, privacyModalFoot: { color: colors.muted, fontSize: 9, lineHeight: 14, marginTop: 15 }, acceptPrivacyButton: { minHeight: 50, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: colors.primary, borderRadius: 17, marginTop: 17 }, acceptPrivacyText: { color: colors.white, fontSize: 12, fontWeight: '900' }, withdrawButton: { minHeight: 48, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.peachSoft, borderRadius: 17, marginTop: 17 }, withdrawButtonText: { color: '#8D4339', fontSize: 12, fontWeight: '900' }, manualPrivacyButton: { minHeight: 44, alignItems: 'center', justifyContent: 'center', marginTop: 5 }, manualPrivacyText: { color: colors.primary, fontSize: 11, fontWeight: '800' },
  formLabel: { color: colors.text, fontWeight: '800', fontSize: 13, marginTop: 20, marginBottom: 8 }, inlineFields: { flexDirection: 'row', gap: 10 },
  placeCard: { flexDirection: 'row', alignItems: 'flex-start', gap: 12, backgroundColor: colors.card, borderRadius: 20, padding: 16, marginBottom: 12, ...shadow }, placePin: { width: 43, height: 43, borderRadius: 22, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  placeTitle: { color: colors.text, fontSize: 14, fontWeight: '800' }, placeMeta: { color: colors.muted, fontSize: 11, marginTop: 4 }, placeAddress: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 5 }, openText: { color: '#41604A', fontSize: 10, fontWeight: '800', marginTop: 6 }, closedText: { color: '#9B463B' }, miniActions: { flexDirection: 'row', flexWrap: 'wrap', gap: 18, marginTop: 11 }, textAction: { color: colors.primary, fontSize: 12, fontWeight: '800' }, reportAction: { color: '#9B463B', fontSize: 12, fontWeight: '800' },
  petProfileCard: { backgroundColor: colors.card, borderRadius: 24, overflow: 'hidden', marginTop: 18, ...shadow }, petPhoto: { height: 265, padding: 14, alignItems: 'flex-start' }, petPhotoRadius: { borderTopLeftRadius: 24, borderTopRightRadius: 24 }, safeBadge: { color: '#41604A', backgroundColor: '#E5F0E2EE', paddingHorizontal: 10, paddingVertical: 6, borderRadius: 14, overflow: 'hidden', fontSize: 10, fontWeight: '800' },
  petProfileBody: { padding: 20 }, petName: { color: colors.text, fontFamily: serif, fontSize: 27, fontWeight: '700' }, cityBadge: { color: colors.primary, backgroundColor: colors.lilacSoft, borderRadius: 13, paddingVertical: 5, paddingHorizontal: 10, overflow: 'hidden', fontSize: 10, fontWeight: '800' }, petMeta: { color: colors.muted, fontSize: 12, marginTop: 4, marginBottom: 13 }, storyHeading: { color: colors.text, fontSize: 15, fontWeight: '800', marginTop: 14 }, bodyText: { color: colors.muted, fontSize: 12, lineHeight: 19, marginTop: 6 },
  infoLine: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 9 }, infoText: { color: '#56635A', flex: 1, fontSize: 11 }, safetyActions: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 18, paddingTop: 14, borderTopWidth: 1, borderTopColor: colors.border }, noticeCard: { flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: colors.sageSoft, borderRadius: 17, padding: 15, marginTop: 14 }, noticeText: { flex: 1, color: '#4B5B50', fontSize: 11, lineHeight: 16 },
  fullButton: { minHeight: 50, flexDirection: 'row', gap: 8, backgroundColor: colors.primary, borderRadius: 18, alignItems: 'center', justifyContent: 'center', marginTop: 18, paddingHorizontal: 17 }, fullButtonText: { color: colors.white, fontSize: 13, fontWeight: '800' },
  ratingCard: { flexDirection: 'row', alignItems: 'center', gap: 15, backgroundColor: colors.card, borderRadius: 22, padding: 17, marginTop: 18, ...shadow }, packageMock: { width: 82, height: 105, borderRadius: 16, backgroundColor: colors.yellowSoft, alignItems: 'center', justifyContent: 'center' }, packageText: { color: '#72530D', fontSize: 10, fontWeight: '900', marginTop: 5 }, labelRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 5 },
  verifiedBadge: { color: '#41604A', backgroundColor: colors.sageSoft, borderRadius: 10, paddingVertical: 4, paddingHorizontal: 7, overflow: 'hidden', fontSize: 8, fontWeight: '900' }, sponsoredBadge: { color: '#72530D', backgroundColor: colors.yellowSoft, borderRadius: 10, paddingVertical: 4, paddingHorizontal: 7, overflow: 'hidden', fontSize: 8, fontWeight: '900' }, productTitle: { color: colors.text, fontSize: 14, fontWeight: '800', marginTop: 8 }, productMeta: { color: colors.muted, fontSize: 10, marginTop: 3 }, ratingBig: { color: '#72530D', fontSize: 18, fontWeight: '900', marginTop: 7 },
  scoreGrid: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 13 }, scoreItem: { width: '23.5%', backgroundColor: colors.card, borderRadius: 15, paddingVertical: 12, alignItems: 'center', borderWidth: 1, borderColor: colors.border }, score: { color: colors.primary, fontSize: 16, fontWeight: '900' }, scoreLabel: { color: colors.muted, fontSize: 8, marginTop: 3, textAlign: 'center' }, comment: { color: colors.text, backgroundColor: colors.lilacSoft, borderRadius: 18, padding: 17, fontSize: 12, fontStyle: 'italic', lineHeight: 18, marginTop: 14 },
  formCard: { backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 22, padding: 17, marginTop: 18 }, formTitle: { color: colors.text, fontFamily: serif, fontSize: 21, fontWeight: '700', marginBottom: 8 }, photoPicker: { minHeight: 104, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft, borderRadius: 17, borderWidth: 1, borderStyle: 'dashed', borderColor: '#CBB8D1', marginBottom: 12 }, photoPickerText: { color: colors.primary, fontSize: 12, fontWeight: '800', marginTop: 6 }, formInput: { flex: 1, minHeight: 49, color: colors.text, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border, borderRadius: 15, paddingHorizontal: 14, paddingVertical: 11, fontSize: 12, marginTop: 10 }, formInputMultiline: { minHeight: 86, textAlignVertical: 'top' }, formHint: { color: colors.muted, fontSize: 10, lineHeight: 15, textAlign: 'center', marginTop: 10 },
  urgentPanel: { flexDirection: 'row', alignItems: 'center', gap: 13, backgroundColor: colors.peachSoft, borderRadius: 22, padding: 17, marginTop: 17, borderWidth: 1, borderColor: colors.peach }, urgentIcon: { width: 50, height: 50, borderRadius: 25, backgroundColor: '#B75B4D', alignItems: 'center', justifyContent: 'center' }, urgentTitle: { color: colors.text, fontSize: 15, fontWeight: '900' }, urgentText: { color: colors.muted, fontSize: 11, lineHeight: 16, marginTop: 4 },
  lostActions: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 10, marginTop: 15 }, smallAction: { width: '48.5%', minHeight: 85, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 18, padding: 14, justifyContent: 'space-between' }, smallActionText: { color: colors.text, fontSize: 11, lineHeight: 15, fontWeight: '800', marginTop: 9 },
  lostPetCard: { flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: colors.card, borderRadius: 20, padding: 10, marginBottom: 12, borderWidth: 1, borderColor: colors.border }, lostPetUrgent: { borderColor: colors.peach, borderWidth: 2, backgroundColor: '#FFF8F4' }, lostPetImage: { width: 76, height: 84 }, lostPetImageRadius: { borderRadius: 14 }, lostPetBody: { flex: 1 }, lostPetName: { color: colors.text, fontFamily: serif, fontSize: 19, fontWeight: '700' }, statusBadge: { borderRadius: 11, paddingVertical: 4, paddingHorizontal: 7, overflow: 'hidden', fontSize: 8, fontWeight: '900' }, lostPetMeta: { color: colors.muted, fontSize: 10, marginTop: 7 }, lostPetDate: { color: colors.muted, fontSize: 9, marginTop: 4 }, urgentNote: { color: '#9B463B', fontSize: 9, fontWeight: '800', marginTop: 5 },
  detailHero: { height: 280, padding: 15, alignItems: 'flex-start', marginTop: 18 }, detailHeroRadius: { borderRadius: 24 }, detailCard: { backgroundColor: colors.card, borderRadius: 22, padding: 19, marginTop: 14, ...shadow }, detailTitle: { color: colors.text, fontFamily: serif, fontSize: 26, fontWeight: '700' }, timeline: { backgroundColor: colors.background, borderRadius: 16, padding: 14, marginTop: 16 }, timelineTitle: { color: colors.text, fontSize: 13, fontWeight: '800' }, timelineItem: { color: colors.primary, fontSize: 11, marginTop: 10 }, timelinePrivate: { color: colors.muted, fontSize: 9, lineHeight: 14, marginTop: 8 },
  sampleNotice: { alignSelf: 'flex-start', flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: '#EEE4F2', borderWidth: 1, borderColor: '#D8C1E8', borderRadius: 13, paddingVertical: 6, paddingHorizontal: 10, marginTop: 12, marginBottom: 10 },
  sampleNoticeText: { color: colors.primary, fontSize: 9, fontWeight: '800' },
}));
