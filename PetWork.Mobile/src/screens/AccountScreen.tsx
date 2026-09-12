import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import * as ImagePicker from 'expo-image-picker';
import { File } from 'expo-file-system';
import { ActivityIndicator, Alert, Image, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, useWindowDimensions, View } from 'react-native';
import { changePassword, deleteAccount, getCurrentUser, mediaUrl, updateProfile, updateUsername, type AuthResponse, type CurrentUser } from '../api';
import { BrandMark } from '../components/BrandMark';
import { saveSession, updateStoredUsername } from '../session';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

type Props = { username: string; token: string; onBack: () => void; onOpenPets: () => void; onOpenContactSettings: () => void; onLogout: () => void; onSessionChanged: (session: AuthResponse) => void; onUsernameChanged: (username: string) => void; onDeleted: () => void };
const cities = ['Adana', 'Adıyaman', 'Afyonkarahisar', 'Ağrı', 'Aksaray', 'Amasya', 'Ankara', 'Antalya', 'Ardahan', 'Artvin', 'Aydın', 'Balıkesir', 'Bartın', 'Batman', 'Bayburt', 'Bilecik', 'Bingöl', 'Bitlis', 'Bolu', 'Burdur', 'Bursa', 'Çanakkale', 'Çankırı', 'Çorum', 'Denizli', 'Diyarbakır', 'Düzce', 'Edirne', 'Elazığ', 'Erzincan', 'Erzurum', 'Eskişehir', 'Gaziantep', 'Giresun', 'Gümüşhane', 'Hakkâri', 'Hatay', 'Iğdır', 'Isparta', 'İstanbul', 'İzmir', 'Kahramanmaraş', 'Karabük', 'Karaman', 'Kars', 'Kastamonu', 'Kayseri', 'Kilis', 'Kırıkkale', 'Kırklareli', 'Kırşehir', 'Kocaeli', 'Konya', 'Kütahya', 'Malatya', 'Manisa', 'Mardin', 'Mersin', 'Muğla', 'Muş', 'Nevşehir', 'Niğde', 'Ordu', 'Osmaniye', 'Rize', 'Sakarya', 'Samsun', 'Siirt', 'Sinop', 'Sivas', 'Şanlıurfa', 'Şırnak', 'Tekirdağ', 'Tokat', 'Trabzon', 'Tunceli', 'Uşak', 'Van', 'Yalova', 'Yozgat', 'Zonguldak'];
const occupations = ['Öğrenci', 'Yazılımcı', 'Mühendis', 'Öğretmen', 'Doktor', 'Veteriner hekim', 'Hemşire', 'Psikolog', 'Avukat', 'Mimar', 'Tasarımcı', 'Akademisyen', 'Muhasebeci', 'Satış uzmanı', 'Pazarlama uzmanı', 'İnsan kaynakları uzmanı', 'Kamu çalışanı', 'Esnaf', 'Serbest meslek', 'Ev çalışanı', 'Emekli', 'Çalışmıyor', 'Diğer'];

