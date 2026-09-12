import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Image,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { deletePet, getPets, mediaUrl, PetProfile, PetProfileRequest, savePet } from '../api';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

const petTypes = ['Kedi', 'Köpek', 'Kuş', 'Tavşan', 'Balık', 'Diğer'];
const genders = ['Dişi', 'Erkek', 'Belirtilmedi'];
const characterOptions = ['Sakin', 'Oyuncu', 'Enerjik', 'Uysal', 'Çekingen', 'Sosyal', 'Koruyucu', 'Bağımsız'];
const careOptions = ['Özel bakım ihtiyacı yok', 'Alerjisi var', 'Düzenli ilaç kullanıyor', 'Özel besleniyor', 'Hareket desteği gerekiyor'];
const compatibilityOptions = ['Uyumlu', 'Kontrollü tanıştırılmalı', 'Uyumsuz', 'Bilinmiyor'];
const tagOptions = ['Eğitimli', 'İnsan canlısı', 'Sessiz', 'Aktif', 'Kucak sever', 'Oyun sever', 'Bahçe sever', 'Ev yaşamına uygun'];
const emptyForm: PetProfileRequest = { name: '', type: 'Kedi', breed: '', age: null, gender: 'Belirtilmedi', description: '', character: '', careNotes: '', childCompatibility: '', otherPetCompatibility: '', isVaccinated: null, isNeutered: null, isMicrochipped: null, tags: [], extraAttributes: {} };

function normalizeAgeInput(value: string) {
  const normalized = value.replace(',', '.').replace(/[^0-9.]/g, '');
  const [whole = '', ...fractionParts] = normalized.split('.');
  const fraction = fractionParts.join('').slice(0, 1);
  return normalized.includes('.') ? `${whole.slice(0, 2)}.${fraction}` : whole.slice(0, 2);
}

function formatAge(age: number) {
  return Number.isInteger(age) ? String(age) : age.toFixed(1).replace('.', ',');
}

export function PetsScreen({ token, onBack }: { token: string; onBack: () => void }) {
  const [pets, setPets] = useState<PetProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<PetProfile | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try { setPets(await getPets(token)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Patiler yüklenemedi.'); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => { setEditing(null); setFormOpen(true); };
  const openEdit = (pet: PetProfile) => { setEditing(pet); setFormOpen(true); };
  const saved = (pet: PetProfile) => {
    setPets(current => editing ? current.map(item => item.id === pet.id ? pet : item) : [...current, pet].sort((a, b) => a.name.localeCompare(b.name, 'tr')));
    setFormOpen(false);
    setEditing(null);
  };

  const remove = (pet: PetProfile) => Alert.alert(
    `${pet.name} silinsin mi?`,
    'Bu pati profili hesabından kalıcı olarak kaldırılacak.',
    [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Sil', style: 'destructive', onPress: async () => {
        try { await deletePet(token, pet.id); setPets(current => current.filter(item => item.id !== pet.id)); }
        catch (reason) { Alert.alert('Silinemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.'); }
      } },
    ],
  );

  return <View style={styles.screen}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
      <View style={styles.header}>
        <Pressable onPress={onBack} accessibilityLabel="Hesabıma dön" style={styles.roundButton}><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable>
        <View style={styles.headerCopy}><Text style={styles.eyebrow}>EV ARKADAŞLARIN</Text><Text style={styles.title}>Patilerim</Text></View>
        <Pressable onPress={openCreate} accessibilityLabel="Yeni pati ekle" style={styles.addCircle}><Ionicons name="add" size={25} color={colors.white} /></Pressable>
      </View>

      <View style={styles.introCard}>
        <View style={styles.introIcon}><Ionicons name="paw" size={28} color={colors.primary} /></View>
        <View style={styles.flexOne}><Text style={styles.introTitle}>Her patiye özel bir profil</Text><Text style={styles.introText}>Bilgileri PatiMatch ve kişiselleştirilmiş içeriklerde kullanabileceksin.</Text></View>
      </View>

      {loading ? <View style={styles.state}><ActivityIndicator color={colors.primary} /><Text style={styles.stateText}>Patilerin yükleniyor…</Text></View> : null}
      {!loading && error ? <View style={styles.errorCard}><Ionicons name="cloud-offline-outline" size={25} color={colors.danger} /><Text style={styles.errorText}>{error}</Text><Pressable onPress={load} style={styles.retry}><Text style={styles.retryText}>Yeniden dene</Text></Pressable></View> : null}
      {!loading && !error && !pets.length ? <View style={styles.emptyCard}>
        <View style={styles.emptyIcon}><Ionicons name="paw-outline" size={42} color={colors.primary} /></View>
        <Text style={styles.emptyTitle}>Henüz bir pati eklemedin</Text>
        <Text style={styles.emptyText}>İlk profilini oluştur; adı, türü ve onu özel yapan küçük ayrıntılar hep yanında olsun.</Text>
        <Pressable onPress={openCreate} style={({ pressed }) => [styles.primaryButton, pressed && styles.pressed]}><Ionicons name="add-circle-outline" size={20} color={colors.white} /><Text style={styles.primaryText}>İlk patimi ekle</Text></Pressable>
      </View> : null}

      {!loading && !error && pets.map(pet => <PetCard key={pet.id} pet={pet} onEdit={() => openEdit(pet)} onDelete={() => remove(pet)} />)}

      {!loading && !error && pets.length ? <Pressable onPress={openCreate} style={({ pressed }) => [styles.outlineButton, pressed && styles.pressed]}><Ionicons name="add" size={20} color={colors.primary} /><Text style={styles.outlineText}>Yeni pati ekle</Text></Pressable> : null}
    </ScrollView>

    <PetFormModal visible={formOpen} token={token} pet={editing} onClose={() => setFormOpen(false)} onSaved={saved} />
  </View>;
}

