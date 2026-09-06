import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import * as Location from 'expo-location';
import * as SecureStore from 'expo-secure-store';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator, Alert, Image, ImageBackground, Platform, Pressable, RefreshControl, ScrollView,
  StatusBar as NativeStatusBar, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { apiUrl, getHome, HomePayload, mediaUrl, Question, Story } from './src/api';
import { AuthScreen } from './src/screens/AuthScreen';
import { AccountScreen } from './src/screens/AccountScreen';
import { PatiSocialScreen } from './src/screens/PatiSocialScreen';
import { colors, shadow } from './src/theme';

type TabKey = 'home' | 'social' | 'lost' | 'match' | 'settings';
type PageKey = 'root' | 'login' | 'register' | 'account' | 'nearby' | 'adoption' | 'reviews' | 'lost-form' | 'found-form' | 'lost-detail' | 'sighting';

const communityDemoImage = require('./assets/community-demo.png');

const categories = [
  { title: 'Soru-Cevap', icon: 'help-circle-outline' as const, color: colors.lilacSoft, ink: colors.primary },
  { title: 'Bakım Rehberleri', icon: 'book-outline' as const, color: colors.sageSoft, ink: '#4E7458' },
  { title: 'Hastalıklar', icon: 'medkit-outline' as const, color: colors.peachSoft, ink: '#A65345' },
  { title: 'Tarifler', icon: 'restaurant-outline' as const, color: colors.yellowSoft, ink: '#8A6515' },
  { title: 'Blog', icon: 'create-outline' as const, color: colors.lilacSoft, ink: colors.primary },
  { title: 'Yas ve Kayıp', icon: 'heart-outline' as const, color: colors.sageSoft, ink: '#4E7458' },
];

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
  const [currentUser, setCurrentUser] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setShowSplash(false), 1600);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    SecureStore.getItemAsync('petim.session').then(value => {
      if (!value) return;
      try {
        const session = JSON.parse(value) as { username?: string; expiresAt?: string };
        if (session.username && session.expiresAt && new Date(session.expiresAt) > new Date()) setCurrentUser(session.username);
        else SecureStore.deleteItemAsync('petim.session');
      } catch { SecureStore.deleteItemAsync('petim.session'); }
    });
  }, []);

  if (showSplash) {
    return <View style={styles.splashView}>
      <StatusBar style="dark" />
      <Image source={require('./assets/splash-petim.png')} style={styles.splashArtwork} resizeMode="contain" accessibilityLabel="Pet'im by PetWork" />
    </View>;
  }

  const changeTab = (next: TabKey) => { setTab(next); setPage('root'); };
  const openLost = () => { setTab('lost'); setPage('root'); };
  const authenticated = (username: string) => { setCurrentUser(username); setTab('home'); setPage('root'); };
  const logout = async () => {
    await SecureStore.deleteItemAsync('petim.session');
    setCurrentUser(null);
    setTab('home');
    setPage('root');
  };

  let screen;
  if (page === 'login') screen = <AuthScreen initialMode="login" onBack={() => setPage('root')} onAuthenticated={authenticated} />;
  else if (page === 'register') screen = <AuthScreen initialMode="register" onBack={() => setPage('root')} onAuthenticated={authenticated} />;
  else if (page === 'account' && currentUser) screen = <AccountScreen username={currentUser} onBack={() => setPage('root')} onLogout={logout} />;
  else if (page === 'nearby') screen = <NearbyScreen onBack={() => setPage('root')} />;
  else if (page === 'adoption') screen = <AdoptionScreen onBack={() => setPage('root')} />;
  else if (page === 'reviews') screen = <ReviewsScreen onBack={() => setPage('root')} />;
  else if (page === 'lost-form') screen = <LostPetForm mode="lost" onBack={() => setPage('root')} />;
  else if (page === 'found-form') screen = <LostPetForm mode="found" onBack={() => setPage('root')} />;
  else if (page === 'lost-detail') screen = <LostDetailScreen onBack={() => setPage('root')} onSighting={() => setPage('sighting')} />;
  else if (page === 'sighting') screen = <SightingForm onBack={() => setPage('lost-detail')} />;
  else if (tab === 'home') screen = <HomeScreen currentUser={currentUser} onOpenAccount={() => setPage('account')} onOpenLost={openLost} onLogin={() => setPage('login')} onRegister={() => setPage('register')} />;
  else if (tab === 'social') screen = <PatiSocialScreen
    onOpenNearby={() => setPage('nearby')}
    onOpenAdoption={() => setPage('adoption')}
    onOpenReviews={() => setPage('reviews')}
    onOpenLost={openLost}
  />;
  else if (tab === 'lost') screen = <LostHub onNavigate={setPage} />;
  else screen = <ComingSoon tab={tab} onHome={() => changeTab('home')} />;

  return (
    <View style={styles.app}>
      <StatusBar style={tab === 'home' ? 'light' : 'dark'} />
      {screen}
      {page !== 'login' && page !== 'register' ? <BottomTabs active={tab} onChange={changeTab} /> : null}
    </View>
  );
}

