import { Ionicons } from '@expo/vector-icons';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator, Alert, BackHandler, ImageBackground, Linking, Platform, Pressable, RefreshControl,
  ScrollView, StatusBar as NativeStatusBar, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { ContentItem, ContentKind, getContent, mediaUrl } from '../api';
import { colors, createThemedStyles, shadow } from '../theme';

const config: Record<ContentKind, { title: string; subtitle: string; icon: keyof typeof Ionicons.glyphMap; color: string }> = {
  all: { title: 'Tüm İçerikler', subtitle: 'En yeni PetWork yayınları', icon: 'library-outline', color: colors.lilacSoft },
  guides: { title: 'Bakım Rehberleri', subtitle: 'Bakım, eğitim ve günlük yaşam', icon: 'book-outline', color: colors.sageSoft },
  diseases: { title: 'Hastalıklar', subtitle: 'Belirtiler, korunma ve veteriner desteği', icon: 'medkit-outline', color: colors.peachSoft },
  recipes: { title: 'Tarifler', subtitle: 'Patilere uygun tarif ve beslenme fikirleri', icon: 'restaurant-outline', color: colors.yellowSoft },
  blogs: { title: 'Blog', subtitle: 'PetWork ve web kaynaklarından güncel yazılar', icon: 'create-outline', color: colors.lilacSoft },
  grief: { title: 'Yas ve Kayıp', subtitle: 'Kayıpla baş etme ve destek kaynakları', icon: 'heart-outline', color: colors.sageSoft },
};

export function ContentScreen({ kind, onBack, onOpenLost }: { kind: ContentKind; onBack: () => void; onOpenLost: () => void }) {
  const [items, setItems] = useState<ContentItem[]>([]);
  const [selected, setSelected] = useState<ContentItem | null>(null);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const section = config[kind];

  const load = useCallback(async (refresh = false) => {
    refresh ? setRefreshing(true) : setLoading(true);
    setError(null);
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 12000);
    try { setItems(await getContent(kind, '', controller.signal)); }
    catch { setError('İçerikler yüklenemedi. Sunucu bağlantısını kontrol edip yeniden dene.'); }
    finally { clearTimeout(timer); setLoading(false); setRefreshing(false); }
  }, [kind]);

  useEffect(() => { setSelected(null); setQuery(''); load(); }, [load]);
  useEffect(() => {
    if (!selected) return;
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      setSelected(null);
      return true;
    });
    return () => subscription.remove();
  }, [selected]);
  const visibleItems = useMemo(() => {
    const term = query.trim().toLocaleLowerCase('tr-TR');
    if (!term) return items;
    return items.filter(item => `${item.title} ${item.summary} ${item.category} ${item.animalType ?? ''}`
      .toLocaleLowerCase('tr-TR').includes(term));
  }, [items, query]);

  if (selected) return <ContentDetail item={selected} onBack={() => setSelected(null)} />;

  return <ScrollView style={styles.screen} contentContainerStyle={styles.content}
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => load(true)} tintColor={colors.primary} />}
    keyboardShouldPersistTaps="handled">
    <Header title={section.title} subtitle={section.subtitle} icon={section.icon} onBack={onBack} />
    <View style={[styles.intro, { backgroundColor: section.color }]}>
      <Ionicons name={section.icon} size={30} color={colors.primary} />
      <Text style={styles.introText}>{kind === 'diseases'
        ? 'Acil veya ağır belirtilerde içerik okumakla yetinmeyip veteriner hekime başvur.'
        : 'İçerikler PetWork veritabanından ve kaynak bilgisi korunarak webden getirilen yayınlardan sunulur.'}</Text>
    </View>
    {kind === 'grief' ? <Pressable onPress={() => onOpenLost()} style={styles.lostButton}>
      <Ionicons name="location" size={21} color={colors.white} />
      <View style={styles.flex}><Text style={styles.lostTitle}>Kayıp pati ilanlarına git</Text><Text style={styles.lostText}>İlanları gör veya yeni bildirim oluştur</Text></View>
      <Ionicons name="chevron-forward" size={20} color={colors.white} />
    </Pressable> : null}
    <View style={styles.searchBox}><Ionicons name="search" size={18} color={colors.primary} />
      <TextInput value={query} onChangeText={setQuery} placeholder={`${section.title} içinde ara…`}
        placeholderTextColor={colors.muted} style={styles.searchInput} />
      {query ? <Pressable onPress={() => setQuery('')}><Ionicons name="close-circle" size={19} color={colors.muted} /></Pressable> : null}
    </View>
    <View style={styles.resultRow}><Text style={styles.resultText}>{visibleItems.length} içerik</Text>
      <Text style={styles.resultHint}>Yenilemek için aşağı çek</Text></View>
    {loading ? <ActivityIndicator style={styles.loader} size="large" color={colors.primary} /> : null}
    {error ? <View style={styles.error}><Text style={styles.errorText}>{error}</Text><Pressable onPress={() => load()}><Text style={styles.retry}>Tekrar dene</Text></Pressable></View> : null}
    {!loading && !error && !visibleItems.length ? <Text style={styles.empty}>Bu bölümde henüz eşleşen içerik yok.</Text> : null}
    {visibleItems.map(item => <Pressable key={`${item.kind}-${item.id}`} onPress={() => setSelected(item)}
      style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
      {item.imagePath ? <ImageBackground source={{ uri: mediaUrl(item.imagePath) }} style={styles.cardImage} imageStyle={styles.cardImageRadius} /> :
        <View style={[styles.cardImage, styles.placeholder]}><Ionicons name={section.icon} size={34} color={colors.primary} /></View>}
      <View style={styles.cardBody}><View style={styles.badgeRow}><Text style={styles.badge}>{item.category}</Text>
        {item.animalType ? <Text style={styles.animal}>{item.animalType}</Text> : null}</View>
        <Text style={styles.cardTitle}>{item.title}</Text><Text style={styles.summary} numberOfLines={3}>{item.summary}</Text>
        <View style={styles.metaRow}><Text style={styles.source} numberOfLines={1}>Kaynak: {item.sourceName || 'PetWork'}</Text>
          <Text style={styles.read}>Oku →</Text></View></View>
    </Pressable>)}
  </ScrollView>;
}