function PetCard({ pet, onEdit, onDelete }: { pet: PetProfile; onEdit: () => void; onDelete: () => void }) {
  const icon = pet.type === 'Kuş' ? 'flame-outline' : pet.type === 'Balık' ? 'fish-outline' : pet.type === 'Tavşan' ? 'leaf-outline' : 'paw';
  const hasPhoto = Boolean(pet.profileImage && pet.profileImage !== 'img/pet-default.jpg');
  return <View style={styles.petCard}>
    <View style={styles.petTop}>
      <View style={styles.petAvatar}>{hasPhoto
        ? <Image source={{ uri: mediaUrl(pet.profileImage) }} style={styles.petAvatarImage} />
        : <Ionicons name={icon} size={30} color={colors.primary} />}</View>
      <View style={styles.flexOne}><Text style={styles.petName}>{pet.name}</Text><Text style={styles.petMeta}>{[pet.type, pet.breed, pet.age != null ? `${formatAge(pet.age)} yaş` : null].filter(Boolean).join(' · ')}</Text></View>
      <View style={styles.petActions}>
        <Pressable onPress={onEdit} accessibilityLabel={`${pet.name} profilini düzenle`} style={styles.iconButton}><Ionicons name="create-outline" size={19} color={colors.primary} /></Pressable>
        <Pressable onPress={onDelete} accessibilityLabel={`${pet.name} profilini sil`} style={styles.iconButton}><Ionicons name="trash-outline" size={18} color={colors.danger} /></Pressable>
      </View>
    </View>
    {pet.gender && pet.gender !== 'Belirtilmedi' ? <View style={styles.badge}><Ionicons name="heart-outline" size={13} color="#4E7458" /><Text style={styles.badgeText}>{pet.gender}</Text></View> : null}
    {pet.description ? <Text style={styles.description}>{pet.description}</Text> : <Text style={styles.descriptionMuted}>Bu pati için henüz bir tanıtım notu eklenmedi.</Text>}
    {pet.character ? <Text style={styles.detailLine}><Text style={styles.detailLabel}>Karakter: </Text>{pet.character}</Text> : null}
    <View style={styles.badges}>{pet.isVaccinated === true ? <InfoBadge text="Aşılı" /> : null}{pet.isNeutered === true ? <InfoBadge text="Kısırlaştırılmış" /> : null}{pet.isMicrochipped === true ? <InfoBadge text="Mikroçipli" /> : null}</View>
    {pet.tags?.length ? <View style={styles.badges}>{pet.tags.map(tag => <View key={tag} style={styles.tag}><Text style={styles.tagText}>#{tag}</Text></View>)}</View> : null}
  </View>;
}

function InfoBadge({ text }: { text: string }) { return <View style={styles.badge}><Ionicons name="checkmark-circle-outline" size={13} color="#4E7458" /><Text style={styles.badgeText}>{text}</Text></View>; }

function PetFormModal({ visible, token, pet, onClose, onSaved }: { visible: boolean; token: string; pet: PetProfile | null; onClose: () => void; onSaved: (pet: PetProfile) => void }) {
  const [form, setForm] = useState<PetProfileRequest>(emptyForm);
  const [ageText, setAgeText] = useState('');
  const [image, setImage] = useState<ImagePicker.ImagePickerAsset | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!visible) return;
    setForm(pet ? { name: pet.name, type: pet.type, breed: pet.breed || '', age: pet.age, gender: pet.gender || 'Belirtilmedi', description: pet.description || '', character: pet.character || '', careNotes: pet.careNotes || '', childCompatibility: pet.childCompatibility || '', otherPetCompatibility: pet.otherPetCompatibility || '', isVaccinated: pet.isVaccinated, isNeutered: pet.isNeutered, isMicrochipped: pet.isMicrochipped, tags: pet.tags || [], extraAttributes: pet.extraAttributes || {} } : emptyForm);
    setAgeText(pet?.age != null ? String(pet.age) : '');
    setImage(null);
  }, [visible, pet]);

  const pickImage = async () => {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      Alert.alert('Galeri izni gerekli', 'Pati fotoğrafı seçebilmek için Pet’im uygulamasına galeri erişimi vermelisin.');
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.82,
    });
    if (!result.canceled && result.assets[0]) setImage(result.assets[0]);
  };

  const submit = async () => {
    const name = form.name.trim();
    if (name.length < 2) return Alert.alert('Ad gerekli', 'Patinin adını en az 2 karakter olarak yaz.');
    const age = ageText.trim() ? Number(ageText.replace(',', '.')) : null;
    if (age !== null && (!Number.isFinite(age) || age < 0 || age > 80)) return Alert.alert('Yaşı kontrol et', 'Yaş 0 ile 80 arasında olmalı. Örneğin 1,5 yazabilirsin.');
    setSaving(true);
    try {
      onSaved(await savePet(token, {
        ...form,
        name,
        age,
        image: image ? { uri: image.uri, fileName: image.fileName, mimeType: image.mimeType } : undefined,
      }, pet?.id));
    }
    catch (reason) { Alert.alert('Kaydedilemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.'); }
    finally { setSaving(false); }
  };

  return <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose}>
    <KeyboardAvoidingView style={styles.modalScreen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.modalContent} keyboardShouldPersistTaps="handled">
        <View style={styles.modalHeader}><Pressable onPress={onClose} style={styles.modalClose}><Ionicons name="close" size={23} color={colors.primary} /></Pressable><Text style={styles.modalTitle}>{pet ? 'Patiyi düzenle' : 'Yeni pati ekle'}</Text><View style={styles.modalClose} /></View>
        <Pressable disabled={saving} onPress={() => void pickImage()} style={styles.photoPicker} accessibilityLabel="Pati fotoğrafı seç">
          {image || pet?.profileImage
            ? <Image source={{ uri: image?.uri || mediaUrl(pet?.profileImage) }} style={styles.photoPreview} />
            : <View style={styles.photoPlaceholder}><Ionicons name="camera-outline" size={31} color={colors.primary} /></View>}
          <View style={styles.photoAction}><Ionicons name="images-outline" size={18} color={colors.primary} /><Text style={styles.photoActionText}>{image || pet?.profileImage ? 'Fotoğrafı değiştir' : 'Pati fotoğrafı ekle'}</Text></View>
        </Pressable>
        <Text style={styles.label}>Adı *</Text><TextInput value={form.name} onChangeText={name => setForm(current => ({ ...current, name }))} placeholder="Örn. Luna" placeholderTextColor="#A09599" style={styles.input} maxLength={50} />
        <Text style={styles.label}>Türü *</Text><View style={styles.choices}>{petTypes.map(type => <Pressable key={type} onPress={() => setForm(current => ({ ...current, type }))} style={[styles.choice, form.type === type && styles.choiceActive]}><Text style={[styles.choiceText, form.type === type && styles.choiceTextActive]}>{type}</Text></Pressable>)}</View>
        <Text style={styles.label}>Irkı</Text><TextInput value={form.breed || ''} onChangeText={breed => setForm(current => ({ ...current, breed }))} placeholder="Örn. Golden Retriever" placeholderTextColor="#A09599" style={styles.input} maxLength={80} />
        <Text style={styles.label}>Yaşı</Text><TextInput value={ageText} onChangeText={value => setAgeText(normalizeAgeInput(value))} placeholder="Örn. 1,5" placeholderTextColor="#A09599" style={styles.input} keyboardType="decimal-pad" inputMode="decimal" />
        <Text style={styles.label}>Cinsiyeti</Text><View style={styles.choices}>{genders.map(gender => <Pressable key={gender} onPress={() => setForm(current => ({ ...current, gender }))} style={[styles.choice, form.gender === gender && styles.choiceActive]}><Text style={[styles.choiceText, form.gender === gender && styles.choiceTextActive]}>{gender}</Text></Pressable>)}</View>
        <Text style={styles.label}>Hakkında</Text><TextInput value={form.description || ''} onChangeText={description => setForm(current => ({ ...current, description }))} placeholder="Hikâyesi, sevdiği oyunlar ve onu özel yapan şeyler…" placeholderTextColor="#A09599" style={[styles.input, styles.textArea]} multiline maxLength={500} textAlignVertical="top" />
        <MultiChoice title="Karakteri" value={form.character || ''} options={characterOptions} onChange={character => setForm(current => ({ ...current, character }))} />
        <MultiChoice title="Sağlık / özel bakım" value={form.careNotes || ''} options={careOptions} onChange={careNotes => setForm(current => ({ ...current, careNotes }))} />
        <SingleChoice title="Çocuklarla uyumu" value={form.childCompatibility || ''} options={compatibilityOptions} onChange={childCompatibility => setForm(current => ({ ...current, childCompatibility }))} />
        <SingleChoice title="Diğer hayvanlarla uyumu" value={form.otherPetCompatibility || ''} options={compatibilityOptions} onChange={otherPetCompatibility => setForm(current => ({ ...current, otherPetCompatibility }))} />
        <OptionalBoolean title="Aşı durumu" value={form.isVaccinated} onChange={isVaccinated => setForm(current => ({ ...current, isVaccinated }))} />
        <OptionalBoolean title="Kısırlaştırma" value={form.isNeutered} onChange={isNeutered => setForm(current => ({ ...current, isNeutered }))} />
        <OptionalBoolean title="Mikroçip" value={form.isMicrochipped} onChange={isMicrochipped => setForm(current => ({ ...current, isMicrochipped }))} />
        <MultiChoice title="Etiketler" value={(form.tags || []).join(',')} options={tagOptions} onChange={value => setForm(current => ({ ...current, tags: value ? value.split(',') : [] }))} />
        {form.type === 'Kedi' ? <><Text style={styles.sectionLabel}>Kediye özel (opsiyonel)</Text><ExtraChoice label="Tüy uzunluğu" field="hairLength" form={form} setForm={setForm} options={['Kısa', 'Orta', 'Uzun']} /><ExtraChoice label="Kum kabı alışkanlığı" field="litterBoxHabit" form={form} setForm={setForm} options={['Alışkın', 'Eğitimde', 'Alışkın değil']} /><ExtraChoice label="Yaşam biçimi" field="indoorOutdoor" form={form} setForm={setForm} options={['Yalnızca evde', 'Ev ve dışarı', 'Çoğunlukla dışarıda']} /></> : null}
        {form.type === 'Köpek' ? <><Text style={styles.sectionLabel}>Köpeğe özel (opsiyonel)</Text><ExtraChoice label="Tuvalet alışkanlığı" field="toiletHabit" form={form} setForm={setForm} options={['Eğitimli', 'Eğitimde', 'Eğitimsiz']} /><ExtraChoice label="Tasma ile yürüme" field="leashWalking" form={form} setForm={setForm} options={['Uyumlu', 'Eğitimde', 'Zorlanıyor']} /><ExtraChoice label="Egzersiz ihtiyacı" field="exerciseNeed" form={form} setForm={setForm} options={['Düşük', 'Orta', 'Yüksek']} /></> : null}
        <Pressable disabled={saving} onPress={submit} style={({ pressed }) => [styles.saveButton, (pressed || saving) && styles.pressed]}>{saving ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="checkmark-circle-outline" size={21} color={colors.white} /><Text style={styles.saveText}>{pet ? 'Değişiklikleri kaydet' : 'Pati profilini oluştur'}</Text></>}</Pressable>
      </ScrollView>
    </KeyboardAvoidingView>
  </Modal>;
}

