import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Alert, Image, Keyboard, KeyboardAvoidingView, Modal, Platform, Pressable,
  RefreshControl, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import {
  AdoptionApplication, AdoptionListing, applyToAdoptionListing, createAdoptionListing, getAdoptionApplications,
  getAdoptionListings, getMobileContactSettings, mediaUrl, reportAdoptionListing, updateAdoptionApplication, updateAdoptionListingStatus,
} from '../api';
import { ListingContactDetails, ListingContactMethodPicker, type ListingContactSelection } from '../components/ListingContactMethods';
import { colors, createThemedStyles, shadow } from '../theme';

type Props = { token: string | null; onLogin: () => void; onBack: () => void; initialListingId?: number | null };
type Action = { kind: 'apply' | 'report'; listing: AdoptionListing } | null;

export function AdoptionScreen({ token, onLogin, onBack, initialListingId }: Props) {
  const [items, setItems] = useState<AdoptionListing[]>([]);
  const [loading, setLoading] = useState(true); const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null); const [creating, setCreating] = useState(false);
  const [action, setAction] = useState<Action>(null); const [actionText, setActionText] = useState('');
  const [submitting, setSubmitting] = useState(false); const [applicationsFor, setApplicationsFor] = useState<number | null>(null);
  const [applications, setApplications] = useState<AdoptionApplication[]>([]);

  const load = useCallback(async (refresh = false) => {
    refresh ? setRefreshing(true) : setLoading(true); setError(null);
    try { setItems(await getAdoptionListings(token)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'İlanlar yüklenemedi.'); }
    finally { setLoading(false); setRefreshing(false); }
  }, [token]);
  useEffect(() => { void load(); }, [load]);

  const requireLogin = () => { if (token) return true; Alert.alert('Giriş gerekli', 'Bu işlem için giriş yapmalısın.', [{ text: 'Vazgeç' }, { text: 'Giriş yap', onPress: onLogin }]); return false; };
  const openAction = (kind: 'apply' | 'report', listing: AdoptionListing) => { if (!requireLogin()) return; setActionText(''); setAction({ kind, listing }); };
  const submitAction = async () => {
    if (!action || !token) return;
    if (actionText.trim().length < (action.kind === 'apply' ? 10 : 3)) { Alert.alert('Eksik bilgi', action.kind === 'apply' ? 'Kendini ve bakım koşullarını en az 10 karakterle anlat.' : 'Bildirim nedenini yaz.'); return; }
    setSubmitting(true);
    try {
      if (action.kind === 'apply') {
        await applyToAdoptionListing(token, action.listing.id, actionText.trim());
        setItems(current => current.map(item => item.id === action.listing.id ? { ...item, hasApplied: true, applicationStatus: 'pending' } : item));
        Alert.alert('Başvuru gönderildi', 'İlan sahibi başvurunu uygulama içinden değerlendirebilir.');
      } else Alert.alert('Teşekkürler', await reportAdoptionListing(token, action.listing.id, actionText.trim()));
      setAction(null);
    } catch (reason) { Alert.alert('İşlem tamamlanamadı', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); }
    finally { setSubmitting(false); }
  };
  const openApplications = async (listing: AdoptionListing) => {
    if (!token) return; setApplicationsFor(listing.id); setApplications([]);
    try { setApplications(await getAdoptionApplications(token, listing.id)); }
    catch (reason) { setApplicationsFor(null); Alert.alert('Başvurular alınamadı', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); }
  };
  const decide = async (applicationId: number, status: 'accepted' | 'rejected') => {
    if (!token) return;
    try {
      await updateAdoptionApplication(token, applicationId, status);
      setApplications(current => current.map(item => item.id === applicationId ? { ...item, status }
        : status === 'accepted' && item.status === 'pending' ? { ...item, status: 'rejected' } : item));
      if (status === 'accepted' && applicationsFor) {
        setItems(current => current.map(item => item.id === applicationsFor ? { ...item, status: 'adopted' } : item));
      }
    }
    catch (reason) { Alert.alert('İşlem tamamlanamadı', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); }
  };
  const closeListing = (listing: AdoptionListing) => {
    if (!token) return;
    Alert.alert('İlanı kapat', `${listing.petName} sahiplendirildi olarak işaretlensin mi?`, [{ text: 'Vazgeç' }, { text: 'Kapat', onPress: async () => {
      try { await updateAdoptionListingStatus(token, listing.id, 'adopted'); setItems(current => current.filter(item => item.id !== listing.id)); }
      catch (reason) { Alert.alert('İlan kapatılamadı', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); }
    } }]);
  };

  return <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.content} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} tintColor={colors.primary} />}>
      <Header title="Sahiplendirme" subtitle="Satın alma, sahiplen" onBack={onBack} />
      <View style={styles.notice}><Ionicons name="shield-checkmark-outline" size={23} color="#4E7458" /><Text style={styles.noticeText}>Pet'imde sahiplendirmeler tamamen ücretsizdir. Para talep edilirse Ayarlar → Sorun Bildir ile bildirin.</Text></View>
      <Pressable style={styles.primaryButton} onPress={() => { if (requireLogin()) setCreating(value => !value); }}><Ionicons name={creating ? 'close' : 'add-circle-outline'} size={20} color="#fff" /><Text style={styles.primaryText}>{creating ? 'Formu kapat' : 'Sahiplendirme ilanı ver'}</Text></Pressable>
      {creating && token ? <CreateListingForm token={token} onCreated={item => { setItems(current => [item, ...current]); setCreating(false); }} /> : null}
      <Text style={styles.sectionTitle}>Yeni yuva arayanlar</Text>
      {loading ? <State icon="hourglass-outline" text="İlanlar yükleniyor…" loading /> : null}
      {!loading && error ? <Pressable onPress={() => void load()}><State icon="cloud-offline-outline" text={`${error}\nTekrar denemek için dokun.`} /></Pressable> : null}
      {!loading && !error && items.length === 0 ? <State icon="home-outline" text="Henüz aktif sahiplendirme ilanı yok." /> : null}
      {items.filter(item => !initialListingId || item.id === initialListingId).map(item => <View key={item.id} style={styles.card}>
        <Image source={{ uri: mediaUrl(item.imagePath) }} style={styles.hero} />
        <View style={styles.cardBody}><View style={styles.row}><Text style={styles.petName}>{item.petName}</Text><Text style={styles.city}>{item.city}</Text></View>
          <Text style={styles.meta}>{[item.ageYears !== null && item.ageYears !== undefined ? `${item.ageYears} yaş` : null, item.breed || item.species, item.gender].filter(Boolean).join(' · ')}</Text>
          <Text style={styles.owner}>@{item.ownerUsername} · {new Date(item.createdAt).toLocaleDateString('tr-TR')}</Text>
          <Text style={styles.label}>Sağlık ve bakım bilgisi</Text><Text style={styles.body}>{item.healthInfo}</Text>
          <Text style={styles.label}>{item.petName}'ın hikâyesi</Text><Text style={styles.body}>{item.story}</Text>
          <ListingContactDetails allowInAppMessages={item.allowInAppMessages} phone={item.contactPhone} email={item.contactEmail} inAppText="Sahiplenme başvurusunu uygulama içinden gönderebilirsin." />
          {item.isMine ? <><Pressable style={styles.outlineButton} onPress={() => void openApplications(item)}><Text style={styles.outlineText}>Başvuruları gör</Text></Pressable>{item.status === 'active' ? <Pressable onPress={() => closeListing(item)}><Text style={styles.dangerText}>Sahiplendirildi olarak kapat</Text></Pressable> : <Text style={styles.owner}>İlan durumu: Sahiplendirildi</Text>}</>
            : item.allowInAppMessages ? <Pressable disabled={item.hasApplied || item.status !== 'active'} style={[styles.primaryButton, (item.hasApplied || item.status !== 'active') && styles.disabled]} onPress={() => openAction('apply', item)}><Ionicons name="heart-outline" size={19} color="#fff" /><Text style={styles.primaryText}>{item.applicationStatus === 'accepted' ? 'Başvurun kabul edildi' : item.applicationStatus === 'rejected' ? 'Başvurun reddedildi' : item.hasApplied ? 'Başvurun bekliyor' : item.status === 'adopted' ? 'Sahiplendirildi' : 'Sahiplenme başvurusu yap'}</Text></Pressable> : null}
          <View style={styles.actions}><Pressable onPress={() => Alert.alert('Güvenli sahiplendirme', 'Hayvanı ve yaşam alanını yüz yüze gör. Kimlik, adres veya ödeme bilgilerini aceleyle paylaşma. Ücret isteyen ya da şüpheli davranan ilanları bildir.')}><Text style={styles.link}>Güvenlik önerileri</Text></Pressable>{!item.isMine ? <Pressable onPress={() => openAction('report', item)}><Text style={styles.report}>İlanı bildir</Text></Pressable> : null}</View>
        </View>
      </View>)}
    </ScrollView>
    <TextActionModal visible={Boolean(action)} title={action?.kind === 'apply' ? 'Sahiplenme başvurusu' : 'İlanı bildir'} placeholder={action?.kind === 'apply' ? 'Bakım koşullarını ve neden sahiplenmek istediğini anlat…' : 'Bildirim nedenini yaz…'} value={actionText} onChange={setActionText} onClose={() => setAction(null)} onSubmit={() => void submitAction()} busy={submitting} />
    <ApplicationsModal visible={applicationsFor !== null} items={applications} onClose={() => setApplicationsFor(null)} onDecide={decide} />
  </KeyboardAvoidingView>;
}