function ContentDetail({ item, onBack }: { item: ContentItem; onBack: () => void }) {
  return <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
    <Header title={item.category} subtitle={item.animalType || 'PetWork bilgi merkezi'} icon="document-text-outline" onBack={onBack} />
    {item.imagePath ? <ImageBackground source={{ uri: mediaUrl(item.imagePath) }} style={styles.heroImage} imageStyle={styles.heroRadius} /> : null}
    <Text style={styles.detailTitle}>{item.title}</Text>
    {item.meta ? <Text style={styles.detailMeta}>{item.meta} · {item.viewCount} görüntülenme</Text> : <Text style={styles.detailMeta}>{item.viewCount} görüntülenme</Text>}
    <View style={styles.detailCard}><Text style={styles.detailSummary}>{item.summary}</Text><Text style={styles.body}>{item.body}</Text></View>
    <View style={styles.sourceCard}><Ionicons name="shield-checkmark-outline" size={24} color="#4E7458" />
      <View style={styles.flex}><Text style={styles.sourceTitle}>Kaynak ve atıf</Text><Text style={styles.sourceCopy}>{item.attribution || item.sourceName || 'PetWork bilgi merkezi'}</Text></View></View>
    {item.sourceUrl ? <Pressable onPress={() => void openSource(item.sourceUrl!)} style={styles.sourceButton}>
      <Ionicons name="open-outline" size={19} color={colors.white} /><Text style={styles.sourceButtonText}>Orijinal web kaynağını aç</Text>
    </Pressable> : null}
    {item.kind === 'diseases' ? <Text style={styles.disclaimer}>Bu içerik tanı veya tedavi yerine geçmez. Belirti varsa veteriner hekime danış.</Text> : null}
  </ScrollView>;
}

async function openSource(url: string) {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') throw new Error('unsupported');
    if (!await Linking.canOpenURL(url)) throw new Error('unsupported');
    await Linking.openURL(url);
  } catch {
    Alert.alert('Bağlantı açılamadı', 'Web kaynağı şu anda açılamıyor. Lütfen daha sonra tekrar dene.');
  }
}

