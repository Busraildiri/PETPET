import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import * as Location from 'expo-location';
import { useCallback, useEffect, useState, type ReactNode } from 'react';
import {
  ActivityIndicator, Alert, BackHandler, Image, InputAccessoryView, Keyboard, KeyboardAvoidingView, Linking,
  Platform, Pressable, RefreshControl, ScrollView, Share, StyleSheet, Text, TextInput, View,
} from 'react-native';
import {
  createLostPet, createLostPetSighting, deleteLostPet, getLostPet, getLostPets, getMyLostPets,
  mediaUrl, updateLostPetStatus, type CreateLostPetRequest, type LostPetDetail, type LostPetSummary,
} from '../api';
import { colors, createThemedStyles, shadow } from '../theme';

type ViewKey = 'hub' | 'list' | 'map' | 'mine' | 'detail' | 'lost-form' | 'found-form' | 'sighting';
type Props = { token: string | null; username: string | null; onLogin: () => void; initialListingId?: number | null };

export function LostPetsScreen({ token, username, onLogin, initialListingId }: Props) {
  const [view, setView] = useState<ViewKey>('hub');
  const [items, setItems] = useState<LostPetSummary[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [nearby, setNearby] = useState(false);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (refresh = false, useLocation = nearby) => {
    refresh ? setRefreshing(true) : setLoading(true);
    setError(null);
    try {
      if (view === 'mine') {
        if (!token) throw new Error('İlanlarını görmek için giriş yapmalısın.');
        setItems(await getMyLostPets(token));
      } else {
        let coordinates: { latitude?: number; longitude?: number } = {};
        if (useLocation) {
          const permission = await Location.requestForegroundPermissionsAsync();
          if (permission.status !== 'granted') throw new Error('Yakındaki ilanlar için konum izni vermelisin.');
          const result = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
          coordinates = result.coords;
        }
        setItems(await getLostPets({ ...coordinates, radiusKm: 50 }));
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'İlanlar yüklenemedi.');
    } finally { setLoading(false); setRefreshing(false); }
  }, [nearby, token, view]);

  useEffect(() => { if (view === 'hub' || view === 'list' || view === 'map' || view === 'mine') void load(); }, [view, load]);
  useEffect(() => {
    if (!initialListingId) return;
    setSelectedId(initialListingId);
    setView('detail');
  }, [initialListingId]);
  useEffect(() => {
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      if (view === 'hub') return false;
      setView(view === 'sighting' ? 'detail' : 'hub');
      return true;
    });
    return () => subscription.remove();
  }, [view]);

  const requireLogin = (next: ViewKey) => {
    if (token && username) return setView(next);
    Alert.alert('Giriş yapmalısın', 'Bu işlem için Pet’im hesabına giriş yap.', [
      { text: 'Vazgeç', style: 'cancel' }, { text: 'Giriş Yap', onPress: onLogin },
    ]);
  };
  const openItem = (id: number) => { setSelectedId(id); setView('detail'); };
  const openNearby = () => { setNearby(true); setView('list'); };

  if (view === 'lost-form' || view === 'found-form') return <ListingForm
    kind={view === 'lost-form' ? 'lost' : 'found'} token={token!} onBack={() => setView('hub')}
    onCreated={item => { setSelectedId(item.id); setView('detail'); }}
  />;
  if (view === 'detail' && selectedId) return <Detail id={selectedId} token={token} onBack={() => setView('hub')}
    onSighting={() => requireLogin('sighting')} onChanged={() => { setView('mine'); }} />;
  if (view === 'sighting' && selectedId) return <SightingForm id={selectedId} token={token!} onBack={() => setView('detail')}
    onCreated={() => setView('detail')} />;

  const title = view === 'mine' ? 'İlanlarım' : view === 'map' ? 'İlan haritası' : view === 'list' ? 'Yakındaki ilanlar' : 'Kayıp Patiler';
  return <Page title={title} subtitle={view === 'hub' ? 'Birlikte arıyor, umutla buluşturuyoruz' : `${items.length} gerçek ilan`}
    refreshing={refreshing} onRefresh={() => void load(true)}
    onBack={view === 'hub' ? undefined : () => { setNearby(false); setView('hub'); }}>
    {view === 'hub' ? <>
      <View style={styles.urgent}><View style={styles.roundCoral}><Ionicons name="search" size={28} color="#fff" /></View><View style={styles.flex}>
        <Text style={styles.urgentTitle}>Yakınındaki patilere göz kulak ol</Text><Text style={styles.muted}>Küçük bir bilgi, bir ailenin yeniden kavuşmasını sağlayabilir.</Text>
      </View></View>
      <View style={styles.actions}>
        <Action icon="list-outline" label="Yakınımdaki İlanlar" onPress={openNearby} />
        <Action icon="map-outline" label="Haritada Gör" onPress={() => setView('map')} />
        <Action icon="add-circle-outline" label="Kayıp İlanı Ver" onPress={() => requireLogin('lost-form')} />
        <Action icon="flag-outline" label="Bulunan Hayvan Bildir" onPress={() => requireLogin('found-form')} />
        <Action icon="folder-outline" label="İlanlarım" onPress={() => requireLogin('mine')} />
      </View>
      <View style={styles.sectionRow}><Text style={styles.sectionTitle}>Güncel ilanlar</Text><Pressable onPress={() => { setNearby(false); setView('list'); }}><Text style={styles.link}>Tümü →</Text></Pressable></View>
    </> : null}
    {loading ? <ActivityIndicator color={colors.primary} size="large" style={styles.loader} /> : null}
    {error ? <View style={styles.error}><Text style={styles.errorText}>{error}</Text><Pressable onPress={() => void load()}><Text style={styles.link}>Tekrar dene</Text></Pressable></View> : null}
    {!loading && !error && items.length === 0 ? <Empty mine={view === 'mine'} /> : null}
    {!loading && !error && items.map(item => <ListingCard key={item.id} item={item} mapMode={view === 'map'} onPress={() => openItem(item.id)} />)}
  </Page>;
}