function CreateListingForm({ token, onCreated }: { token: string; onCreated: (item: AdoptionListing) => void }) {
  const [photo, setPhoto] = useState<ImagePicker.ImagePickerAsset | null>(null); const [busy, setBusy] = useState(false);
  const [petName, setPetName] = useState(''); const [species, setSpecies] = useState(''); const [breed, setBreed] = useState('');
  const [age, setAge] = useState(''); const [gender, setGender] = useState(''); const [city, setCity] = useState(''); const [district, setDistrict] = useState('');
  const [healthInfo, setHealthInfo] = useState(''); const [story, setStory] = useState('');
  const [contactSettings, setContactSettings] = useState<Awaited<ReturnType<typeof getMobileContactSettings>> | null>(null);
  const [contactMethods, setContactMethods] = useState<ListingContactSelection>({ allowInAppMessages: true, sharePhone: false, shareEmail: false });
  useEffect(() => { getMobileContactSettings(token).then(setContactSettings).catch(() => setContactSettings(null)); }, [token]);
  const pick = async () => { const permission = await ImagePicker.requestMediaLibraryPermissionsAsync(); if (!permission.granted) { Alert.alert('Fotoğraf izni gerekli', 'İlan fotoğrafı seçebilmek için galeri izni ver.'); return; } const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], allowsEditing: true, aspect: [4, 3], quality: 0.85 }); if (!result.canceled) setPhoto(result.assets[0]); };
  const submit = async () => {
    Keyboard.dismiss();
    if (!photo || !petName.trim() || species.trim().length < 2 || city.trim().length < 2 || healthInfo.trim().length < 5 || story.trim().length < 10) { Alert.alert('Eksik bilgi', 'Fotoğraf, isim, tür, şehir, sağlık bilgisi ve hikâye alanlarını doldur.'); return; }
    if (!contactMethods.allowInAppMessages && !contactMethods.sharePhone && !contactMethods.shareEmail) { Alert.alert('İletişim yöntemi gerekli', 'En az bir iletişim yöntemi seçmelisin.'); return; }
    const ageYears = age.trim() ? Number(age) : undefined; if (ageYears !== undefined && (!Number.isInteger(ageYears) || ageYears < 0 || ageYears > 40)) { Alert.alert('Yaş geçersiz', 'Yaşı 0–40 arasında tam sayı olarak gir.'); return; }
    setBusy(true); try { onCreated(await createAdoptionListing(token, { petName, species, breed, ageYears, gender, city, district, healthInfo, story, ...contactMethods, image: { uri: photo.uri, mimeType: photo.mimeType } })); Alert.alert('İlan yayınlandı', 'Sahiplendirme ilanı sunucuya kaydedildi.'); }
    catch (reason) { Alert.alert('İlan oluşturulamadı', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); } finally { setBusy(false); }
  };
  return <View style={styles.form}><Text style={styles.formTitle}>Yeni sahiplendirme ilanı</Text><Pressable style={styles.photoPicker} onPress={() => void pick()}>{photo ? <Image source={{ uri: photo.uri }} style={styles.photoPreview} /> : <><Ionicons name="images-outline" size={26} color={colors.primary} /><Text style={styles.link}>Galeriden fotoğraf ekle</Text></>}</Pressable>
    <Field value={petName} onChange={setPetName} placeholder="Pati adı *" /><Field value={species} onChange={setSpecies} placeholder="Tür (kedi, köpek…) *" /><Field value={breed} onChange={setBreed} placeholder="Irk" />
    <View style={styles.two}><View style={styles.flex}><Field value={age} onChange={setAge} placeholder="Yaş" keyboardType="number-pad" /></View><View style={styles.flex}><Field value={gender} onChange={setGender} placeholder="Cinsiyet" /></View></View>
    <View style={styles.two}><View style={styles.flex}><Field value={city} onChange={setCity} placeholder="Şehir *" /></View><View style={styles.flex}><Field value={district} onChange={setDistrict} placeholder="İlçe" /></View></View>
    <Field value={healthInfo} onChange={setHealthInfo} placeholder="Sağlık ve bakım bilgisi *" multiline /><Field value={story} onChange={setStory} placeholder="Hikâyesi ve aradığın yuva *" multiline />
    <ListingContactMethodPicker context="adoption" settings={contactSettings} value={contactMethods} onChange={setContactMethods} />
    <Pressable disabled={busy} style={[styles.primaryButton, busy && styles.disabled]} onPress={() => void submit()}>{busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>İlanı yayınla</Text>}</Pressable>
  </View>;
}

