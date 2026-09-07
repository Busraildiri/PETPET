import { Ionicons } from '@expo/vector-icons';
import { useState } from 'react';
import {
  ActivityIndicator, Alert, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
  useWindowDimensions,
} from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { forgotPassword, loginUser, registerUser, resetPassword, type AuthResponse } from '../api';
import { saveSession } from '../session';
import { colors, shadow } from '../theme';

export type AuthMode = 'login' | 'register' | 'forgot' | 'reset';

export function AuthScreen({ initialMode, resetToken = '', onBack, onAuthenticated }: { initialMode: AuthMode; resetToken?: string; onBack: () => void; onAuthenticated: (session: AuthResponse) => void }) {
  const { width, height } = useWindowDimensions();
  const [mode, setMode] = useState<AuthMode>(initialMode);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(true);
  const [accepted, setAccepted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const isLogin = mode === 'login';
  const isRegister = mode === 'register';
  const isForgot = mode === 'forgot';
  const isReset = mode === 'reset';
  const isPhone = width < 600;
  const isShort = height < 760;

  const switchMode = (next: AuthMode) => {
    setMode(next);
    setPassword('');
    setConfirmPassword('');
    setFormError(null);
  };

  const submit = async () => {
    setFormError(null);
    if (isForgot) {
      if (!email.trim()) { setFormError('E-posta alanını doldurmalısın.'); return; }
      setSubmitting(true);
      try { Alert.alert('İstek alındı', await forgotPassword(email.trim()), [{ text: 'Tamam', onPress: () => switchMode('login') }]); }
      catch (reason) { setFormError(reason instanceof Error ? reason.message : 'İstek gönderilemedi.'); }
      finally { setSubmitting(false); }
      return;
    }
    if (isReset) {
      if (!password || !confirmPassword) { setFormError('Yeni şifre alanlarını doldurmalısın.'); return; }
      if (password !== confirmPassword) { setFormError('Şifreler eşleşmiyor.'); return; }
      if (!isStrongPassword(password)) { setFormError(passwordHelp); return; }
      setSubmitting(true);
      try { Alert.alert('Şifren yenilendi', await resetPassword(resetToken, password), [{ text: 'Giriş Yap', onPress: () => switchMode('login') }]); }
      catch (reason) { setFormError(reason instanceof Error ? reason.message : 'Şifre yenilenemedi.'); }
      finally { setSubmitting(false); }
      return;
    }
    if (!email.trim() || !password) {
      setFormError('E-posta ve şifre alanlarını doldurmalısın.');
      return;
    }
    if (isRegister && !name.trim()) {
      setFormError('Kullanıcı adı alanını doldurmalısın.');
      return;
    }
    if (isRegister && password !== confirmPassword) {
      setFormError('Şifre ve şifre tekrar alanları eşleşmiyor.');
      return;
    }
    if (isRegister && !isStrongPassword(password)) { setFormError(passwordHelp); return; }
    if (isRegister && !accepted) {
      setFormError('Üyelik koşullarını ve gizlilik politikasını kabul etmelisin.');
      return;
    }

    setSubmitting(true);
    try {
      const result = isLogin
        ? await loginUser({ emailOrUsername: email.trim(), password, rememberMe: remember })
        : await registerUser({
          username: name.trim(), email: email.trim(), password, confirmPassword, acceptTerms: accepted, rememberMe: true,
        });
      await saveSession(result);
      Alert.alert(isLogin ? 'Hoş geldin!' : 'Kaydın tamamlandı!', result.message, [{ text: 'Devam Et', onPress: () => onAuthenticated(result) }]);
    } catch (reason) {
      setFormError(reason instanceof Error ? reason.message : `${isLogin ? 'Giriş' : 'Kayıt'} işlemi tamamlanamadı.`);
    } finally {
      setSubmitting(false);
    }
  };

  return <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <StatusBar style="dark" />
    <ScrollView contentContainerStyle={[styles.scroll, isPhone && styles.scrollPhone]} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false} contentInsetAdjustmentBehavior="never">
      <View style={[
        styles.card,
        isRegister && styles.registerCard,
        isPhone && styles.cardPhone,
        isPhone && { minHeight: Math.max(height, isRegister ? 790 : 700) },
      ]}>
        <View style={styles.cornerWash} /><View style={styles.ribbonTop} /><View style={styles.ribbonSide} />
        <View style={styles.cardTopRow}>
          <Pressable onPress={onBack} accessibilityRole="button" accessibilityLabel="Ana sayfaya dön" style={styles.backLink}>
            <Ionicons name="arrow-back" size={20} color={colors.primary} />
          </Pressable>
          <Pressable onPress={onBack} accessibilityRole="button" accessibilityLabel="Menüyü kapat" style={styles.menuButton}>
            <Ionicons name="menu" size={27} color={colors.text} />
          </Pressable>
        </View>

        <View style={[styles.formArea, isRegister && styles.formAreaRegister, isShort && styles.formAreaShort, isRegister && isShort && styles.formAreaRegisterShort]}>
        <Text style={styles.title}>{isLogin ? 'Giriş Yap' : isRegister ? 'Hesap Oluştur' : isForgot ? 'Şifremi Unuttum' : 'Yeni Şifre'}</Text>
        {isForgot ? <Text style={styles.helperText}>Hesabına ait e-posta adresini gir. Hesap varsa güvenli yenileme bağlantısı gönderilir.</Text> : null}

        {isRegister ? <AuthInput icon="person-outline" value={name} onChangeText={setName} placeholder="Ad veya kullanıcı adı" autoCapitalize="words" /> : null}
        {!isReset ? <AuthInput icon="mail-outline" value={email} onChangeText={setEmail} placeholder={isLogin ? 'E-posta veya kullanıcı adı' : 'E-posta adresi'} keyboardType="email-address" /> : null}
        {!isForgot ? <AuthInput icon="lock-closed-outline" value={password} onChangeText={setPassword} placeholder={isReset ? 'Yeni şifre' : 'Şifre'} secureTextEntry={!showPassword}
          right={<Pressable onPress={() => setShowPassword(value => !value)} accessibilityLabel={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'} hitSlop={10}>
            <Ionicons name={showPassword ? 'eye-off-outline' : 'eye-outline'} size={20} color={colors.muted} />
          </Pressable>} /> : null}
        {(isRegister || isReset) ? <AuthInput icon="shield-checkmark-outline" value={confirmPassword} onChangeText={setConfirmPassword} placeholder="Şifre tekrar" secureTextEntry={!showPassword} /> : null}
        {(isRegister || isReset) ? <Text style={styles.passwordHelp}>{passwordHelp}</Text> : null}

        {isLogin ? <View style={styles.optionsRow}>
          <CheckRow checked={remember} onPress={() => setRemember(!remember)} label="Beni hatırla" />
          <Pressable onPress={() => switchMode('forgot')}><Text style={styles.linkSmall}>Şifremi unuttum</Text></Pressable>
        </View> : isRegister ? <View style={styles.termsBox}><CheckRow checked={accepted} onPress={() => setAccepted(!accepted)} label="Üyelik koşullarını ve gizlilik politikasını kabul ediyorum." /></View> : null}

        {formError ? <View style={styles.errorBox}><Ionicons name="alert-circle-outline" size={17} color="#9B463B" /><Text style={styles.errorText}>{formError}</Text></View> : null}

        <Pressable onPress={submit} disabled={submitting} accessibilityRole="button" accessibilityState={{ disabled: submitting }} style={({ pressed }) => [styles.submitButton, submitting && styles.submitDisabled, pressed && styles.pressed]}>
          {submitting ? <ActivityIndicator color={colors.white} /> : <><Text style={styles.submitText}>{isLogin ? 'Giriş Yap' : isRegister ? 'Üye Ol' : isForgot ? 'Bağlantı Gönder' : 'Şifreyi Yenile'}</Text><Ionicons name="arrow-forward" size={19} color={colors.white} /></>}
        </Pressable>

        {(isLogin || isRegister) ? <>
        <View style={styles.switchRow}><Text style={styles.switchText}>{isLogin ? 'Henüz hesabın yok mu?' : 'Zaten hesabın var mı?'}</Text>
          <Pressable onPress={() => switchMode(isLogin ? 'register' : 'login')}><Text style={styles.switchLink}>{isLogin ? 'Üye Ol' : 'Giriş Yap'}</Text></Pressable>
        </View>
        </> : <Pressable onPress={() => switchMode('login')} style={styles.switchRow}><Text style={styles.switchLink}>Giriş ekranına dön</Text></Pressable>}
        </View>
      </View>
    </ScrollView>
  </KeyboardAvoidingView>;
}

function AuthInput({ icon, right, ...props }: {
  icon: keyof typeof Ionicons.glyphMap;
  right?: React.ReactNode;
  value: string;
  onChangeText: (value: string) => void;
  placeholder: string;
  secureTextEntry?: boolean;
  keyboardType?: 'default' | 'email-address';
  autoCapitalize?: 'none' | 'words';
}) {
  return <View style={styles.inputWrap}><Ionicons name={icon} size={19} color={colors.primary} />
    <TextInput {...props} style={styles.input} placeholderTextColor="#94898D" autoCapitalize={props.autoCapitalize ?? 'none'} autoCorrect={false} />{right}
  </View>;
}

function CheckRow({ checked, onPress, label }: { checked: boolean; onPress: () => void; label: string }) {
  return <Pressable onPress={onPress} accessibilityRole="checkbox" accessibilityState={{ checked }} style={styles.checkRow}>
    <View style={[styles.checkbox, checked && styles.checkboxChecked]}>{checked ? <Ionicons name="checkmark" size={14} color={colors.white} /> : null}</View>
    <Text style={styles.checkLabel}>{label}</Text>
  </Pressable>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const passwordHelp = 'En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter kullan.';
const isStrongPassword = (value: string) => value.length >= 8 && value.length <= 100 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /\d/.test(value) && /[^A-Za-z0-9]/.test(value);

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.card }, scroll: { flexGrow: 1, justifyContent: 'center', paddingVertical: Platform.OS === 'ios' ? 30 : 18, paddingHorizontal: 24, backgroundColor: '#F2F0F1' }, scrollPhone: { justifyContent: 'flex-start', paddingVertical: 0, paddingHorizontal: 0, backgroundColor: colors.card },
  card: { width: '100%', maxWidth: 390, minHeight: 650, alignSelf: 'center', backgroundColor: colors.card, borderRadius: 24, paddingHorizontal: 34, paddingVertical: 24, overflow: 'hidden', ...shadow }, registerCard: { minHeight: 720 },
  cardPhone: { maxWidth: '100%', borderRadius: 0, paddingTop: Platform.OS === 'ios' ? 52 : 28, paddingBottom: 32, shadowOpacity: 0, elevation: 0 },
  cornerWash: { position: 'absolute', width: 245, height: 175, borderBottomRightRadius: 120, backgroundColor: colors.yellowSoft, opacity: 0.58, top: 0, left: 0 },
  ribbonTop: { position: 'absolute', width: 230, height: 28, borderRadius: 18, backgroundColor: colors.peach, top: 30, left: 95, transform: [{ rotate: '-24deg' }] },
  ribbonSide: { position: 'absolute', width: 190, height: 28, borderRadius: 18, backgroundColor: colors.peach, top: 119, left: -65, transform: [{ rotate: '-59deg' }] },
  cardTopRow: { zIndex: 2, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }, backLink: { width: 38, height: 38, borderRadius: 19, backgroundColor: '#FFFCF8CC', alignItems: 'center', justifyContent: 'center' }, menuButton: { width: 38, height: 38, alignItems: 'center', justifyContent: 'center' },
  formArea: { marginTop: 135 }, formAreaRegister: { marginTop: 82 }, formAreaShort: { marginTop: 98 }, formAreaRegisterShort: { marginTop: 48 }, title: { color: colors.text, fontSize: 28, fontWeight: '900', marginBottom: 18 },
  helperText: { color: colors.muted, fontSize: 11, lineHeight: 17, marginTop: -8, marginBottom: 8 }, passwordHelp: { color: colors.muted, fontSize: 9, lineHeight: 14, marginTop: 7 },
  inputWrap: { minHeight: 53, flexDirection: 'row', alignItems: 'center', gap: 10, borderBottomWidth: 1, borderBottomColor: '#D8C9D3', marginTop: 7 }, input: { flex: 1, color: colors.text, fontSize: 13, paddingVertical: 14 },
  optionsRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginTop: 13 }, checkRow: { flexDirection: 'row', alignItems: 'center', gap: 7, flex: 1 },
  checkbox: { width: 17, height: 17, borderRadius: 3, borderWidth: 1, borderColor: '#B7A7B1', backgroundColor: colors.yellowSoft, alignItems: 'center', justifyContent: 'center' }, checkboxChecked: { backgroundColor: colors.primary, borderColor: colors.primary },
  checkLabel: { flex: 1, color: colors.text, fontSize: 9, lineHeight: 14 }, linkSmall: { color: colors.text, fontSize: 9, fontWeight: '600' }, termsBox: { marginTop: 14, backgroundColor: colors.sageSoft, borderRadius: 12, padding: 10 },
  errorBox: { flexDirection: 'row', alignItems: 'center', gap: 7, backgroundColor: colors.peachSoft, borderRadius: 10, padding: 10, marginTop: 10 }, errorText: { flex: 1, color: '#823D35', fontSize: 10, lineHeight: 14 },
  submitButton: { minHeight: 45, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7, backgroundColor: colors.primary, borderRadius: 7, marginTop: 19 }, submitDisabled: { opacity: 0.6 }, submitText: { color: colors.white, fontSize: 13, fontWeight: '900', textTransform: 'uppercase' }, pressed: { opacity: 0.8, transform: [{ scale: 0.99 }] },
  switchRow: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', flexWrap: 'wrap', gap: 4, marginTop: 17 }, switchText: { color: colors.text, fontSize: 9 }, switchLink: { color: colors.peach, fontSize: 9, fontWeight: '900', textDecorationLine: 'underline' },
});