function HomeScreen({ currentUser, onOpenAccount, onOpenLost, onLogin, onRegister }: { currentUser: string | null; onOpenAccount: () => void; onOpenLost: () => void; onLogin: () => void; onRegister: () => void }) {
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
      <Hero currentUser={currentUser} query={query} setQuery={setQuery} onOpenAccount={onOpenAccount} onLogin={onLogin} onRegister={onRegister} />
      <View style={styles.content}>
        {query.trim() ? <SearchResults query={query} results={results} loading={loading} /> : (
          <>
            <SectionTitle title="Konular" />
            <View style={styles.categoryGrid}>{categories.map(category => <CategoryCard key={category.title} {...category} />)}</View>
            <LostHomeBanner onPress={onOpenLost} />
            <SectionTitle title="Güncel İçerikler" action="Tümü" />
            {loading && !data ? <LoadingCards /> : null}
            {error && !data ? <ErrorState message={error} onRetry={() => load()} /> : null}
            {data?.featured.length ? (
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.horizontalList}>
                {data.featured.map(item => <StoryCard key={`${item.type}-${item.id}`} story={item} />)}
              </ScrollView>
            ) : null}
            {data?.blogs[0] ? <BlogSpotlight story={data.blogs[0]} /> : null}
            {data?.questions[0] ? <QuestionCard question={data.questions[0]} /> : null}
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

function Hero({ currentUser, query, setQuery, onOpenAccount, onLogin, onRegister }: { currentUser: string | null; query: string; setQuery: (value: string) => void; onOpenAccount: () => void; onLogin: () => void; onRegister: () => void }) {
  return (
    <LinearGradient colors={[colors.primaryDark, colors.primary, '#845B7C']} style={styles.hero}>
      <View style={styles.headerRow}>
        <View>
          <View style={styles.brandRow}><Ionicons name="paw" size={19} color={colors.peach} /><Text style={styles.brand}>Pet'im</Text></View>
          <Text style={styles.byline}>by PetWork</Text>
        </View>
        {currentUser ? <Pressable onPress={onOpenAccount} accessibilityRole="button" accessibilityLabel={`${currentUser} hesap menüsünü aç`} style={({ pressed }) => [styles.userChip, pressed && styles.pressed]}><Ionicons name="person-circle-outline" size={21} color={colors.primary} /><Text style={styles.userChipText} numberOfLines={1}>{currentUser}</Text></Pressable> : <View style={styles.authRow}>
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

function SectionTitle({ title, action }: { title: string; action?: string }) {
  return <View style={styles.sectionHeader}><Text style={styles.sectionTitle}>{title}</Text>
    {action ? <Pressable onPress={() => Alert.alert(title, 'Tüm içerik listesi sıradaki ekranda açılacak.')}><Text style={styles.sectionAction}>{action} →</Text></Pressable> : null}
  </View>;
}

function CategoryCard({ title, icon, color, ink }: typeof categories[number]) {
  return <Pressable accessibilityRole="button" accessibilityLabel={`${title} kategorisini aç`}
    onPress={() => Alert.alert(title, 'Bu kategori ekranını sıradaki adımda bağlıyoruz.')}
    style={({ pressed }) => [styles.categoryCard, { backgroundColor: color }, pressed && styles.cardPressed]}>
    <View style={[styles.categoryIcon, { backgroundColor: `${ink}14` }]}><Ionicons name={icon} size={27} color={ink} /></View>
    <Text style={[styles.categoryLabel, { color: ink }]} numberOfLines={2}>{title}</Text>
  </Pressable>;
}

function StoryCard({ story }: { story: Story }) {
  return <Pressable onPress={() => Alert.alert(story.title, story.excerpt)} style={({ pressed }) => [styles.storyCard, pressed && styles.cardPressed]}>
    <ImageBackground source={{ uri: mediaUrl(story.imagePath) }} style={styles.storyImage} imageStyle={styles.storyImageRadius}>
      <View style={styles.storyBadge}><Text style={styles.storyBadgeText}>{story.category}</Text></View>
    </ImageBackground>
    <View style={styles.storyBody}><Text style={styles.storyTitle} numberOfLines={2}>{story.title}</Text>
      <Text style={styles.storySource}>Kaynak: PetWork bilgi merkezi</Text></View>
  </Pressable>;
}

function BlogSpotlight({ story }: { story: Story }) {
  return <Pressable onPress={() => Alert.alert(story.title, story.excerpt)} style={({ pressed }) => [styles.blogSpotlight, pressed && styles.cardPressed]}>
    <Text style={styles.outlineBadge}>Blog</Text><Text style={styles.spotlightTitle}>{story.title}</Text>
    <Text style={styles.spotlightMeta}>PetWork topluluğu · 5 dk okuma</Text><Text style={styles.spotlightSource}>Kaynak: PetWork</Text>
  </Pressable>;
}

function QuestionCard({ question }: { question: Question }) {
  return <Pressable onPress={() => Alert.alert(question.title, `${question.answerCount} cevap bulunuyor.`)} style={({ pressed }) => [styles.questionCard, pressed && styles.cardPressed]}>
    <View style={styles.questionIcon}><Ionicons name="help" size={25} color={colors.peach} /></View>
    <View style={styles.questionBody}><Text style={styles.questionTitle}>{question.title}</Text>
      <Text style={styles.questionMeta}>{question.answerCount} yanıt · {question.username}</Text><Text style={styles.questionLink}>Yanıtları gör →</Text></View>
  </Pressable>;
}

type SearchResult = { kind: 'story'; item: Story } | { kind: 'question'; item: Question };
function SearchResults({ query, results, loading }: { query: string; results: SearchResult[]; loading: boolean }) {
  return <View><SectionTitle title={`“${query.trim()}” sonuçları`} />
    {loading ? <ActivityIndicator color={colors.primary} size="large" /> : null}
    {!loading && !results.length ? <Text style={styles.emptyText}>Bu aramayla eşleşen güncel içerik bulunamadı.</Text> : null}
    {results.map(result => result.kind === 'story'
      ? <View key={`s-${result.item.type}-${result.item.id}`} style={styles.searchResult}><StoryCard story={result.item} /></View>
      : <View key={`q-${result.item.id}`} style={styles.searchResult}><QuestionCard question={result.item} /></View>)}
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

const nearbyPlaces = [
  { title: 'Veterinerler', icon: 'medical-outline' as const, count: '12 yakın sonuç' },
  { title: 'Pet Kuaförleri', icon: 'cut-outline' as const, count: '8 yakın sonuç' },
  { title: 'Pet Otelleri', icon: 'bed-outline' as const, count: '5 yakın sonuç' },
  { title: 'Park ve Oyun Alanları', icon: 'leaf-outline' as const, count: '9 yakın sonuç' },
];

function ScreenShell({ title, subtitle, onBack, children }: { title: string; subtitle?: string; onBack?: () => void; children: React.ReactNode }) {
  return <ScrollView style={styles.screen} contentContainerStyle={styles.subScreenContent} showsVerticalScrollIndicator={false} keyboardShouldPersistTaps="handled">
    <View style={styles.subHeader}>
      {onBack ? <Pressable onPress={onBack} style={styles.backButton} accessibilityLabel="Geri dön"><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable> : <View style={styles.headerMark}><Ionicons name="paw" size={20} color={colors.peach} /></View>}
      <View style={styles.flexOne}><Text style={styles.subTitle}>{title}</Text>{subtitle ? <Text style={styles.subSubtitle}>{subtitle}</Text> : null}</View>
    </View>
    {children}
  </ScrollView>;
}

function NearbyShortcuts({ onOpen }: { onOpen: () => void }) {
  return <View><View style={styles.sectionHeader}><View><Text style={styles.cardSectionTitle}>Yakınındakiler</Text><Text style={styles.cardSectionHint}>Konumunu yalnızca sen istediğinde kullanırız.</Text></View></View>
    <View style={styles.nearbyGrid}>{nearbyPlaces.map((place, index) => <Pressable key={place.title} onPress={onOpen}
      style={({ pressed }) => [styles.nearbyShortcut, { backgroundColor: index % 2 ? colors.sageSoft : colors.lilacSoft }, pressed && styles.cardPressed]}>
      <Ionicons name={place.icon} size={27} color={index % 2 ? '#4E7458' : colors.primary} /><Text style={styles.nearbyTitle}>{place.title}</Text><Text style={styles.nearbyCount}>{place.count}</Text>
    </Pressable>)}</View>
    <Text style={styles.privacyNote}><Ionicons name="lock-closed-outline" size={12} /> Kesin konumun herkese açık gösterilmez.</Text>
  </View>;
}

function NearbyScreen({ onBack }: { onBack: () => void }) {
  const [locationText, setLocationText] = useState('Konum kullanılmadı');
  const [city, setCity] = useState('');
  const [district, setDistrict] = useState('');
  const askLocation = async () => {
    const permission = await Location.requestForegroundPermissionsAsync();
    if (!permission.granted) { setLocationText('İzin verilmedi; şehir ve ilçe ile arayabilirsin.'); return; }
    setLocationText('Konum izni açık · yakın sonuçlar güncellendi');
  };
  return <ScreenShell title="Yakınındakiler" subtitle="İhtiyacın olan yerleri güvenle bul" onBack={onBack}>
    <View style={styles.mapCard}><Ionicons name="map-outline" size={54} color={colors.primary} /><Text style={styles.mapTitle}>Harita ve liste görünümü</Text><Text style={styles.mapText}>{locationText}</Text>
      <ActionButton label="Konumumu kullan" icon="navigate-outline" onPress={askLocation} />
    </View>
    <Text style={styles.formLabel}>Manuel konum</Text><View style={styles.inlineFields}><FormInput value={city} onChangeText={setCity} placeholder="Şehir" /><FormInput value={district} onChangeText={setDistrict} placeholder="İlçe" /></View>
    <SectionTitle title="Yakın sonuçlar" />
    <SampleBadge />
    <PlaceCard title="Pati Dostu Veteriner Kliniği" kind="Veteriner" distance="850 m" rating="4,8" open />
  </ScreenShell>;
}

function PlaceCard({ title, kind, distance, rating, open = false }: { title: string; kind: string; distance: string; rating: string; open?: boolean }) {
  return <View style={styles.placeCard}><View style={styles.placePin}><Ionicons name="location" size={22} color={colors.primary} /></View><View style={styles.flexOne}>
    <Text style={styles.placeTitle}>{title}</Text><Text style={styles.placeMeta}>{kind} · {distance} · ★ {rating}</Text><Text style={[styles.openText, !open && styles.closedText]}>{open ? 'Şimdi açık' : 'Kapalı'}</Text>
    <View style={styles.miniActions}><Text style={styles.textAction}>Yol tarifi</Text><Text style={styles.textAction}>Detaylar</Text></View></View></View>;
}

function AdoptionScreen({ onBack }: { onBack: () => void }) {
  return <ScreenShell title="Sahiplendirme" subtitle="Satın alma, sahiplen" onBack={onBack}>
    <SampleBadge />
    <View style={styles.petProfileCard}><ImageBackground source={communityDemoImage} style={styles.petPhoto} imageStyle={styles.petPhotoRadius}>
      <Text style={styles.safeBadge}>Sağlık bilgisi doğrulandı</Text></ImageBackground>
      <View style={styles.petProfileBody}><View style={styles.rowBetween}><Text style={styles.petName}>Luna</Text><Text style={styles.cityBadge}>İstanbul</Text></View>
        <Text style={styles.petMeta}>2 yaş · Tekir · Dişi</Text><InfoLine icon="medkit-outline" text="Aşıları tam, kısırlaştırılmış" />
        <Text style={styles.storyHeading}>Luna'nın hikâyesi</Text><Text style={styles.bodyText}>İnsanlarla iletişimi güçlü, sakin ve oyun seven Luna için güvenli bir yuva aranıyor.</Text>
        <ActionButton label="Sahiplenme başvurusu yap" icon="heart-outline" onPress={() => Alert.alert('Başvurun alındı', 'Bu prototipte başvuru güvenli iletişim akışına yönlendirilecek.')} />
        <View style={styles.safetyActions}><Text style={styles.textAction}>Güvenlik önerileri</Text><Text style={styles.reportAction}>İlanı bildir</Text></View>
      </View>
    </View>
    <View style={styles.noticeCard}><Ionicons name="shield-checkmark-outline" size={25} color="#4E7458" /><Text style={styles.noticeText}>Hayvan satışı ve fiyat bilgisi bu alanda yer almaz. Görüşmelerde kişisel bilgilerini koru.</Text></View>
  </ScreenShell>;
}

function ReviewsScreen({ onBack }: { onBack: () => void }) {
  const [adding, setAdding] = useState(false);
  return <ScreenShell title="Pati Denedi" subtitle="Gerçek mama deneyimleri" onBack={onBack}>
    <SampleBadge />
    <View style={styles.ratingCard}><View style={styles.packageMock}><Ionicons name="nutrition" size={35} color="#8A6515" /><Text style={styles.packageText}>MAMA</Text></View><View style={styles.flexOne}>
      <View style={styles.labelRow}><Text style={styles.verifiedBadge}>Doğrulanmış Deneyim</Text><Text style={styles.sponsoredBadge}>Sponsorlu</Text></View>
      <Text style={styles.productTitle}>Somonlu Yetişkin Kedi Maması</Text><Text style={styles.productMeta}>PatiPlus · Kedi</Text><Text style={styles.ratingBig}>4,4 ★</Text></View></View>
    <View style={styles.scoreGrid}>{[['Lezzet','4,7'], ['İçerik','4,3'], ['Sindirim','4,5'], ['Fiyat/Değer','4,0']].map(([label, score]) => <View key={label} style={styles.scoreItem}><Text style={styles.score}>{score}</Text><Text style={styles.scoreLabel}>{label}</Text></View>)}</View>
    <View style={styles.safetyActions}><Pressable onPress={() => setAdding(!adding)}><Text style={styles.textAction}>{adding ? 'Formu kapat' : '+ Mama deneyimi ekle'}</Text></Pressable><Text style={styles.reportAction}>İçeriği bildir</Text></View>
    {adding ? <ReviewForm /> : null}
  </ScreenShell>;
}

function ReviewForm() {
  const [brand, setBrand] = useState(''); const [product, setProduct] = useState(''); const [petType, setPetType] = useState('');
  return <View style={styles.formCard}><Text style={styles.formTitle}>Yeni deneyim</Text><View style={styles.photoPicker}><Ionicons name="camera-outline" size={24} color={colors.primary} /><Text style={styles.photoPickerText}>Paket fotoğrafı ekle</Text></View>
    <FormInput value={brand} onChangeText={setBrand} placeholder="Marka" /><FormInput value={product} onChangeText={setProduct} placeholder="Ürün adı" /><FormInput value={petType} onChangeText={setPetType} placeholder="Evcil hayvan türü" />
    <Text style={styles.formHint}>Lezzet · İçerik · Sindirim · Fiyat/Değer puanları gönderim adımında seçilecek.</Text>
    <ActionButton label="Deneyimi kaydet" icon="checkmark-circle-outline" onPress={() => Alert.alert('Taslak kaydedildi', 'Backend bağlantısı eklendiğinde incelemeye gönderilecek.')} />
  </View>;
}

const lostPets = [
  { name: 'Tarçın', species: 'Kedi', district: 'Kadıköy', date: 'Bugün 10:20', status: 'Kayıp', image: communityDemoImage },
];

function LostHub({ onNavigate }: { onNavigate: (page: PageKey) => void }) {
  return <ScreenShell title="Kayıp Patiler" subtitle="Birlikte arıyor, umutla buluşturuyoruz">
    <View style={styles.urgentPanel}><View style={styles.urgentIcon}><Ionicons name="search" size={26} color={colors.white} /></View><View style={styles.flexOne}><Text style={styles.urgentTitle}>Yakınındaki patilere göz kulak ol</Text><Text style={styles.urgentText}>Küçük bir bilgi, bir ailenin yeniden kavuşmasını sağlayabilir.</Text></View></View>
    <View style={styles.lostActions}>
      <SmallAction icon="list-outline" label="Yakınımdaki İlanlar" onPress={() => {}} />
      <SmallAction icon="map-outline" label="Haritada Gör" onPress={() => Alert.alert('Harita', 'Yaklaşık konumlar harita zaman çizelgesinde gösterilecek.')} />
      <SmallAction icon="add-circle-outline" label="Kayıp İlanı Ver" onPress={() => onNavigate('lost-form')} />
      <SmallAction icon="flag-outline" label="Bulunan Hayvan Bildir" onPress={() => onNavigate('found-form')} />
      <SmallAction icon="folder-outline" label="İlanlarım" onPress={() => Alert.alert('İlanlarım', 'Giriş yaptıktan sonra kendi ilanların burada görünecek.')} />
    </View>
    <SectionTitle title="Yakınımdaki ilanlar" action="Tümü" />
    <SampleBadge />
    {lostPets.map((pet, index) => <LostPetCard key={pet.name} pet={pet} urgent={index === 0} onPress={() => onNavigate('lost-detail')} />)}
  </ScreenShell>;
}

function LostPetCard({ pet, urgent, onPress }: { pet: typeof lostPets[number]; urgent?: boolean; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.lostPetCard, urgent && styles.lostPetUrgent, pressed && styles.cardPressed]}>
    <ImageBackground source={pet.image} style={styles.lostPetImage} imageStyle={styles.lostPetImageRadius} />
    <View style={styles.lostPetBody}><View style={styles.rowBetween}><Text style={styles.lostPetName}>{pet.name}</Text><Text style={[styles.statusBadge, statusStyle(pet.status)]}>{pet.status}</Text></View>
      <Text style={styles.lostPetMeta}>{pet.species} · Son görülme: {pet.district}</Text><Text style={styles.lostPetDate}>{pet.date}</Text>
      {urgent ? <Text style={styles.urgentNote}>Yeni ilan · Yakın çevrede dikkatli olalım</Text> : null}
    </View><Ionicons name="chevron-forward" size={20} color={colors.primary} />
  </Pressable>;
}

function statusStyle(status: string) {
  if (status === 'Kayıp') return { backgroundColor: colors.peachSoft, color: '#9B463B' };
  if (status === 'Görüldü') return { backgroundColor: colors.yellowSoft, color: '#72530D' };
  if (status === 'Güvenli Alanda') return { backgroundColor: colors.sageSoft, color: '#41604A' };
  return { backgroundColor: '#E7E1EB', color: colors.primary };
}

function LostDetailScreen({ onBack, onSighting }: { onBack: () => void; onSighting: () => void }) {
  return <ScreenShell title="Tarçın aranıyor" subtitle="Son görülme: Kadıköy" onBack={onBack}>
    <SampleBadge />
    <ImageBackground source={communityDemoImage} style={styles.detailHero} imageStyle={styles.detailHeroRadius}><Text style={[styles.statusBadge, statusStyle('Kayıp')]}>Kayıp · Yeni ilan</Text></ImageBackground>
    <View style={styles.detailCard}><Text style={styles.detailTitle}>Tarçın</Text><Text style={styles.petMeta}>Tekir kedi · 3 yaş · Kadıköy</Text>
      <InfoLine icon="calendar-outline" text="Bugün yaklaşık 10:20'de görüldü" /><InfoLine icon="finger-print-outline" text="Sol kulağında küçük çentik, mor tasma" />
      <Text style={styles.bodyText}>Ürkek olabilir; lütfen kovalamadan, güvenli mesafeden gözlemleyin.</Text>
      <ActionButton label="Burada Gördüm" icon="eye-outline" onPress={onSighting} />
      <View style={styles.timeline}><Text style={styles.timelineTitle}>Görülme zaman çizelgesi</Text><Text style={styles.timelineItem}>● 10:20 · Kadıköy çevresi · İlan sahibi</Text><Text style={styles.timelinePrivate}>Kesin konum ve iletişim bilgileri yalnızca ilan sahibine gösterilir.</Text></View>
      <View style={styles.safetyActions}><Text style={styles.textAction}>Güvenli paylaş</Text><Text style={styles.reportAction}>İlanı bildir</Text></View>
    </View>
  </ScreenShell>;
}

function LostPetForm({ mode, onBack }: { mode: 'lost' | 'found'; onBack: () => void }) {
  const [name, setName] = useState(''); const [breed, setBreed] = useState(''); const [features, setFeatures] = useState(''); const [date, setDate] = useState(''); const [location, setLocation] = useState(''); const [collar, setCollar] = useState(''); const [notes, setNotes] = useState('');
  const lost = mode === 'lost';
  return <ScreenShell title={lost ? 'Kayıp ilanı ver' : 'Bulunan hayvan bildir'} subtitle="Paylaştığın bilgiler güvenle korunur" onBack={onBack}>
    <View style={styles.formCard}><View style={styles.photoPicker}><Ionicons name="images-outline" size={26} color={colors.primary} /><Text style={styles.photoPickerText}>Fotoğraf ekle</Text><Text style={styles.formHint}>Birden fazla fotoğraf ekleyebilirsin</Text></View>
      <FormInput value={name} onChangeText={setName} placeholder={lost ? 'Adı' : 'Varsa bilinen adı'} /><FormInput value={breed} onChangeText={setBreed} placeholder="Türü ve cinsi" />
      <FormInput value={features} onChangeText={setFeatures} placeholder="Ayırt edici özellikleri" multiline /><FormInput value={date} onChangeText={setDate} placeholder={lost ? 'Kaybolma tarihi ve yaklaşık saat' : 'Bulunma tarihi ve yaklaşık saat'} />
      <FormInput value={location} onChangeText={setLocation} placeholder="Yaklaşık konum (mahalle/ilçe)" /><FormInput value={collar} onChangeText={setCollar} placeholder="Tasma / mikroçip bilgisi" />
      <FormInput value={notes} onChangeText={setNotes} placeholder="Önemli notlar" multiline />
      <View style={styles.noticeCard}><Ionicons name="lock-closed-outline" size={22} color="#4E7458" /><Text style={styles.noticeText}>Telefon numaranı veya açık ev adresini paylaşman gerekmez.</Text></View>
      <ActionButton label={lost ? 'İlanı incelemeye gönder' : 'Bildirimi gönder'} icon="paper-plane-outline" onPress={() => Alert.alert('Taslak hazır', 'Hesap ve backend bağlantısı tamamlandığında güvenle gönderilecek.')} />
    </View>
  </ScreenShell>;
}

function SightingForm({ onBack }: { onBack: () => void }) {
  const [location, setLocation] = useState(''); const [time, setTime] = useState(''); const [note, setNote] = useState('');
  return <ScreenShell title="Burada gördüm" subtitle="Gözlemin Tarçın'ın sahibine umut olabilir" onBack={onBack}>
    <View style={styles.formCard}><View style={styles.photoPicker}><Ionicons name="camera-outline" size={26} color={colors.primary} /><Text style={styles.photoPickerText}>Gözlem fotoğrafı ekle</Text></View>
      <FormInput value={location} onChangeText={setLocation} placeholder="Yaklaşık konum" /><FormInput value={time} onChangeText={setTime} placeholder="Gördüğün saat" /><FormInput value={note} onChangeText={setNote} placeholder="Kısa not" multiline />
      <Text style={styles.formHint}>Kesin detaylar yalnızca ilan sahibine iletilir ve haritada yaklaşık olarak gösterilir.</Text>
      <ActionButton label="Gözlemi güvenle gönder" icon="notifications-outline" onPress={() => Alert.alert('Teşekkür ederiz', 'İlan sahibine bildirim gönderilecek ve gözlem zaman çizelgesine eklenecek.')} />
    </View>
  </ScreenShell>;
}

function FormInput({ value, onChangeText, placeholder, multiline = false }: { value: string; onChangeText: (text: string) => void; placeholder: string; multiline?: boolean }) {
  return <TextInput value={value} onChangeText={onChangeText} placeholder={placeholder} placeholderTextColor="#958991" multiline={multiline} style={[styles.formInput, multiline && styles.formInputMultiline]} />;
}

function ActionButton({ label, icon, onPress }: { label: string; icon: keyof typeof Ionicons.glyphMap; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.fullButton, pressed && styles.pressed]}><Ionicons name={icon} size={19} color={colors.white} /><Text style={styles.fullButtonText}>{label}</Text></Pressable>;
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

function SampleBadge() {
  return <View style={styles.sampleNotice}><Ionicons name="eye-outline" size={14} color={colors.primary} /><Text style={styles.sampleNoticeText}>Örnek görünüm</Text></View>;
}

function ComingSoon({ tab, onHome }: { tab: 'match' | 'settings'; onHome: () => void }) {
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

const styles = StyleSheet.create({
  app: { flex: 1, backgroundColor: colors.background }, screen: { flex: 1, backgroundColor: colors.background }, scrollContent: { paddingBottom: 108 },
  splashView: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background }, splashArtwork: { width: '100%', height: '100%' },
  hero: { paddingTop: statusInset + 14, paddingHorizontal: 22, paddingBottom: 24, borderBottomLeftRadius: 24, borderBottomRightRadius: 24 },
  headerRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 }, brandRow: { flexDirection: 'row', alignItems: 'center', gap: 7 },
  brand: { color: colors.white, fontFamily: serif, fontSize: 29, fontWeight: '700', letterSpacing: -0.6 }, byline: { color: '#DDCED9', fontSize: 11, marginTop: 1, marginLeft: 27, letterSpacing: 0.3 },
  authRow: { flexDirection: 'row', gap: 8 }, loginButton: { minHeight: 42, paddingHorizontal: 16, borderRadius: 22, backgroundColor: colors.card, justifyContent: 'center' },
  userChip: { maxWidth: 150, minHeight: 42, flexDirection: 'row', alignItems: 'center', gap: 7, backgroundColor: colors.card, paddingHorizontal: 13, borderRadius: 22 }, userChipText: { flexShrink: 1, color: colors.primaryDark, fontSize: 12, fontWeight: '800' },
  loginText: { color: colors.primaryDark, fontWeight: '700', fontSize: 13 }, joinButton: { minHeight: 42, paddingHorizontal: 15, borderRadius: 22, borderWidth: 1.2, borderColor: '#F6EAF2', justifyContent: 'center' },
  joinText: { color: colors.white, fontWeight: '700', fontSize: 13 }, pressed: { opacity: 0.78, transform: [{ scale: 0.98 }] },
  searchBox: { minHeight: 48, marginTop: 22, borderWidth: 1, borderColor: '#A987A0', borderRadius: 25, backgroundColor: '#FFFFFF16', flexDirection: 'row', alignItems: 'center', paddingHorizontal: 17, gap: 10 },
  searchInput: { flex: 1, color: colors.white, fontSize: 14, paddingVertical: 10 }, content: { width: '100%', maxWidth: 760, alignSelf: 'center', paddingHorizontal: 20 },
  sectionHeader: { marginTop: 27, marginBottom: 14, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, sectionTitle: { color: colors.text, fontFamily: serif, fontWeight: '700', fontSize: 23, letterSpacing: -0.4 },
  sectionAction: { color: colors.primary, fontWeight: '700', fontSize: 13, paddingVertical: 8 }, categoryGrid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 12 },
  categoryCard: { width: '31.5%', aspectRatio: 1.03, borderRadius: 19, alignItems: 'center', justifyContent: 'center', padding: 8, ...shadow }, categoryIcon: { width: 43, height: 43, borderRadius: 22, alignItems: 'center', justifyContent: 'center', marginBottom: 9 },
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
  lostHomeBanner: { flexDirection: 'row', alignItems: 'center', gap: 13, marginTop: 18, padding: 17, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach, borderRadius: 22, ...shadow },
  lostHomeIcon: { width: 48, height: 48, borderRadius: 24, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }, lostEyebrow: { color: '#9B463B', fontSize: 10, fontWeight: '900', letterSpacing: 1 },
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
  mapCard: { alignItems: 'center', backgroundColor: colors.sageSoft, borderRadius: 24, padding: 22, marginTop: 18 }, mapTitle: { color: colors.text, fontFamily: serif, fontSize: 21, fontWeight: '700', marginTop: 8 }, mapText: { color: colors.muted, textAlign: 'center', fontSize: 11, marginTop: 5 },
  formLabel: { color: colors.text, fontWeight: '800', fontSize: 13, marginTop: 20, marginBottom: 8 }, inlineFields: { flexDirection: 'row', gap: 10 },
  placeCard: { flexDirection: 'row', alignItems: 'flex-start', gap: 12, backgroundColor: colors.card, borderRadius: 20, padding: 16, marginBottom: 12, ...shadow }, placePin: { width: 43, height: 43, borderRadius: 22, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  placeTitle: { color: colors.text, fontSize: 14, fontWeight: '800' }, placeMeta: { color: colors.muted, fontSize: 11, marginTop: 4 }, openText: { color: '#41604A', fontSize: 10, fontWeight: '800', marginTop: 6 }, closedText: { color: '#9B463B' }, miniActions: { flexDirection: 'row', gap: 18, marginTop: 11 }, textAction: { color: colors.primary, fontSize: 12, fontWeight: '800' }, reportAction: { color: '#9B463B', fontSize: 12, fontWeight: '800' },
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
});