function TextActionModal({ visible, title, placeholder, value, onChange, onClose, onSubmit, busy }: { visible: boolean; title: string; placeholder: string; value: string; onChange: (v: string) => void; onClose: () => void; onSubmit: () => void; busy: boolean }) {
  return <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}><KeyboardAvoidingView style={styles.overlay} behavior={Platform.OS === 'ios' ? 'padding' : undefined}><Pressable style={styles.backdrop} onPress={Keyboard.dismiss}><View style={styles.modal}><Text style={styles.modalTitle}>{title}</Text><TextInput value={value} onChangeText={onChange} placeholder={placeholder} placeholderTextColor="#958991" multiline maxLength={1000} style={styles.modalInput} /><View style={styles.two}><Pressable style={[styles.outlineButton, styles.flex]} onPress={onClose}><Text style={styles.outlineText}>Vazgeç</Text></Pressable><Pressable disabled={busy} style={[styles.primaryButton, styles.flex, busy && styles.disabled]} onPress={onSubmit}>{busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>Gönder</Text>}</Pressable></View></View></Pressable></KeyboardAvoidingView></Modal>;
}

function ApplicationsModal({ visible, items, onClose, onDecide }: { visible: boolean; items: AdoptionApplication[]; onClose: () => void; onDecide: (id: number, status: 'accepted' | 'rejected') => void }) {
  return <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}><View style={styles.overlay}><View style={styles.appModal}><View style={styles.row}><Text style={styles.modalTitle}>Başvurular</Text><Pressable onPress={onClose}><Ionicons name="close" size={28} color={colors.primary} /></Pressable></View><ScrollView>{items.length === 0 ? <Text style={styles.body}>Henüz başvuru yok.</Text> : items.map(item => <View key={item.id} style={styles.application}><Text style={styles.label}>@{item.username}</Text><Text style={styles.body}>{item.message}</Text><Text style={styles.owner}>Durum: {item.status === 'pending' ? 'Bekliyor' : item.status === 'accepted' ? 'Kabul edildi' : 'Reddedildi'}</Text>{item.status === 'pending' ? <View style={styles.two}><Pressable style={[styles.outlineButton, styles.flex]} onPress={() => onDecide(item.id, 'rejected')}><Text style={styles.report}>Reddet</Text></Pressable><Pressable style={[styles.primaryButton, styles.flex]} onPress={() => onDecide(item.id, 'accepted')}><Text style={styles.primaryText}>Kabul et</Text></Pressable></View> : null}</View>)}</ScrollView></View></View></Modal>;
}