function Page({ title, subtitle, onBack, refreshing, onRefresh, children }: { title: string; subtitle: string; onBack?: () => void; refreshing?: boolean; onRefresh?: () => void; children: ReactNode }) {
  return <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <ScrollView style={styles.page} contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled"
      keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'} onScrollBeginDrag={Keyboard.dismiss}
      refreshControl={onRefresh ? <RefreshControl refreshing={Boolean(refreshing)} onRefresh={onRefresh} tintColor={colors.primary} /> : undefined}>
      <View style={styles.header}>{onBack ? <Pressable onPress={onBack} style={styles.back}><Ionicons name="arrow-back" size={26} color={colors.primary} /></Pressable> : <View style={styles.logo}><Ionicons name="paw" size={25} color="#F0A28F" /></View>}
        <View style={styles.flex}><Text style={styles.title}>{title}</Text><Text style={styles.subtitle}>{subtitle}</Text></View></View>
      {children}
    </ScrollView>
    {Platform.OS === 'ios' ? <InputAccessoryView nativeID="lost-pets-keyboard">
      <View style={styles.keyboardBar}><Pressable onPress={Keyboard.dismiss} hitSlop={12}><Text style={styles.keyboardDone}>Klavyeyi kapat</Text></Pressable></View>
    </InputAccessoryView> : null}
  </KeyboardAvoidingView>;
}

function Action({ icon, label, onPress }: { icon: keyof typeof Ionicons.glyphMap; label: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.action, pressed && styles.pressed]}><Ionicons name={icon} size={27} color={colors.primary} /><Text style={styles.actionText}>{label}</Text></Pressable>;
}

function Empty({ mine }: { mine: boolean }) {
  return <View style={styles.empty}><Ionicons name="paw-outline" size={42} color="#B8AAB4" /><Text style={styles.emptyTitle}>{mine ? 'Henüz ilan oluşturmadın' : 'Henüz aktif ilan yok'}</Text><Text style={styles.muted}>{mine ? 'Oluşturduğun ilanlar burada görünecek.' : 'Yeni kayıp ve bulunan hayvan bildirimleri burada görünecek.'}</Text></View>;
}