function OptionalBoolean({ title, value, onChange }: { title: string; value?: boolean | null; onChange: (value: boolean | null) => void }) {
  return <><Text style={styles.label}>{title}</Text><View style={styles.choices}>{([['Belirtilmedi', null], ['Evet', true], ['Hayır', false]] as const).map(([label, option]) => <Pressable key={label} onPress={() => onChange(option)} style={[styles.choice, value === option && styles.choiceActive]}><Text style={[styles.choiceText, value === option && styles.choiceTextActive]}>{label}</Text></Pressable>)}</View></>;
}

function SingleChoice({ title, value, options, onChange }: { title: string; value: string; options: string[]; onChange: (value: string) => void }) { return <><Text style={styles.label}>{title}</Text><View style={styles.choices}>{options.map(option => <Pressable key={option} onPress={() => onChange(value === option ? '' : option)} style={[styles.choice, value === option && styles.choiceActive]}><Text style={[styles.choiceText, value === option && styles.choiceTextActive]}>{option}</Text></Pressable>)}</View></>; }
function MultiChoice({ title, value, options, onChange }: { title: string; value: string; options: string[]; onChange: (value: string) => void }) { const selected = value.split(',').filter(Boolean); return <><Text style={styles.label}>{title}</Text><View style={styles.choices}>{options.map(option => { const active = selected.includes(option); return <Pressable key={option} onPress={() => onChange((active ? selected.filter(item => item !== option) : [...selected, option]).join(','))} style={[styles.choice, active && styles.choiceActive]}><Text style={[styles.choiceText, active && styles.choiceTextActive]}>{option}</Text></Pressable>; })}</View></>; }
function ExtraChoice({ label, field, form, setForm, options }: { label: string; field: string; form: PetProfileRequest; setForm: React.Dispatch<React.SetStateAction<PetProfileRequest>>; options: string[] }) { const value = form.extraAttributes?.[field] || ''; return <SingleChoice title={label} value={value} options={options} onChange={next => setForm(current => ({ ...current, extraAttributes: { ...(current.extraAttributes || {}), [field]: next } }))} />; }

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = createThemedStyles(() => ({
  screen: { flex: 1, backgroundColor: colors.background }, content: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingTop: Platform.OS === 'ios' ? 56 : 28, paddingHorizontal: 20, paddingBottom: 112 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, headerCopy: { alignItems: 'center' }, eyebrow: { color: colors.peach, fontSize: 9, fontWeight: '900', letterSpacing: 1.8 }, title: { color: colors.text, fontFamily: serif, fontSize: 27, fontWeight: '700', marginTop: 2 },
  roundButton: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center', ...shadow }, addCircle: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', ...shadow },
  introCard: { flexDirection: 'row', alignItems: 'center', gap: 13, backgroundColor: colors.sageSoft, borderRadius: 21, padding: 16, marginTop: 22 }, introIcon: { width: 48, height: 48, borderRadius: 24, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center' }, introTitle: { color: colors.text, fontSize: 14, fontWeight: '900' }, introText: { color: '#56635A', fontSize: 10, lineHeight: 15, marginTop: 4 },
  state: { minHeight: 240, alignItems: 'center', justifyContent: 'center', gap: 11 }, stateText: { color: colors.muted, fontSize: 12 }, errorCard: { alignItems: 'center', gap: 10, backgroundColor: colors.peachSoft, borderRadius: 22, padding: 24, marginTop: 20 }, errorText: { color: colors.text, textAlign: 'center', lineHeight: 19 }, retry: { backgroundColor: colors.card, borderRadius: 15, paddingHorizontal: 16, paddingVertical: 10 }, retryText: { color: colors.primary, fontWeight: '800' },
  emptyCard: { alignItems: 'center', backgroundColor: colors.card, borderRadius: 26, padding: 28, marginTop: 18, borderWidth: 1, borderColor: colors.border, ...shadow }, emptyIcon: { width: 82, height: 82, borderRadius: 41, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, emptyTitle: { color: colors.text, fontFamily: serif, fontSize: 21, fontWeight: '700', marginTop: 18 }, emptyText: { color: colors.muted, fontSize: 11, lineHeight: 17, textAlign: 'center', marginTop: 7 },
  primaryButton: { minHeight: 50, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: colors.primary, borderRadius: 17, paddingHorizontal: 22, marginTop: 20 }, primaryText: { color: colors.white, fontSize: 12, fontWeight: '900' },
  petCard: { backgroundColor: colors.card, borderRadius: 24, padding: 17, marginTop: 15, borderWidth: 1, borderColor: colors.border, ...shadow }, petTop: { flexDirection: 'row', alignItems: 'center', gap: 12 }, petAvatar: { width: 58, height: 58, borderRadius: 29, overflow: 'hidden', backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, petAvatarImage: { width: '100%', height: '100%' }, petName: { color: colors.text, fontFamily: serif, fontSize: 21, fontWeight: '700' }, petMeta: { color: colors.muted, fontSize: 10, marginTop: 4 }, petActions: { flexDirection: 'row', gap: 5 }, iconButton: { width: 37, height: 37, borderRadius: 19, backgroundColor: colors.background, alignItems: 'center', justifyContent: 'center' }, badge: { alignSelf: 'flex-start', flexDirection: 'row', gap: 5, alignItems: 'center', backgroundColor: colors.sageSoft, borderRadius: 13, paddingHorizontal: 9, paddingVertical: 5, marginTop: 13 }, badgeText: { color: '#4E7458', fontSize: 9, fontWeight: '800' }, badges: { flexDirection: 'row', flexWrap: 'wrap', gap: 7 }, tag: { backgroundColor: colors.lilacSoft, borderRadius: 12, paddingHorizontal: 9, paddingVertical: 5, marginTop: 10 }, tagText: { color: colors.primary, fontSize: 9, fontWeight: '700' }, description: { color: colors.text, fontSize: 11, lineHeight: 17, marginTop: 11 }, descriptionMuted: { color: colors.muted, fontSize: 10, fontStyle: 'italic', marginTop: 11 }, detailLine: { color: colors.text, fontSize: 10, lineHeight: 16, marginTop: 9 }, detailLabel: { fontWeight: '900' },
  outlineButton: { minHeight: 50, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7, borderWidth: 1.3, borderColor: colors.primary, borderRadius: 17, marginTop: 18 }, outlineText: { color: colors.primary, fontWeight: '900', fontSize: 12 },
  modalScreen: { flex: 1, backgroundColor: colors.background }, modalContent: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingHorizontal: 20, paddingTop: Platform.OS === 'ios' ? 22 : 28, paddingBottom: 40 }, modalHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 20 }, modalClose: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, modalTitle: { color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' }, photoPicker: { alignItems: 'center', gap: 10, marginBottom: 5 }, photoPreview: { width: 128, height: 128, borderRadius: 64, backgroundColor: colors.lilacSoft }, photoPlaceholder: { width: 128, height: 128, borderRadius: 64, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft, borderWidth: 1, borderColor: colors.border }, photoAction: { flexDirection: 'row', alignItems: 'center', gap: 6 }, photoActionText: { color: colors.primary, fontSize: 12, fontWeight: '900' }, label: { color: colors.text, fontSize: 11, fontWeight: '900', marginTop: 14, marginBottom: 7 }, sectionLabel: { color: colors.primary, fontFamily: serif, fontSize: 18, fontWeight: '700', marginTop: 25, paddingTop: 18, borderTopWidth: 1, borderTopColor: colors.border }, input: { minHeight: 51, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 16, paddingHorizontal: 15, color: colors.text, fontSize: 13 }, textArea: { minHeight: 105, paddingTop: 14 }, choices: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 }, choice: { paddingHorizontal: 14, paddingVertical: 10, borderRadius: 15, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }, choiceActive: { backgroundColor: colors.primary, borderColor: colors.primary }, choiceText: { color: colors.muted, fontSize: 11, fontWeight: '700' }, choiceTextActive: { color: colors.white }, saveButton: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: colors.primary, borderRadius: 18, marginTop: 24 }, saveText: { color: colors.white, fontWeight: '900', fontSize: 13 },
  flexOne: { flex: 1 }, pressed: { opacity: 0.76, transform: [{ scale: 0.99 }] },
}));