function Header({ title, subtitle, onBack }: { title: string; subtitle: string; onBack: () => void }) { return <View style={styles.header}><Pressable style={styles.back} onPress={onBack}><Ionicons name="arrow-back" size={30} color={colors.primary} /></Pressable><View><Text style={styles.title}>{title}</Text><Text style={styles.subtitle}>{subtitle}</Text></View></View>; }
function Field({ value, onChange, placeholder, multiline, keyboardType }: { value: string; onChange: (v: string) => void; placeholder: string; multiline?: boolean; keyboardType?: 'default' | 'number-pad' }) { return <TextInput value={value} onChangeText={onChange} placeholder={placeholder} placeholderTextColor="#958991" multiline={multiline} keyboardType={keyboardType} maxLength={multiline ? 2000 : 150} style={[styles.input, multiline && styles.multiline]} />; }
function State({ icon, text, loading }: { icon: keyof typeof Ionicons.glyphMap; text: string; loading?: boolean }) { return <View style={styles.state}>{loading ? <ActivityIndicator color={colors.primary} /> : <Ionicons name={icon} size={30} color={colors.primary} />}<Text style={styles.body}>{text}</Text></View>; }

const styles = createThemedStyles(() => ({
  page: { flex: 1, backgroundColor: '#FFF9F3' }, content: { padding: 24, paddingTop: 62, paddingBottom: 130 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 16, marginBottom: 24 }, back: { width: 54, height: 54, borderRadius: 27, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  title: { fontSize: 37, fontWeight: '800', color: '#30262B' }, subtitle: { fontSize: 16, color: '#776D72', marginTop: 2 },
  notice: { flexDirection: 'row', gap: 12, backgroundColor: colors.sageSoft, padding: 16, borderRadius: 20, marginBottom: 16 }, noticeText: { flex: 1, color: '#415D48', lineHeight: 21 },
  primaryButton: { minHeight: 50, borderRadius: 16, backgroundColor: colors.primary, flexDirection: 'row', gap: 8, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 16, marginTop: 12 }, primaryText: { color: '#fff', fontWeight: '800', fontSize: 16 }, disabled: { opacity: 0.5 },
  sectionTitle: { fontSize: 28, fontWeight: '800', color: '#30262B', marginTop: 28, marginBottom: 12 }, state: { padding: 28, alignItems: 'center', gap: 12, backgroundColor: '#fff', borderRadius: 20 },
  card: { backgroundColor: '#fff', borderRadius: 24, overflow: 'hidden', marginBottom: 20, ...shadow }, hero: { width: '100%', height: 220 }, cardBody: { padding: 18 }, row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  petName: { fontSize: 29, fontWeight: '800', color: '#30262B' }, city: { color: colors.primary, backgroundColor: colors.peachSoft, paddingHorizontal: 12, paddingVertical: 6, borderRadius: 14, overflow: 'hidden', fontWeight: '700' },
  meta: { fontSize: 17, color: '#6F656A', marginTop: 4 }, owner: { color: '#91868C', marginTop: 5 }, label: { color: '#3D3036', fontWeight: '800', fontSize: 16, marginTop: 14 }, body: { color: '#675D62', lineHeight: 22, marginTop: 5 },
  outlineButton: { minHeight: 48, borderWidth: 1.5, borderColor: colors.primary, borderRadius: 16, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 14, marginTop: 12 }, outlineText: { color: colors.primary, fontWeight: '800' }, dangerText: { color: '#A44E45', fontWeight: '800', textAlign: 'center', padding: 14 },
  actions: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 16 }, link: { color: colors.primary, fontWeight: '800' }, report: { color: '#A44E45', fontWeight: '800' },
  form: { backgroundColor: '#fff', borderRadius: 22, padding: 16, marginTop: 16, ...shadow }, formTitle: { fontSize: 21, fontWeight: '800', color: '#30262B', marginBottom: 10 }, input: { minHeight: 50, borderWidth: 1, borderColor: '#E1D8D3', borderRadius: 15, paddingHorizontal: 14, fontSize: 16, marginTop: 10, backgroundColor: '#FFFCF9' }, multiline: { minHeight: 100, paddingTop: 13, textAlignVertical: 'top' },
  photoPicker: { borderWidth: 1.5, borderStyle: 'dashed', borderColor: '#C9AED3', borderRadius: 16, minHeight: 90, alignItems: 'center', justifyContent: 'center', gap: 7, overflow: 'hidden' }, photoPreview: { width: '100%', height: 170 }, two: { flexDirection: 'row', gap: 10 }, flex: { flex: 1 },
  overlay: { flex: 1, backgroundColor: '#0007', justifyContent: 'flex-end' }, backdrop: { flex: 1, justifyContent: 'flex-end' }, modal: { backgroundColor: '#FFF9F3', padding: 22, borderTopLeftRadius: 28, borderTopRightRadius: 28 }, modalTitle: { fontSize: 23, fontWeight: '800', color: '#30262B' }, modalInput: { minHeight: 130, borderWidth: 1, borderColor: '#DED4D0', borderRadius: 18, padding: 14, marginTop: 14, backgroundColor: '#fff', textAlignVertical: 'top' },
  appModal: { backgroundColor: '#FFF9F3', maxHeight: '78%', padding: 22, borderTopLeftRadius: 28, borderTopRightRadius: 28 }, application: { backgroundColor: '#fff', borderRadius: 17, padding: 14, marginTop: 12 },
}));