function ListingCard({ item, mapMode, onPress }: { item: LostPetSummary; mapMode: boolean; onPress: () => void }) {
  const openMap = () => {
    const query = item.latitude != null && item.longitude != null ? `${item.latitude},${item.longitude}` : `${item.neighborhood ?? ''} ${item.district} ${item.city}`;
    void Linking.openURL(`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}`);
  };
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
    <Image source={{ uri: mediaUrl(item.imagePath) }} style={styles.cardImage} />
    <View style={styles.flex}><View style={styles.row}><Text style={styles.cardTitle}>{item.petName}</Text><Text style={[styles.badge, item.kind === 'lost' ? styles.lost : styles.found]}>{item.kind === 'lost' ? 'Kayıp' : 'Bulundu'}</Text></View>
      <Text style={styles.muted}>{item.species}{item.breed ? ` · ${item.breed}` : ''}</Text><Text style={styles.meta}>{item.neighborhood ? `${item.neighborhood}, ` : ''}{item.district} / {item.city}</Text>
      <Text style={styles.meta}>{new Date(item.eventAt).toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })}{item.distanceKm != null ? ` · ${item.distanceKm} km` : ''}</Text>
      {mapMode ? <Pressable onPress={openMap}><Text style={styles.link}>Haritada aç →</Text></Pressable> : null}
    </View><Ionicons name="chevron-forward" size={20} color={colors.primary} />
  </Pressable>;
}

