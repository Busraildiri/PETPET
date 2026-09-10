import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useState } from 'react';
import { ActivityIndicator, Alert, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { changePassword, deleteAccount, updateUsername, type AuthResponse } from '../api';
import { BrandMark } from '../components/BrandMark';
import { saveSession, updateStoredUsername } from '../session';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

type Props = { username: string; token: string; onBack: () => void; onOpenPets: () => void; onLogout: () => void; onSessionChanged: (session: AuthResponse) => void; onUsernameChanged: (username: string) => void; onDeleted: () => void };

export function AccountScreen({ username, token, onBack, onOpenPets, onLogout, onSessionChanged, onUsernameChanged, onDeleted }: Props) {
  const [dialog, setDialog] = useState<'username' | 'password' | 'delete' | null>(null);
  const [usernameInput, setUsernameInput] = useState(username);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [deletePhrase, setDeletePhrase] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
    <View style={styles.profileCard}><View style={styles.avatar}><Text style={styles.avatarText}>{username.charAt(0).toLocaleUpperCase('tr-TR')}</Text></View><Text style={styles.username}>{username}</Text><Text style={styles.memberText}>PetWork topluluk üyesi</Text><View style={styles.activeBadge}><Ionicons name="shield-checkmark" size={15} color="#41604A" /><Text style={styles.activeText}>Oturum güvenli</Text></View></View>
    <View style={styles.menuCard}><MenuRow icon="paw-outline" title="Patilerim" subtitle="Evcil hayvan profillerini yönet" onPress={onOpenPets} /><MenuRow icon="person-outline" title="Kullanıcı adını değiştir" subtitle="Toplulukta görünen kullanıcı adını güncelle" onPress={() => { setUsernameInput(username); setDialog('username'); }} /><MenuRow icon="key-outline" title="Şifreyi değiştir" subtitle="Mevcut şifrenle yeni şifre oluştur" onPress={() => setDialog('password')} last /></View>
    <Pressable onPress={confirmLogout} style={({ pressed }) => [styles.logoutButton, pressed && styles.pressed]}><Ionicons name="log-out-outline" size={21} color="#9B463B" /><Text style={styles.logoutText}>Çıkış Yap</Text></Pressable>
    <Pressable onPress={confirmDelete} style={({ pressed }) => [styles.deleteButton, pressed && styles.pressed]}><Text style={styles.deleteText}>Hesabımı Kalıcı Olarak Sil</Text></Pressable>
  </ScrollView>
  <Modal visible={dialog !== null} transparent animationType="fade" onRequestClose={closeDialog}><KeyboardAvoidingView style={styles.overlay} behavior={Platform.OS === 'ios' ? 'padding' : undefined}><View style={styles.dialog}>
    <View style={styles.dialogTitleRow}><Text style={styles.dialogTitle}>{dialog === 'username' ? 'Kullanıcı adını değiştir' : dialog === 'password' ? 'Şifreyi değiştir' : 'Hesabı kalıcı olarak sil'}</Text><Pressable disabled={busy} onPress={closeDialog}><Ionicons name="close" size={24} color={colors.muted} /></Pressable></View>
    {dialog === 'delete' ? <Text style={styles.warning}>Bu işlem geri alınamaz. Onaylamak için şifreni ve SİL ifadesini gir.</Text> : dialog === 'username' ? <Text style={styles.helper}>3-50 karakter kullan. Harf, rakam, nokta, tire ve alt çizgi kabul edilir.</Text> : <Text style={styles.helper}>{passwordHelp}</Text>}
    {dialog === 'username' ? <Field value={usernameInput} onChangeText={setUsernameInput} placeholder="Yeni kullanıcı adı" autoCapitalize="none" maxLength={50} /> : <Field value={currentPassword} onChangeText={setCurrentPassword} placeholder="Mevcut şifre" secureTextEntry />}
    {dialog === 'password' ? <><Field value={newPassword} onChangeText={setNewPassword} placeholder="Yeni şifre" secureTextEntry /><Field value={confirmPassword} onChangeText={setConfirmPassword} placeholder="Yeni şifre tekrar" secureTextEntry /></> : dialog === 'delete' ? <Field value={deletePhrase} onChangeText={setDeletePhrase} placeholder="SİL yaz" autoCapitalize="characters" /> : null}
    {error ? <Text style={styles.error}>{error}</Text> : null}
    <Pressable disabled={busy || (dialog === 'delete' && deletePhrase.trim().toLocaleUpperCase('tr-TR') !== 'SİL')} onPress={dialog === 'username' ? submitUsername : dialog === 'password' ? submitPassword : submitDelete} style={[styles.primary, dialog === 'delete' && styles.danger, busy && styles.disabled]}>{busy ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>{dialog === 'username' ? 'Kullanıcı Adını Kaydet' : dialog === 'password' ? 'Şifreyi Değiştir' : 'Hesabımı Sil'}</Text>}</Pressable>
  </View></KeyboardAvoidingView></Modal></>;
}

function Field(props: React.ComponentProps<typeof TextInput>) { return <TextInput {...props} style={styles.input} placeholderTextColor="#94898D" autoCorrect={false} />; }
function MenuRow({ icon, title, subtitle, last, onPress }: { icon: keyof typeof Ionicons.glyphMap; title: string; subtitle: string; last?: boolean; onPress: () => void }) { return <Pressable onPress={onPress} style={({ pressed }) => [styles.menuRow, last && styles.menuRowLast, pressed && styles.pressed]}><View style={styles.menuIcon}><Ionicons name={icon} size={21} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.menuTitle}>{title}</Text><Text style={styles.menuSubtitle}>{subtitle}</Text></View><Ionicons name="chevron-forward" size={18} color="#AA9DA4" /></Pressable>; }
const passwordHelp = 'En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter kullan.';
const isStrongPassword = (value: string) => value.length >= 8 && value.length <= 100 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /\d/.test(value) && /[^A-Za-z0-9]/.test(value);
const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = createThemedStyles(() => ({
  screen: { flex: 1, backgroundColor: colors.background }, content: { flexGrow: 1, paddingTop: Platform.OS === 'ios' ? 56 : 28, paddingHorizontal: 20, paddingBottom: 112, width: '100%', maxWidth: 620, alignSelf: 'center' }, header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, roundButton: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center', ...shadow }, brandButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center' }, headerTitle: { color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' },
  profileCard: { alignItems: 'center', backgroundColor: colors.card, borderRadius: 26, padding: 24, marginTop: 24, borderWidth: 1, borderColor: colors.border, ...shadow }, avatar: { width: 82, height: 82, borderRadius: 41, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', borderWidth: 5, borderColor: colors.lilacSoft }, avatarText: { color: colors.white, fontFamily: serif, fontSize: 36, fontWeight: '700' }, username: { color: colors.text, fontFamily: serif, fontSize: 26, fontWeight: '700', marginTop: 13 }, memberText: { color: colors.muted, fontSize: 11, marginTop: 4 }, activeBadge: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.sageSoft, paddingHorizontal: 11, paddingVertical: 7, borderRadius: 15, marginTop: 13 }, activeText: { color: '#41604A', fontSize: 10, fontWeight: '800' },
  menuCard: { backgroundColor: colors.card, borderRadius: 22, marginTop: 16, paddingHorizontal: 16, borderWidth: 1, borderColor: colors.border }, menuRow: { minHeight: 76, flexDirection: 'row', alignItems: 'center', gap: 12, borderBottomWidth: 1, borderBottomColor: colors.border }, menuRowLast: { borderBottomWidth: 0 }, menuIcon: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, flexOne: { flex: 1 }, menuTitle: { color: colors.text, fontSize: 13, fontWeight: '800' }, menuSubtitle: { color: colors.muted, fontSize: 9, marginTop: 4 }, logoutButton: { minHeight: 53, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach, borderRadius: 18, marginTop: 18 }, logoutText: { color: '#8E4036', fontSize: 13, fontWeight: '900' }, deleteButton: { alignItems: 'center', paddingVertical: 17 }, deleteText: { color: '#9B463B', fontSize: 11, fontWeight: '800', textDecorationLine: 'underline' }, pressed: { opacity: 0.78, transform: [{ scale: 0.99 }] },
  overlay: { flex: 1, backgroundColor: '#2A2026AA', justifyContent: 'center', padding: 22 }, dialog: { width: '100%', maxWidth: 460, alignSelf: 'center', backgroundColor: colors.card, borderRadius: 24, padding: 22 }, dialogTitleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 }, dialogTitle: { flex: 1, color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' }, helper: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 12 }, warning: { color: '#823D35', backgroundColor: colors.peachSoft, borderRadius: 12, padding: 12, fontSize: 11, lineHeight: 17, marginTop: 12 }, input: { minHeight: 50, borderWidth: 1, borderColor: colors.border, borderRadius: 13, paddingHorizontal: 14, color: colors.text, marginTop: 12 }, error: { color: '#823D35', fontSize: 10, lineHeight: 15, marginTop: 10 }, primary: { minHeight: 50, borderRadius: 14, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', marginTop: 16 }, danger: { backgroundColor: '#9B463B' }, disabled: { opacity: 0.5 }, primaryText: { color: colors.white, fontSize: 12, fontWeight: '900' },
}));