function Header({ title, subtitle, icon, onBack }: { title: string; subtitle: string; icon: keyof typeof Ionicons.glyphMap; onBack: () => void }) {
  return <View style={styles.header}><Pressable onPress={onBack} style={styles.back}><Ionicons name="arrow-back" size={23} color={colors.primary} /></Pressable>
    <View style={styles.mark}><Ionicons name={icon} size={21} color={colors.white} /></View><View style={styles.flex}><Text style={styles.title}>{title}</Text><Text style={styles.subtitle}>{subtitle}</Text></View></View>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const statusInset = Platform.OS === 'android' ? NativeStatusBar.currentHeight ?? 24 : 48;
const styles = createThemedStyles(() => ({
  screen: { flex: 1, backgroundColor: colors.background }, content: { width: '100%', maxWidth: 760, alignSelf: 'center', paddingTop: statusInset + 8, paddingHorizontal: 20, paddingBottom: 116 },
  header: { minHeight: 62, flexDirection: 'row', alignItems: 'center', gap: 10 }, back: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  mark: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }, flex: { flex: 1 }, title: { color: colors.text, fontFamily: serif, fontSize: 25, fontWeight: '700' }, subtitle: { color: colors.muted, fontSize: 10, marginTop: 2 },
  intro: { flexDirection: 'row', alignItems: 'center', gap: 13, padding: 16, borderRadius: 20, marginTop: 12 }, introText: { flex: 1, color: colors.text, fontSize: 11, lineHeight: 17 },
  lostButton: { flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: colors.primary, borderRadius: 19, padding: 15, marginTop: 12 }, lostTitle: { color: colors.white, fontSize: 13, fontWeight: '800' }, lostText: { color: '#E9DDE6', fontSize: 10, marginTop: 2 },
  searchBox: { minHeight: 49, flexDirection: 'row', alignItems: 'center', gap: 9, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 18, paddingHorizontal: 14, marginTop: 16 }, searchInput: { flex: 1, color: colors.text, fontSize: 13 },
  resultRow: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 18, marginBottom: 10 }, resultText: { color: colors.text, fontSize: 12, fontWeight: '800' }, resultHint: { color: colors.muted, fontSize: 10 }, loader: { marginTop: 55 },
  error: { backgroundColor: colors.peachSoft, padding: 17, borderRadius: 18, gap: 10 }, errorText: { color: colors.text, fontSize: 12 }, retry: { color: colors.primary, fontWeight: '800' }, empty: { color: colors.muted, backgroundColor: colors.card, padding: 20, borderRadius: 18 },
  card: { flexDirection: 'row', minHeight: 148, backgroundColor: colors.card, borderRadius: 20, marginBottom: 12, overflow: 'hidden', ...shadow }, pressed: { opacity: 0.82, transform: [{ scale: 0.99 }] },
  cardImage: { width: 112, minHeight: 148 }, cardImageRadius: { borderTopLeftRadius: 20, borderBottomLeftRadius: 20 }, placeholder: { backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, cardBody: { flex: 1, padding: 13 },
  badgeRow: { flexDirection: 'row', alignItems: 'center', gap: 6 }, badge: { color: '#4E7458', backgroundColor: colors.sageSoft, borderRadius: 10, paddingVertical: 3, paddingHorizontal: 7, overflow: 'hidden', fontSize: 8, fontWeight: '800' }, animal: { color: colors.muted, fontSize: 9 },
  cardTitle: { color: colors.text, fontSize: 14, lineHeight: 18, fontWeight: '800', marginTop: 8 }, summary: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 4 }, metaRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 5, marginTop: 8 }, source: { flex: 1, color: colors.muted, fontSize: 8 }, read: { color: colors.primary, fontSize: 10, fontWeight: '800' },
  heroImage: { height: 210, marginTop: 14 }, heroRadius: { borderRadius: 23 }, detailTitle: { color: colors.text, fontFamily: serif, fontSize: 28, lineHeight: 34, fontWeight: '700', marginTop: 22 }, detailMeta: { color: colors.muted, fontSize: 11, marginTop: 8 },
  detailCard: { backgroundColor: colors.card, borderRadius: 22, padding: 19, marginTop: 16, ...shadow }, detailSummary: { color: colors.text, fontSize: 14, lineHeight: 21, fontWeight: '700', paddingBottom: 14, borderBottomWidth: 1, borderBottomColor: colors.border }, body: { color: colors.text, fontSize: 13, lineHeight: 21, marginTop: 15 },
  sourceCard: { flexDirection: 'row', alignItems: 'flex-start', gap: 11, backgroundColor: colors.sageSoft, borderRadius: 18, padding: 15, marginTop: 14 }, sourceTitle: { color: colors.text, fontSize: 12, fontWeight: '800' }, sourceCopy: { color: '#56635A', fontSize: 10, lineHeight: 15, marginTop: 4 },
  sourceButton: { minHeight: 48, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: colors.primary, borderRadius: 17, marginTop: 12 }, sourceButtonText: { color: colors.white, fontSize: 12, fontWeight: '800' }, disclaimer: { color: '#8D4339', backgroundColor: colors.peachSoft, borderRadius: 16, padding: 14, fontSize: 10, lineHeight: 15, marginTop: 12 },
}));