function Detail({ id, token, onBack, onSighting, onChanged }: { id: number; token: string | null; onBack: () => void; onSighting: () => void; onChanged: () => void }) {
  const [item, setItem] = useState<LostPetDetail | null>(null); const [error, setError] = useState<string | null>(null);
  const load = useCallback(async () => { try { setError(null); setItem(await getLostPet(id, token)); } catch (e) { setError(e instanceof Error ? e.message : 'İlan yüklenemedi.'); } }, [id, token]);
  useEffect(() => { void load(); }, [load]);
  if (!item) return <Page title="İlan" subtitle="Detaylar yükleniyor" onBack={onBack}>{error ? <Text style={styles.errorText}>{error}</Text> : <ActivityIndicator color={colors.primary} />}</Page>;
  const share = () => void Share.share({ message: `${item.petName} ${item.kind === 'lost' ? 'aranıyor' : 'bulundu'}\n${item.district}, ${item.city}\n${item.distinguishingFeatures}\nPet'im by PetWork` });
  const openSightingMap = async (latitude: number, longitude: number) => {
    const url = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${latitude},${longitude}`)}`;
    try { await Linking.openURL(url); } catch { Alert.alert('Harita açılamadı', 'Bu koordinatı açabilecek bir harita uygulaması bulunamadı.'); }
  };
  const close = () => Alert.alert('İlanı kapat', 'Hayvan ailesine kavuştuysa ilanı çözüldü olarak kapatabilirsin.', [
    { text: 'Vazgeç', style: 'cancel' }, { text: 'Çözüldü', onPress: async () => { try { await updateLostPetStatus(token!, id, 'resolved'); onChanged(); } catch (e) { Alert.alert('Güncellenemedi', e instanceof Error ? e.message : 'Tekrar dene.'); } } },
  ]);
  const remove = () => Alert.alert('İlanı sil', 'Bu ilan listelerden kaldırılacak.', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Sil', style: 'destructive', onPress: async () => { try { await deleteLostPet(token!, id); onChanged(); } catch (e) { Alert.alert('Silinemedi', e instanceof Error ? e.message : 'Tekrar dene.'); } } }]);
  return <Page title={`${item.petName} ${item.kind === 'lost' ? 'aranıyor' : 'bulundu'}`} subtitle={`${item.district}, ${item.city}`} onBack={onBack}>
    <Image source={{ uri: mediaUrl(item.imagePath) }} style={styles.hero} />
    <View style={styles.detail}><View style={styles.row}><Text style={styles.detailTitle}>{item.petName}</Text><Text style={[styles.badge, item.kind === 'lost' ? styles.lost : styles.found]}>{item.kind === 'lost' ? 'Kayıp' : 'Bulundu'}</Text></View>
      <Info icon="paw-outline" text={`${item.species}${item.breed ? ` · ${item.breed}` : ''}`} /><Info icon="calendar-outline" text={new Date(item.eventAt).toLocaleString('tr-TR')} />
      <Info icon="location-outline" text={`${item.neighborhood ? `${item.neighborhood}, ` : ''}${item.district} / ${item.city}`} />
      <Text style={styles.label}>Ayırt edici özellikler</Text><Text style={styles.body}>{item.distinguishingFeatures}</Text>
      {item.collarOrMicrochip ? <><Text style={styles.label}>Tasma / mikroçip</Text><Text style={styles.body}>{item.collarOrMicrochip}</Text></> : null}
      {item.notes ? <><Text style={styles.label}>Not</Text><Text style={styles.body}>{item.notes}</Text></> : null}
      {!item.isMine && item.status === 'active' ? <Primary label="Burada Gördüm" icon="eye-outline" onPress={onSighting} /> : null}
      <View style={styles.sectionRow}><Text style={styles.sectionTitle}>Görülme bildirimleri</Text><Text style={styles.muted}>{item.sightings.length}</Text></View>
      {item.sightings.length ? item.sightings.map(s => <View key={s.id} style={styles.timeline}><Text style={styles.label}>{new Date(s.seenAt).toLocaleString('tr-TR')}</Text><Text style={styles.body}>{s.locationLabel}</Text>{s.note ? <Text style={styles.muted}>{s.note}</Text> : null}{item.isMine && s.latitude != null && s.longitude != null ? <Pressable onPress={() => void openSightingMap(s.latitude!, s.longitude!)}><Text style={styles.link}>Kesin konumu haritada aç →</Text></Pressable> : null}</View>) : <Text style={styles.muted}>Henüz görülme bildirimi yok.</Text>}
      <Text style={styles.privacy}>{item.isMine ? 'Gözlemde koordinat paylaşıldıysa kesin konumu yalnızca sen haritada açabilirsin.' : 'Kesin konum ayrıntıları yalnızca ilan sahibine gösterilir.'}</Text>
      <Pressable onPress={share}><Text style={styles.link}>Güvenli paylaş</Text></Pressable>
      {item.isMine ? <View style={styles.ownerActions}><Pressable onPress={close}><Text style={styles.link}>Çözüldü olarak kapat</Text></Pressable><Pressable onPress={remove}><Text style={styles.danger}>İlanı sil</Text></Pressable></View> : null}
    </View>
  </Page>;
}

function ListingForm({ kind, token, onBack, onCreated }: { kind: 'lost' | 'found'; token: string; onBack: () => void; onCreated: (item: LostPetDetail) => void }) {
  const [photo, setPhoto] = useState<ImagePicker.ImagePickerAsset | null>(null); const [petName, setPetName] = useState(''); const [species, setSpecies] = useState('');
  const [breed, setBreed] = useState(''); const [features, setFeatures] = useState(''); const [eventAt, setEventAt] = useState(localDateTime());
  const [city, setCity] = useState(''); const [district, setDistrict] = useState(''); const [neighborhood, setNeighborhood] = useState('');
  const [collar, setCollar] = useState(''); const [notes, setNotes] = useState(''); const [coords, setCoords] = useState<{ latitude: number; longitude: number }>();
  const [submitting, setSubmitting] = useState(false); const [error, setError] = useState<string | null>(null);
  const choosePhoto = async () => {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) return Alert.alert('Fotoğraf izni gerekli', 'İlan fotoğrafı seçmek için galeri izni vermelisin.');
    const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], allowsEditing: true, aspect: [4, 3], quality: 0.85 });
    if (!result.canceled) setPhoto(result.assets[0]);
  };
  const useLocation = async () => {
    const permission = await Location.requestForegroundPermissionsAsync();
    if (permission.status !== 'granted') return Alert.alert('Konum izni gerekli', 'Yaklaşık konumu eklemek için izin vermelisin.');
    const result = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
    setCoords(result.coords); Alert.alert('Konum eklendi', 'Koordinatın ilan sahibine tam, diğer kullanıcılara yaklaşık gösterilecek.');
  };
  const submit = async () => {
    if (!photo || !petName.trim() || !species.trim() || features.trim().length < 5 || !city.trim() || !district.trim()) return setError('Fotoğraf, ad, tür, ayırt edici özellik, il ve ilçe zorunludur.');
    const parsed = new Date(eventAt);
    if (Number.isNaN(parsed.getTime())) return setError('Tarih biçimi geçersiz. Örnek: 2026-09-08T17:30');
    setSubmitting(true); setError(null);
    try {
      const request: CreateLostPetRequest = { kind, petName: petName.trim(), species: species.trim(), breed: breed.trim(), distinguishingFeatures: features.trim(), eventAt, city: city.trim(), district: district.trim(), neighborhood: neighborhood.trim(), latitude: coords?.latitude, longitude: coords?.longitude, collarOrMicrochip: collar.trim(), notes: notes.trim(), image: { uri: photo.uri, mimeType: photo.mimeType } };
      onCreated(await createLostPet(token, request));
      Alert.alert('İlan yayınlandı', 'İlanın gerçek kaydı oluşturuldu ve listelerde görünmeye başladı.');
    } catch (e) { setError(e instanceof Error ? e.message : 'İlan oluşturulamadı.'); } finally { setSubmitting(false); }
  };
  return <Page title={kind === 'lost' ? 'Kayıp ilanı ver' : 'Bulunan hayvan bildir'} subtitle="Bilgileri olabildiğince açık gir" onBack={onBack}>
    <Pressable onPress={choosePhoto} style={styles.photo}>{photo ? <Image source={{ uri: photo.uri }} style={styles.photoPreview} /> : <><Ionicons name="images-outline" size={30} color={colors.primary} /><Text style={styles.actionText}>Fotoğraf seç</Text><Text style={styles.muted}>JPG, PNG veya WebP · en fazla 8 MB</Text></>}</Pressable>
    <Field value={petName} onChangeText={setPetName} placeholder={kind === 'lost' ? 'Adı *' : 'Varsa bilinen adı *'} /><Field value={species} onChangeText={setSpecies} placeholder="Türü (Kedi, Köpek...) *" /><Field value={breed} onChangeText={setBreed} placeholder="Cinsi" />
    <Field value={features} onChangeText={setFeatures} placeholder="Ayırt edici özellikleri *" multiline /><Field value={eventAt} onChangeText={setEventAt} placeholder="Tarih ve saat: YYYY-MM-DDTHH:mm *" />
    <Field value={city} onChangeText={setCity} placeholder="İl *" /><Field value={district} onChangeText={setDistrict} placeholder="İlçe *" /><Field value={neighborhood} onChangeText={setNeighborhood} placeholder="Mahalle" />
    <Pressable onPress={useLocation} style={styles.locationButton}><Ionicons name={coords ? 'checkmark-circle' : 'locate-outline'} size={21} color="#4E7458" /><Text style={styles.locationText}>{coords ? 'Yaklaşık konum eklendi' : 'Telefonun yaklaşık konumunu ekle'}</Text></Pressable>
    <Field value={collar} onChangeText={setCollar} placeholder="Tasma / mikroçip bilgisi" /><Field value={notes} onChangeText={setNotes} placeholder="Önemli notlar" multiline />
    <Text style={styles.privacy}>Telefon numaranı veya açık ev adresini yazma.</Text>{error ? <Text style={styles.errorText}>{error}</Text> : null}
    <Primary label={submitting ? 'Gönderiliyor…' : 'İlanı yayınla'} icon="paper-plane-outline" onPress={() => { if (!submitting) void submit(); }} />
  </Page>;
}