export function AccountScreen({ username, token, onBack, onOpenPets, onOpenContactSettings, onLogout, onSessionChanged, onUsernameChanged, onDeleted }: Props) {
  const [dialog, setDialog] = useState<'profile' | 'username' | 'password' | 'delete' | null>(null);
  const [profile, setProfile] = useState<CurrentUser | null>(null);
  const [profileImage, setProfileImage] = useState<ImagePicker.ImagePickerAsset | null>(null);
  const [removeProfileImage, setRemoveProfileImage] = useState(false);
  const [usernameInput, setUsernameInput] = useState(username);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [deletePhrase, setDeletePhrase] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { void getCurrentUser(token).then(setProfile).catch(() => undefined); }, [token]);
  const clearForm = () => { setUsernameInput(username); setCurrentPassword(''); setNewPassword(''); setConfirmPassword(''); setDeletePhrase(''); setError(null); };
  const closeDialog = () => { if (!busy) { setDialog(null); clearForm(); } };

  const submitUsername = async () => {
    setError(null);
    const nextUsername = usernameInput.trim();
    if (nextUsername.length < 3 || nextUsername.length > 50) return setError('Kullanıcı adı 3-50 karakter arasında olmalıdır.');
    if (!/^[\p{L}\p{N}._-]+$/u.test(nextUsername)) return setError('Kullanıcı adı yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.');
    setBusy(true);
    try {
      const user = await updateUsername(token, nextUsername);
      await updateStoredUsername(user.username);
      onUsernameChanged(user.username);
      setDialog(null);
      Alert.alert('Kullanıcı adı güncellendi', `Yeni kullanıcı adın: ${user.username}`);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Kullanıcı adı güncellenemedi.'); }
    finally { setBusy(false); }
  };

  const submitPassword = async () => {
    setError(null);
    if (!currentPassword || !newPassword || !confirmPassword) return setError('Tüm şifre alanlarını doldurmalısın.');
    if (newPassword !== confirmPassword) return setError('Yeni şifreler eşleşmiyor.');
    if (!isStrongPassword(newPassword)) return setError(passwordHelp);
    setBusy(true);
    try {
      const session = await changePassword(token, currentPassword, newPassword);
      await saveSession(session); onSessionChanged(session); setDialog(null); clearForm(); Alert.alert('Şifre değiştirildi', session.message);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Şifre değiştirilemedi.'); }
    finally { setBusy(false); }
  };
  const chooseProfileImage = async () => {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) return Alert.alert('Galeri izni gerekli', 'Profil fotoğrafı seçmek için galeri erişimine izin ver.');
    const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], allowsEditing: true, aspect: [1, 1], quality: 0.82 });
    if (!result.canceled) { setProfileImage(result.assets[0]); setRemoveProfileImage(false); }
  };
  const submitProfile = async () => {
    if (!profile) return;
    if (!profile.city?.trim()) return Alert.alert('Şehir gerekli', 'Lütfen yaşadığın şehri yaz.');
    if (!profile.occupation?.trim()) return Alert.alert('Meslek gerekli', 'Lütfen mesleğini yaz.');
    if (!profile.livingSituation?.trim()) return Alert.alert('Birlikte yaşam bilgisi gerekli', 'Lütfen yaşam durumunu seç.');
    if (profile.hasChildren == null) return Alert.alert('Çocuk bilgisi gerekli', 'Evde çocuk olup olmadığını seç.');
    if (profile.hasOtherPets == null) return Alert.alert('Evcil hayvan bilgisi gerekli', 'Başka evcil hayvanın olup olmadığını seç.');
    setBusy(true); setError(null);
    try {
      const file = profileImage ? new File(profileImage.uri) : null;
      const updated = await updateProfile(token, { bio: profile.bio, city: profile.city, occupation: profile.occupation, livingSituation: profile.livingSituation, hasChildren: profile.hasChildren, hasOtherPets: profile.hasOtherPets, imageBase64: file ? await file.base64() : null, imageContentType: profileImage?.mimeType || file?.type || null, removeProfileImage });
      setProfile(updated); setProfileImage(null); setRemoveProfileImage(false); setDialog(null); Alert.alert('Profil güncellendi', 'Bilgilerin başarıyla kaydedildi.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Profil güncellenemedi.'); } finally { setBusy(false); }
  };
  const submitDelete = async () => {
    setError(null);
    if (!currentPassword) return setError('Mevcut şifreni girmelisin.');
    if (deletePhrase.trim().toLocaleUpperCase('tr-TR') !== 'SİL') return setError('Onay alanına SİL yazmalısın.');
    setBusy(true);
    try { await deleteAccount(token, currentPassword, 'SİL'); setDialog(null); clearForm(); onDeleted(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Hesap silinemedi.'); }
    finally { setBusy(false); }
  };
  const confirmLogout = () => Alert.alert('Çıkış yapmak istiyor musun?', 'Bu cihazdaki oturum sunucuda da kapatılacak.', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Çıkış Yap', style: 'destructive', onPress: onLogout }]);
  const confirmDelete = () => Alert.alert('Hesabını kalıcı olarak sil?', 'Patilerin ve hesabına bağlı içerikler silinir. Bu işlem geri alınamaz.', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Devam Et', style: 'destructive', onPress: () => setDialog('delete') }]);

  return <><ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <View style={styles.header}><Pressable onPress={onBack} style={styles.roundButton}><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable><Text style={styles.headerTitle}>Hesabım</Text><View style={styles.brandButton}><BrandMark size={42} /></View></View>
    <View style={styles.profileCard}>{profile?.profileImage && profile.profileImage !== 'img/user-profile.jpg' ? <Image source={{ uri: mediaUrl(profile.profileImage) }} style={styles.avatarImage} /> : <View style={styles.avatar}><Text style={styles.avatarText}>{username.charAt(0).toLocaleUpperCase('tr-TR')}</Text></View>}<Text style={styles.username}>{username}</Text><Text style={styles.memberText}>{profile?.bio || 'Henüz hakkında bilgisi eklenmedi.'}</Text><View style={styles.activeBadge}><Ionicons name="shield-checkmark" size={15} color="#41604A" /><Text style={styles.activeText}>Oturum güvenli</Text></View></View>
    <View style={styles.menuCard}><MenuRow icon="person-circle-outline" title="Profilimi düzenle" subtitle="Fotoğraf, hakkında ve yaşam bilgileri" onPress={() => setDialog('profile')} /><MenuRow icon="call-outline" title="İletişim bilgileri" subtitle="Telefon, e-posta ve paylaşım izinlerini yönet" onPress={onOpenContactSettings} /><MenuRow icon="paw-outline" title="Patilerim" subtitle="Evcil hayvan profillerini yönet" onPress={onOpenPets} /><MenuRow icon="person-outline" title="Kullanıcı adını değiştir" subtitle="Toplulukta görünen kullanıcı adını güncelle" onPress={() => { setUsernameInput(username); setDialog('username'); }} /><MenuRow icon="key-outline" title="Şifreyi değiştir" subtitle="Mevcut şifrenle yeni şifre oluştur" onPress={() => setDialog('password')} last /></View>
    <Pressable onPress={confirmLogout} style={({ pressed }) => [styles.logoutButton, pressed && styles.pressed]}><Ionicons name="log-out-outline" size={21} color="#9B463B" /><Text style={styles.logoutText}>Çıkış Yap</Text></Pressable>
    <Pressable onPress={confirmDelete} style={({ pressed }) => [styles.deleteButton, pressed && styles.pressed]}><Text style={styles.deleteText}>Hesabımı Kalıcı Olarak Sil</Text></Pressable>
  </ScrollView>
  <ProfileEditModal visible={dialog === 'profile'} profile={profile} selectedImage={profileImage} removeImage={removeProfileImage} busy={busy} error={error} onClose={closeDialog} onPickImage={chooseProfileImage} onRemoveImage={() => { setProfileImage(null); setRemoveProfileImage(true); }} onChange={setProfile} onOpenContactSettings={onOpenContactSettings} onSave={submitProfile} />
  <Modal visible={dialog !== null && dialog !== 'profile'} transparent animationType="fade" onRequestClose={closeDialog}><KeyboardAvoidingView style={styles.overlay} behavior={Platform.OS === 'ios' ? 'padding' : undefined}><View style={styles.dialog}>
    <View style={styles.dialogTitleRow}><Text style={styles.dialogTitle}>{dialog === 'username' ? 'Kullanıcı adını değiştir' : dialog === 'password' ? 'Şifreyi değiştir' : 'Hesabı kalıcı olarak sil'}</Text><Pressable disabled={busy} onPress={closeDialog}><Ionicons name="close" size={24} color={colors.muted} /></Pressable></View>
    {dialog === 'delete' ? <Text style={styles.warning}>Bu işlem geri alınamaz. Onaylamak için şifreni ve SİL ifadesini gir.</Text> : dialog === 'username' ? <Text style={styles.helper}>3-50 karakter kullan. Harf, rakam, nokta, tire ve alt çizgi kabul edilir.</Text> : <Text style={styles.helper}>{passwordHelp}</Text>}{dialog === 'username' ? <Field value={usernameInput} onChangeText={setUsernameInput} placeholder="Yeni kullanıcı adı" autoCapitalize="none" maxLength={50} /> : <Field value={currentPassword} onChangeText={setCurrentPassword} placeholder="Mevcut şifre" secureTextEntry />}
    {dialog === 'password' ? <><Field value={newPassword} onChangeText={setNewPassword} placeholder="Yeni şifre" secureTextEntry /><Field value={confirmPassword} onChangeText={setConfirmPassword} placeholder="Yeni şifre tekrar" secureTextEntry /></> : dialog === 'delete' ? <Field value={deletePhrase} onChangeText={setDeletePhrase} placeholder="SİL yaz" autoCapitalize="characters" /> : null}
    {error ? <Text style={styles.error}>{error}</Text> : null}
    <Pressable disabled={busy || (dialog === 'delete' && deletePhrase.trim().toLocaleUpperCase('tr-TR') !== 'SİL')} onPress={dialog === 'username' ? submitUsername : dialog === 'password' ? submitPassword : submitDelete} style={[styles.primary, dialog === 'delete' && styles.danger, busy && styles.disabled]}>{busy ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>{dialog === 'username' ? 'Kullanıcı Adını Kaydet' : dialog === 'password' ? 'Şifreyi Değiştir' : 'Hesabımı Sil'}</Text>}</Pressable>
  </View></KeyboardAvoidingView></Modal></>;
}

function ProfileEditModal({ visible, profile, selectedImage, removeImage, busy, error, onClose, onPickImage, onRemoveImage, onChange, onOpenContactSettings, onSave }: { visible: boolean; profile: CurrentUser | null; selectedImage: ImagePicker.ImagePickerAsset | null; removeImage: boolean; busy: boolean; error: string | null; onClose: () => void; onPickImage: () => void; onRemoveImage: () => void; onChange: (profile: CurrentUser) => void; onOpenContactSettings: () => void; onSave: () => void }) {
  const { width, height } = useWindowDimensions();
  const [selection, setSelection] = useState<'city' | 'occupation' | null>(null);
  useEffect(() => { if (!visible) setSelection(null); }, [visible]);
  if (!profile) return null;
  const preview = selectedImage?.uri || (!removeImage && profile.profileImage !== 'img/user-profile.jpg' ? mediaUrl(profile.profileImage) : null);
  const options = selection === 'city' ? cities : occupations;
  const selectedValue = selection === 'city' ? profile.city : profile.occupation;
  const choose = (value: string) => { onChange(selection === 'city' ? { ...profile, city: value } : { ...profile, occupation: value }); setSelection(null); };
  const compactScreen = width < 380 || height < 700;
  return <Modal visible={visible} animationType="slide" presentationStyle="fullScreen" statusBarTranslucent={false} onRequestClose={selection ? () => setSelection(null) : onClose}>
    <KeyboardAvoidingView style={styles.profileModalScreen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      {selection ? <ScrollView contentInsetAdjustmentBehavior="automatic" contentContainerStyle={[styles.profileModalContent, compactScreen && styles.profileModalContentCompact]} showsVerticalScrollIndicator={false}>
        <View style={styles.fullHeader}><Pressable onPress={() => setSelection(null)} style={styles.fullHeaderButton}><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable><View style={styles.flexOne}><Text numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.82} style={styles.fullTitle}>{selection === 'city' ? 'Şehir seç' : 'Meslek seç'}</Text><Text style={styles.fullSubtitle}>Bir seçeneğe dokunarak profile ekle.</Text></View></View>
        <View style={styles.tileList}>{options.map(option => <Pressable key={option} onPress={() => choose(option)} style={({ pressed }) => [styles.optionTile, selectedValue === option && styles.optionTileSelected, pressed && styles.pressed]}><View style={[styles.tileIcon, selectedValue === option && styles.tileIconSelected]}><Ionicons name={selection === 'city' ? 'location-outline' : 'briefcase-outline'} size={20} color={selectedValue === option ? colors.white : colors.primary} /></View><Text style={[styles.optionTileText, selectedValue === option && styles.optionTileTextSelected]}>{option}</Text>{selectedValue === option ? <Ionicons name="checkmark-circle" size={22} color={colors.primary} /> : <Ionicons name="chevron-forward" size={18} color={colors.muted} />}</Pressable>)}</View>
      </ScrollView> : <ScrollView contentInsetAdjustmentBehavior="automatic" contentContainerStyle={[styles.profileModalContent, compactScreen && styles.profileModalContentCompact]} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false}>
        <View style={styles.fullHeader}><View style={styles.flexOne}><Text style={styles.fullTitle}>Profilimi düzenle</Text><Text style={styles.fullSubtitle}>Profil fotoğrafını ve temel bilgilerini güncelle.</Text></View><Pressable disabled={busy} onPress={onClose} style={styles.fullHeaderButton}><Ionicons name="close" size={24} color={colors.primary} /></Pressable></View>
        <View style={styles.previewArea}>{preview ? <Image source={{ uri: preview }} style={styles.profilePreview} /> : <View style={styles.profilePreviewPlaceholder}><Ionicons name="person-outline" size={42} color={colors.primary} /></View>}<Pressable onPress={() => void onPickImage()} style={styles.photoButton}><Ionicons name="images-outline" size={18} color={colors.primary} /><Text style={styles.photoButtonText}>{preview ? 'Profil fotoğrafını değiştir' : 'Profil fotoğrafı ekle'}</Text></Pressable>{preview ? <Pressable onPress={onRemoveImage}><Text style={styles.removePhoto}>Fotoğrafı kaldır</Text></Pressable> : null}</View>
        <Text style={styles.fieldLabel}>Hakkında (isteğe bağlı)</Text><Field value={profile.bio || ''} onChangeText={bio => onChange({ ...profile, bio })} placeholder="Kendinden biraz bahset" multiline maxLength={500} />
        <SelectionField title="Şehir *" value={profile.city} placeholder="Şehir seç" icon="location-outline" onPress={() => setSelection('city')} />
        <SelectionField title="Meslek *" value={profile.occupation} placeholder="Meslek seç" icon="briefcase-outline" onPress={() => setSelection('occupation')} />
        <ProfileChoice title="Birlikte yaşam *" value={profile.livingSituation || null} options={['Tek başına', 'Aileyle', 'Partnerle', 'Ev arkadaşıyla']} onChange={livingSituation => onChange({ ...profile, livingSituation })} />
        <ProfileChoice title="Evde çocuk *" value={profile.hasChildren == null ? null : profile.hasChildren ? 'Var' : 'Yok'} options={['Var', 'Yok']} onChange={value => onChange({ ...profile, hasChildren: value === 'Var' })} />
        <ProfileChoice title="Diğer evcil hayvan *" value={profile.hasOtherPets == null ? null : profile.hasOtherPets ? 'Var' : 'Yok'} options={['Var', 'Yok']} onChange={value => onChange({ ...profile, hasOtherPets: value === 'Var' })} />
        <Pressable onPress={onOpenContactSettings} style={styles.contactLink}><Ionicons name="call-outline" size={18} color={colors.primary} /><View style={styles.flexOne}><Text style={styles.contactLinkTitle}>İletişim bilgileri</Text><Text style={styles.contactLinkText}>Telefon, e-posta ve paylaşım izinlerini Ayarlar’dan yönet</Text></View><Ionicons name="chevron-forward" size={18} color={colors.muted} /></Pressable>
        {error ? <Text style={styles.error}>{error}</Text> : null}<Pressable disabled={busy} onPress={onSave} style={[styles.primary, styles.profileSave, busy && styles.disabled]}>{busy ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>Profili Kaydet</Text>}</Pressable>
      </ScrollView>}
    </KeyboardAvoidingView>
  </Modal>;
}

function SelectionField({ title, value, placeholder, icon, onPress }: { title: string; value?: string | null; placeholder: string; icon: keyof typeof Ionicons.glyphMap; onPress: () => void }) { return <View><Text style={styles.fieldLabel}>{title}</Text><Pressable onPress={onPress} style={({ pressed }) => [styles.selectionField, pressed && styles.pressed]}><Ionicons name={icon} size={20} color={colors.primary} /><Text style={[styles.selectionText, !value && styles.selectionPlaceholder]}>{value || placeholder}</Text><Ionicons name="chevron-forward" size={19} color={colors.muted} /></Pressable></View>; }

function Field(props: React.ComponentProps<typeof TextInput>) { return <TextInput {...props} style={styles.input} placeholderTextColor="#94898D" autoCorrect={false} />; }
function ProfileChoice({ title, value, options, onChange }: { title: string; value: string | null; options: string[]; onChange: (value: string) => void }) { return <View><Text style={styles.fieldLabel}>{title}</Text><View style={styles.choiceRow}>{options.map(option => <Pressable key={option} onPress={() => onChange(option)} style={[styles.choice, value === option && styles.choiceSelected]}><Text style={[styles.choiceText, value === option && styles.choiceTextSelected]}>{option}</Text></Pressable>)}</View></View>; }
function MenuRow({ icon, title, subtitle, last, onPress }: { icon: keyof typeof Ionicons.glyphMap; title: string; subtitle: string; last?: boolean; onPress: () => void }) { return <Pressable onPress={onPress} style={({ pressed }) => [styles.menuRow, last && styles.menuRowLast, pressed && styles.pressed]}><View style={styles.menuIcon}><Ionicons name={icon} size={21} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.menuTitle}>{title}</Text><Text style={styles.menuSubtitle}>{subtitle}</Text></View><Ionicons name="chevron-forward" size={18} color="#AA9DA4" /></Pressable>; }
const passwordHelp = 'En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter kullan.';
const isStrongPassword = (value: string) => value.length >= 8 && value.length <= 100 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /\d/.test(value) && /[^A-Za-z0-9]/.test(value);
const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = createThemedStyles(() => ({
  screen: { flex: 1, backgroundColor: colors.background }, content: { flexGrow: 1, paddingTop: Platform.OS === 'ios' ? 56 : 28, paddingHorizontal: 20, paddingBottom: 112, width: '100%', maxWidth: 620, alignSelf: 'center' }, header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, roundButton: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center', ...shadow }, brandButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center' }, headerTitle: { color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' },
  profileModalScreen: { flex: 1, backgroundColor: colors.background }, profileModalContent: { width: '100%', maxWidth: 680, alignSelf: 'center', paddingTop: Platform.OS === 'ios' ? 58 : 28, paddingHorizontal: 20, paddingBottom: 44 }, profileModalContentCompact: { paddingHorizontal: 16 }, fullHeader: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 20 }, fullHeaderButton: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft }, fullTitle: { color: colors.text, fontFamily: serif, fontSize: 25, lineHeight: 31, fontWeight: '700' }, fullSubtitle: { color: colors.muted, fontSize: 11, lineHeight: 16, marginTop: 2 }, previewArea: { alignItems: 'center', marginBottom: 8 }, profilePreview: { width: 132, height: 132, borderRadius: 66, backgroundColor: colors.lilacSoft, borderWidth: 4, borderColor: colors.card }, profilePreviewPlaceholder: { width: 132, height: 132, borderRadius: 66, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft, borderWidth: 4, borderColor: colors.card }, selectionField: { minHeight: 54, flexDirection: 'row', alignItems: 'center', gap: 10, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card, borderRadius: 15, paddingHorizontal: 14, marginTop: 11 }, selectionText: { flex: 1, color: colors.text, fontSize: 12, fontWeight: '800' }, selectionPlaceholder: { color: colors.muted, fontWeight: '500' }, tileList: { gap: 9 }, optionTile: { minHeight: 64, flexDirection: 'row', alignItems: 'center', gap: 12, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card, borderRadius: 18, paddingHorizontal: 14 }, optionTileSelected: { borderColor: colors.primary, backgroundColor: colors.lilacSoft }, tileIcon: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft }, tileIconSelected: { backgroundColor: colors.primary }, optionTileText: { flex: 1, color: colors.text, fontSize: 12, fontWeight: '800' }, optionTileTextSelected: { color: colors.primary }, profileSave: { minHeight: 56, marginTop: 22 },
  profileCard: { alignItems: 'center', backgroundColor: colors.card, borderRadius: 26, padding: 24, marginTop: 24, borderWidth: 1, borderColor: colors.border, ...shadow }, avatar: { width: 82, height: 82, borderRadius: 41, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', borderWidth: 5, borderColor: colors.lilacSoft }, avatarImage: { width: 82, height: 82, borderRadius: 41, borderWidth: 5, borderColor: colors.lilacSoft }, avatarText: { color: colors.white, fontFamily: serif, fontSize: 36, fontWeight: '700' }, username: { color: colors.text, fontFamily: serif, fontSize: 26, fontWeight: '700', marginTop: 13 }, memberText: { color: colors.muted, fontSize: 11, marginTop: 4, textAlign: 'center' }, activeBadge: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.sageSoft, paddingHorizontal: 11, paddingVertical: 7, borderRadius: 15, marginTop: 13 }, activeText: { color: '#41604A', fontSize: 10, fontWeight: '800' }, profileForm: { maxHeight: 430 }, photoButton: { flexDirection: 'row', gap: 7, paddingVertical: 12, paddingHorizontal: 18, alignItems: 'center', backgroundColor: colors.lilacSoft, borderRadius: 12, marginTop: 12 }, photoButtonText: { color: colors.primary, fontWeight: '800', fontSize: 11 }, removePhoto: { color: '#9B463B', fontSize: 10, textAlign: 'center', marginTop: 9 }, visibilityText: { color: colors.muted, fontSize: 10, marginTop: 11 }, contactLink: { minHeight: 66, flexDirection: 'row', alignItems: 'center', gap: 10, borderRadius: 15, backgroundColor: colors.lilacSoft, paddingHorizontal: 13, marginTop: 15 }, contactLinkTitle: { color: colors.text, fontSize: 11, fontWeight: '900' }, contactLinkText: { color: colors.muted, fontSize: 9, lineHeight: 13, marginTop: 3 },
  menuCard: { backgroundColor: colors.card, borderRadius: 22, marginTop: 16, paddingHorizontal: 16, borderWidth: 1, borderColor: colors.border }, menuRow: { minHeight: 76, flexDirection: 'row', alignItems: 'center', gap: 12, borderBottomWidth: 1, borderBottomColor: colors.border }, menuRowLast: { borderBottomWidth: 0 }, menuIcon: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, flexOne: { flex: 1 }, menuTitle: { color: colors.text, fontSize: 13, fontWeight: '800' }, menuSubtitle: { color: colors.muted, fontSize: 9, marginTop: 4 }, fieldLabel: { color: colors.text, fontSize: 10, fontWeight: '900', marginTop: 13, marginBottom: -5 }, choiceRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 7, marginTop: 10 }, choice: { borderWidth: 1, borderColor: colors.border, borderRadius: 13, paddingHorizontal: 11, paddingVertical: 8, backgroundColor: colors.card }, choiceSelected: { borderColor: colors.primary, backgroundColor: colors.primary }, choiceText: { color: colors.muted, fontSize: 10, fontWeight: '700' }, choiceTextSelected: { color: colors.white }, logoutButton: { minHeight: 53, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach, borderRadius: 18, marginTop: 18 }, logoutText: { color: '#8E4036', fontSize: 13, fontWeight: '900' }, deleteButton: { alignItems: 'center', paddingVertical: 17 }, deleteText: { color: '#9B463B', fontSize: 11, fontWeight: '800', textDecorationLine: 'underline' }, pressed: { opacity: 0.78, transform: [{ scale: 0.99 }] },
  overlay: { flex: 1, backgroundColor: '#2A2026AA', justifyContent: 'center', padding: 22 }, dialog: { width: '100%', maxWidth: 460, alignSelf: 'center', backgroundColor: colors.card, borderRadius: 24, padding: 22 }, dialogTitleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 }, dialogTitle: { flex: 1, color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' }, helper: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 12 }, warning: { color: '#823D35', backgroundColor: colors.peachSoft, borderRadius: 12, padding: 12, fontSize: 11, lineHeight: 17, marginTop: 12 }, input: { minHeight: 50, borderWidth: 1, borderColor: colors.border, borderRadius: 13, paddingHorizontal: 14, color: colors.text, marginTop: 12 }, error: { color: '#823D35', fontSize: 10, lineHeight: 15, marginTop: 10 }, primary: { minHeight: 50, borderRadius: 14, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', marginTop: 16 }, danger: { backgroundColor: '#9B463B' }, disabled: { opacity: 0.5 }, primaryText: { color: colors.white, fontSize: 12, fontWeight: '900' },
}));