function SightingForm({ id, token, onBack, onCreated }: { id: number; token: string; onBack: () => void; onCreated: () => void }) {
  const [location, setLocation] = useState(''); const [seenAt, setSeenAt] = useState(localDateTime()); const [note, setNote] = useState('');
  const [coords, setCoords] = useState<{ latitude: number; longitude: number }>(); const [sending, setSending] = useState(false); const [error, setError] = useState<string | null>(null);
  const capture = async () => { const p = await Location.requestForegroundPermissionsAsync(); if (p.status !== 'granted') return Alert.alert('Konum izni gerekli'); const result = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High }); setCoords(result.coords); };
  const submit = async () => { const date = new Date(seenAt); if (location.trim().length < 3 || Number.isNaN(date.getTime())) return setError('Konum ve geçerli görülme zamanı gereklidir.'); setSending(true); setError(null); try { await createLostPetSighting(token, id, { locationLabel: location.trim(), seenAt, note: note.trim(), latitude: coords?.latitude, longitude: coords?.longitude }); Alert.alert('Bildirim gönderildi', 'İlan sahibi yeni görülme bilgisini görebilecek.'); onCreated(); } catch (e) { setError(e instanceof Error ? e.message : 'Bildirim gönderilemedi.'); } finally { setSending(false); } };
  return <Page title="Burada gördüm" subtitle="Gözlemini ilan sahibine güvenle ilet" onBack={onBack}>
    <Field value={location} onChangeText={setLocation} placeholder="Yaklaşık konum *" /><Field value={seenAt} onChangeText={setSeenAt} placeholder="Görülme tarihi ve saati *" /><Field value={note} onChangeText={setNote} placeholder="Davranış, yön veya kısa not" multiline />
    <Pressable onPress={capture} style={styles.locationButton}><Ionicons name={coords ? 'checkmark-circle' : 'locate-outline'} size={21} color="#4E7458" /><Text style={styles.locationText}>{coords ? 'Kesin konum güvenle eklendi' : 'Telefon konumunu ekle'}</Text></Pressable>
    <Text style={styles.privacy}>Kesin koordinat yalnızca ilan sahibine gösterilir; diğer kullanıcılar yaklaşık konumu görür.</Text>{error ? <Text style={styles.errorText}>{error}</Text> : null}<Primary label={sending ? 'Gönderiliyor…' : 'Gözlemi gönder'} icon="notifications-outline" onPress={() => { if (!sending) void submit(); }} />
  </Page>;
}

function Field(props: { value: string; onChangeText: (value: string) => void; placeholder: string; multiline?: boolean }) {
  return <TextInput {...props} placeholderTextColor="#958991" style={[styles.input, props.multiline && styles.multiline]}
    inputAccessoryViewID={Platform.OS === 'ios' ? 'lost-pets-keyboard' : undefined}
    returnKeyType={props.multiline ? 'default' : 'next'} blurOnSubmit={!props.multiline} />;
}
function Primary({ label, icon, onPress }: { label: string; icon: keyof typeof Ionicons.glyphMap; onPress: () => void }) { return <Pressable onPress={onPress} style={({ pressed }) => [styles.primary, pressed && styles.pressed]}><Ionicons name={icon} size={20} color="#fff" /><Text style={styles.primaryText}>{label}</Text></Pressable>; }
function Info({ icon, text }: { icon: keyof typeof Ionicons.glyphMap; text: string }) { return <View style={styles.info}><Ionicons name={icon} size={20} color={colors.primary} /><Text style={styles.body}>{text}</Text></View>; }
function localDateTime() { const date = new Date(Date.now() - new Date().getTimezoneOffset() * 60000); return date.toISOString().slice(0, 16); }

const styles = createThemedStyles(() => ({
  page: { flex: 1, backgroundColor: colors.background }, content: { padding: 20, paddingTop: 54, paddingBottom: 130 }, flex: { flex: 1 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 14, marginBottom: 25 }, logo: { width: 54, height: 54, borderRadius: 27, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }, back: { width: 48, height: 48, borderRadius: 24, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  title: { fontSize: 34, lineHeight: 39, fontFamily: 'Georgia', fontWeight: '800', color: '#2D2529' }, subtitle: { fontSize: 15, color: '#746A70', marginTop: 2 },
  urgent: { flexDirection: 'row', alignItems: 'center', gap: 16, padding: 18, borderRadius: 25, borderWidth: 1.5, borderColor: '#EAA08D', backgroundColor: colors.peachSoft, marginBottom: 18 }, roundCoral: { width: 58, height: 58, borderRadius: 29, backgroundColor: '#B45446', alignItems: 'center', justifyContent: 'center' }, urgentTitle: { fontSize: 18, fontWeight: '800', color: '#2D2529', marginBottom: 4 }, muted: { color: '#7D7278', fontSize: 14, lineHeight: 20 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 }, action: { width: '48%', minHeight: 112, padding: 16, borderRadius: 22, borderWidth: 1, borderColor: '#E2DADC', backgroundColor: '#FFFCFA', justifyContent: 'space-between', ...shadow }, actionText: { fontSize: 16, fontWeight: '800', color: '#332B2F' }, pressed: { opacity: 0.7, transform: [{ scale: 0.99 }] },
  sectionRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 25, marginBottom: 13 }, sectionTitle: { fontSize: 24, fontFamily: 'Georgia', fontWeight: '800', color: '#332B2F' }, link: { color: colors.primary, fontWeight: '800', fontSize: 15, paddingVertical: 8 }, loader: { marginVertical: 40 },
  error: { padding: 18, borderRadius: 18, backgroundColor: colors.peachSoft, marginVertical: 18 }, errorText: { color: '#A3453C', fontSize: 15, lineHeight: 21, marginVertical: 8 }, empty: { alignItems: 'center', padding: 30, marginTop: 15, borderRadius: 24, backgroundColor: '#FFFCFA', borderWidth: 1, borderColor: '#E3DBDD' }, emptyTitle: { fontSize: 18, fontWeight: '800', color: '#3A3136', marginVertical: 8 },
  card: { flexDirection: 'row', gap: 13, alignItems: 'center', marginBottom: 13, padding: 12, borderRadius: 22, backgroundColor: '#FFFCFA', borderWidth: 1, borderColor: '#E2DADC', ...shadow }, cardImage: { width: 92, height: 100, borderRadius: 16, backgroundColor: '#EEE7E7' }, row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 }, cardTitle: { flex: 1, fontSize: 22, fontFamily: 'Georgia', fontWeight: '800', color: '#2D2529' }, badge: { paddingHorizontal: 9, paddingVertical: 5, borderRadius: 99, overflow: 'hidden', fontSize: 12, fontWeight: '800' }, lost: { backgroundColor: colors.peachSoft, color: '#9B463B' }, found: { backgroundColor: colors.sageSoft, color: '#41604A' }, meta: { color: '#62585E', fontSize: 13, marginTop: 3 },
  hero: { height: 260, borderRadius: 26, backgroundColor: '#EEE7E7', marginBottom: 16 }, detail: { padding: 19, borderRadius: 25, backgroundColor: '#FFFCFA', borderWidth: 1, borderColor: '#E2DADC' }, detailTitle: { fontFamily: 'Georgia', fontWeight: '800', fontSize: 28, color: '#2D2529' }, label: { fontSize: 15, fontWeight: '800', color: '#41363C', marginTop: 12 }, body: { flexShrink: 1, fontSize: 15, lineHeight: 22, color: '#50464C' }, info: { flexDirection: 'row', alignItems: 'center', gap: 9, marginTop: 10 }, timeline: { borderLeftWidth: 3, borderLeftColor: colors.lilacSoft, paddingLeft: 13, marginBottom: 10 }, privacy: { padding: 13, backgroundColor: colors.sageSoft, borderRadius: 14, color: '#4E6755', marginVertical: 15, lineHeight: 19 }, ownerActions: { marginTop: 12, borderTopWidth: 1, borderTopColor: '#E8E0E2', paddingTop: 10 }, danger: { color: '#AA413C', fontWeight: '800', paddingVertical: 10 },
  photo: { minHeight: 150, borderRadius: 23, borderStyle: 'dashed', borderWidth: 1.5, borderColor: '#CBB8D3', backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center', gap: 6, overflow: 'hidden', marginBottom: 14 }, photoPreview: { width: '100%', height: 230 }, input: { minHeight: 55, borderRadius: 17, borderWidth: 1, borderColor: '#DED6D8', backgroundColor: '#FFFCFA', paddingHorizontal: 16, marginBottom: 11, fontSize: 15, color: '#30272C' }, multiline: { minHeight: 105, textAlignVertical: 'top', paddingTop: 15 }, locationButton: { flexDirection: 'row', alignItems: 'center', gap: 9, padding: 15, borderRadius: 17, backgroundColor: colors.sageSoft, marginBottom: 11 }, locationText: { color: '#486352', fontWeight: '700' }, primary: { minHeight: 57, borderRadius: 17, backgroundColor: colors.primary, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, marginTop: 10 }, primaryText: { color: '#fff', fontWeight: '900', fontSize: 17 },
  keyboardBar: { minHeight: 46, backgroundColor: '#F6F1F3', borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: '#D8CED2', alignItems: 'flex-end', justifyContent: 'center', paddingHorizontal: 18 }, keyboardDone: { color: colors.primary, fontSize: 16, fontWeight: '800', paddingVertical: 8 },
}));
